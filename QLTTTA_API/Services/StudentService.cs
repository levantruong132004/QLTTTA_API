using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace QLTTTA_API.Services
{
    /// <summary>
    /// Interface nghiệp vụ quản lý học viên (CRUD + tìm kiếm + phân trang).
    /// </summary>
    public interface IStudentService
    {
        /// <summary>Lấy danh sách học viên phân trang có thể kèm từ khóa tìm kiếm.</summary>
        Task<PaginatedResponse<Student>> GetStudentsAsync(int pageNumber = 1, int pageSize = 10, string? search = null);
        /// <summary>Lấy thông tin học viên theo ID.</summary>
        Task<Student?> GetStudentByIdAsync(int id);
        /// <summary>Tạo học viên mới (bao gồm tạo tài khoản kèm). Transaction đảm bảo cả hai bảng được tạo thành công.</summary>
        Task<ApiResponse<Student>> CreateStudentAsync(StudentCreateDto dto);
        /// <summary>Cập nhật thông tin học viên.</summary>
        Task<ApiResponse<Student>> UpdateStudentAsync(StudentUpdateDto dto);
        /// <summary>Vô hiệu hóa (soft delete) tài khoản học viên bằng IS_ACTIVE = 0.</summary>
        Task<ApiResponse<bool>> DeleteStudentAsync(int id);
        /// <summary>Tìm kiếm nhanh học viên theo tên hoặc mã.</summary>
        Task<List<Student>> SearchStudentsAsync(string keyword);
    }

    /// <summary>
    /// Triển khai IStudentService. Dựa trên BaseService để tận dụng kết nối per-user nếu cần và tiện ích truy vấn.
    /// </summary>
    public class StudentService : BaseService, IStudentService
    {
        public StudentService(IConfiguration configuration, ILogger<StudentService> logger, IOracleConnectionProvider userConnProvider)
            : base(configuration, logger, userConnProvider) { }

        /// <summary>
        /// Lấy danh sách học viên phân trang. Sử dụng SP_GET_STUDENTS.
        /// </summary>
        public async Task<PaginatedResponse<Student>> GetStudentsAsync(int pageNumber = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                using var connection = await GetConnectionAsync();
                using var command = new OracleCommand("SP_GET_STUDENTS", connection);
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add("p_page_number", OracleDbType.Int32).Value = pageNumber;
                command.Parameters.Add("p_page_size", OracleDbType.Int32).Value = pageSize;
                command.Parameters.Add("p_search", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(search) ? DBNull.Value : search;
                
                command.Parameters.Add("p_cursor", OracleDbType.RefCursor).Direction = ParameterDirection.Output;
                var pTotal = command.Parameters.Add("p_total", OracleDbType.Int32);
                pTotal.Direction = ParameterDirection.Output;

                var students = new List<Student>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        students.Add(MapToObject<Student>(reader));
                    }
                }

                int totalRecords = 0;
                if (pTotal.Value != null && int.TryParse(pTotal.Value.ToString(), out var t))
                {
                    totalRecords = t;
                }

                return new PaginatedResponse<Student>
                {
                    Data = students,
                    TotalRecords = totalRecords,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStudentsAsync");
                return new PaginatedResponse<Student>
                {
                    Data = new List<Student>(),
                    TotalRecords = 0,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
        }

        /// <summary>
        /// Lấy chi tiết một học viên theo ID (chỉ lấy nếu tài khoản active).
        /// </summary>
        public async Task<Student?> GetStudentByIdAsync(int id)
        {
            var students = await ExecuteStoredProcedureQueryAsync<Student>("SP_GET_STUDENT_BY_ID", new { p_id = id });
            return students.FirstOrDefault();
        }

        /// <summary>
        /// Tạo học viên mới kèm tạo tài khoản. Sử dụng các Stored Procedures trong transaction.
        /// </summary>
        public async Task<ApiResponse<Student>> CreateStudentAsync(StudentCreateDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(dto.Email))
                {
                    return new ApiResponse<Student> { Success = false, Message = "Thiếu thông tin tài khoản (username / password / email)" };
                }

                using var connection = await GetConnectionAsync();
                using var transaction = connection.BeginTransaction();
                try
                {
                    // 1. Check Username
                    using (var cmd = new OracleCommand("SP_CHECK_USERNAME_EXISTS", connection))
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = dto.Username.Trim();
                        var pCount = cmd.Parameters.Add("p_count", OracleDbType.Int32);
                        pCount.Direction = ParameterDirection.Output;
                        await cmd.ExecuteNonQueryAsync();
                        if (Convert.ToInt32(pCount.Value.ToString()) > 0)
                        {
                            return new ApiResponse<Student> { Success = false, Message = "Tên đăng nhập đã tồn tại" };
                        }
                    }

                    // 2. Check Email
                    using (var cmd = new OracleCommand("SP_CHECK_EMAIL_EXISTS", connection))
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = dto.Email.Trim();
                        var pCount = cmd.Parameters.Add("p_count", OracleDbType.Int32);
                        pCount.Direction = ParameterDirection.Output;
                        await cmd.ExecuteNonQueryAsync();
                        if (Convert.ToInt32(pCount.Value.ToString()) > 0)
                        {
                            return new ApiResponse<Student> { Success = false, Message = "Email đã được sử dụng" };
                        }
                    }

                    // 3. Get Role ID
                    int roleId = 1;
                    using (var cmd = new OracleCommand("SP_GET_ROLE_ID_BY_NAME", connection))
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("p_role_name", OracleDbType.Varchar2).Value = "HOC VIEN";
                        var pRoleId = cmd.Parameters.Add("p_role_id", OracleDbType.Int32);
                        pRoleId.Direction = ParameterDirection.Output;
                        await cmd.ExecuteNonQueryAsync();
                        if (pRoleId.Value != null && int.TryParse(pRoleId.Value.ToString(), out var rid) && rid > 0)
                        {
                            roleId = rid;
                        }
                    }

                    // 4. Create Account
                    int newUserId = 0;
                    using (var cmd = new OracleCommand("SP_CREATE_ACCOUNT", connection))
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = dto.Username.Trim();
                        cmd.Parameters.Add("p_password", OracleDbType.Varchar2).Value = dto.Password;
                        cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = dto.Email.Trim();
                        cmd.Parameters.Add("p_role_id", OracleDbType.Int32).Value = roleId;
                        var pUserId = cmd.Parameters.Add("p_user_id", OracleDbType.Int32);
                        pUserId.Direction = ParameterDirection.Output;
                        await cmd.ExecuteNonQueryAsync();
                        if (pUserId.Value != null && int.TryParse(pUserId.Value.ToString(), out var uid))
                        {
                            newUserId = uid;
                        }
                    }

                    if (newUserId <= 0)
                    {
                        transaction.Rollback();
                        return new ApiResponse<Student> { Success = false, Message = "Không lấy được USER_ID sau khi tạo tài khoản" };
                    }

                    // 5. Create Student
                    using (var cmd = new OracleCommand("SP_CREATE_STUDENT", connection))
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = newUserId;
                        cmd.Parameters.Add("p_fullname", OracleDbType.NVarchar2).Value = dto.FullName;
                        cmd.Parameters.Add("p_sex", OracleDbType.NVarchar2).Value = (object?)dto.Sex ?? DBNull.Value;
                        cmd.Parameters.Add("p_dob", OracleDbType.Date).Value = dto.DateOfBirth;
                        cmd.Parameters.Add("p_phone", OracleDbType.Varchar2).Value = dto.PhoneNumber;
                        cmd.Parameters.Add("p_addr", OracleDbType.NVarchar2).Value = (object?)dto.Address ?? DBNull.Value;
                        await cmd.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();

                    // 6. Get created student
                    var newStudent = await GetStudentByIdAsync(newUserId);
                    return new ApiResponse<Student>
                    {
                        Success = true,
                        Message = "Tạo học viên & tài khoản thành công",
                        Data = newStudent
                    };
                }
                catch (Exception innerEx)
                {
                    try { transaction.Rollback(); } catch { }
                    _logger.LogError(innerEx, "Rollback student create transaction");
                    return new ApiResponse<Student>
                    {
                        Success = false,
                        Message = "Lỗi tạo học viên: " + innerEx.Message
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating student");
                return new ApiResponse<Student>
                {
                    Success = false,
                    Message = "Lỗi: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Cập nhật thông tin học viên, kiểm tra trùng STUDENT_CODE (ngoại trừ chính nó).
        /// </summary>
        public async Task<ApiResponse<Student>> UpdateStudentAsync(StudentUpdateDto dto)
        {
            try
            {
                var student = await GetStudentByIdAsync(dto.StudentId);
                if (student == null)
                {
                    return new ApiResponse<Student> { Success = false, Message = "Không tìm thấy học viên" };
                }

                // Check duplicate code
                using var connection = await GetConnectionAsync();
                using var cmdCheck = new OracleCommand("SP_CHECK_STUDENT_CODE_EXISTS", connection);
                cmdCheck.CommandType = CommandType.StoredProcedure;
                cmdCheck.Parameters.Add("p_code", OracleDbType.Varchar2).Value = dto.StudentCode;
                cmdCheck.Parameters.Add("p_exclude_id", OracleDbType.Int32).Value = dto.StudentId;
                var pCount = cmdCheck.Parameters.Add("p_count", OracleDbType.Int32);
                pCount.Direction = ParameterDirection.Output;
                await cmdCheck.ExecuteNonQueryAsync();

                if (Convert.ToInt32(pCount.Value.ToString()) > 0)
                {
                    return new ApiResponse<Student> { Success = false, Message = "Mã học viên đã tồn tại" };
                }

                // Update
                using var cmdUpdate = new OracleCommand("SP_UPDATE_STUDENT", connection);
                cmdUpdate.CommandType = CommandType.StoredProcedure;
                cmdUpdate.Parameters.Add("p_id", OracleDbType.Int32).Value = dto.StudentId;
                cmdUpdate.Parameters.Add("p_fullname", OracleDbType.NVarchar2).Value = dto.FullName;
                cmdUpdate.Parameters.Add("p_code", OracleDbType.Varchar2).Value = dto.StudentCode;
                cmdUpdate.Parameters.Add("p_sex", OracleDbType.NVarchar2).Value = dto.Sex;
                cmdUpdate.Parameters.Add("p_dob", OracleDbType.Date).Value = dto.DateOfBirth;
                cmdUpdate.Parameters.Add("p_phone", OracleDbType.Varchar2).Value = dto.PhoneNumber;
                cmdUpdate.Parameters.Add("p_addr", OracleDbType.NVarchar2).Value = dto.Address;
                await cmdUpdate.ExecuteNonQueryAsync();

                var updatedStudent = await GetStudentByIdAsync(dto.StudentId);
                return new ApiResponse<Student>
                {
                    Success = true,
                    Message = "Cập nhật học viên thành công",
                    Data = updatedStudent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating student");
                return new ApiResponse<Student> { Success = false, Message = "Có lỗi xảy ra khi cập nhật học viên" };
            }
        }

        /// <summary>
        /// Vô hiệu hóa học viên bằng cách đặt IS_ACTIVE = 0 trên ACCOUNTS.
        /// </summary>
        public async Task<ApiResponse<bool>> DeleteStudentAsync(int id)
        {
            try
            {
                using var connection = await GetConnectionAsync();
                using var command = new OracleCommand("SP_SOFT_DELETE_STUDENT", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
                var pRowCount = command.Parameters.Add("p_rowcount", OracleDbType.Int32);
                pRowCount.Direction = ParameterDirection.Output;
                
                await command.ExecuteNonQueryAsync();
                
                int affected = 0;
                if (pRowCount.Value != null && int.TryParse(pRowCount.Value.ToString(), out var tmp)) affected = tmp;

                if (affected == 0)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy học viên hoặc tài khoản đã bị khóa" };
                }
                return new ApiResponse<bool> { Success = true, Message = "Đã vô hiệu hóa tài khoản học viên", Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting student");
                return new ApiResponse<bool> { Success = false, Message = "Có lỗi xảy ra khi vô hiệu hóa học viên" };
            }
        }

        /// <summary>
        /// Tìm kiếm nhanh học viên theo FULL_NAME hoặc STUDENT_CODE (chỉ các tài khoản active).
        /// </summary>
        public async Task<List<Student>> SearchStudentsAsync(string keyword)
        {
            return await ExecuteStoredProcedureQueryAsync<Student>("SP_SEARCH_STUDENTS", new { p_keyword = $"%{keyword}%" });
        }
    }
}