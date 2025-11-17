using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;
using Oracle.ManagedDataAccess.Client;

namespace QLTTTA_API.Services
{
    public interface IStudentService
    {
        Task<PaginatedResponse<Student>> GetStudentsAsync(int pageNumber = 1, int pageSize = 10, string? search = null);
        Task<Student?> GetStudentByIdAsync(int id);
        Task<ApiResponse<Student>> CreateStudentAsync(StudentCreateDto dto);
        Task<ApiResponse<Student>> UpdateStudentAsync(StudentUpdateDto dto);
        Task<ApiResponse<bool>> DeleteStudentAsync(int id);
        Task<List<Student>> SearchStudentsAsync(string keyword);
    }

    public class StudentService : BaseService, IStudentService
    {
        public StudentService(IConfiguration configuration, ILogger<StudentService> logger, IOracleConnectionProvider userConnProvider)
            : base(configuration, logger, userConnProvider) { }

        public async Task<PaginatedResponse<Student>> GetStudentsAsync(int pageNumber = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                // Đơn giản hóa: lấy tất cả trước, sau đó phân trang
                string sql;
                object? parameters = null;

                if (string.IsNullOrEmpty(search))
                {
                    sql = @"SELECT 
                                s.ID_HOC_VIEN AS StudentId, 
                                s.HO_TEN AS FullName, 
                                s.MA_HOC_VIEN AS StudentCode, 
                                s.GIOI_TINH AS Sex, 
                                s.NGAY_SINH AS DateOfBirth, 
                                s.SO_DIEN_THOAI AS PhoneNumber, 
                                s.DIA_CHI AS Address
                            FROM QLTT_ADMIN.HOC_VIEN s 
                            ORDER BY s.ID_HOC_VIEN";
                }
                else
                {
                    sql = @"SELECT 
                                s.ID_HOC_VIEN AS StudentId, 
                                s.HO_TEN AS FullName, 
                                s.MA_HOC_VIEN AS StudentCode, 
                                s.GIOI_TINH AS Sex, 
                                s.NGAY_SINH AS DateOfBirth, 
                                s.SO_DIEN_THOAI AS PhoneNumber, 
                                s.DIA_CHI AS Address
                            FROM QLTT_ADMIN.HOC_VIEN s 
                            WHERE (UPPER(s.HO_TEN) LIKE UPPER(:search) OR UPPER(s.MA_HOC_VIEN) LIKE UPPER(:search))
                            ORDER BY s.ID_HOC_VIEN";
                    parameters = new { search = $"%{search}%" };
                }

                var allStudents = await ExecuteQueryAdminAsync<Student>(sql, parameters);
                var totalRecords = allStudents.Count;

                var skip = (pageNumber - 1) * pageSize;
                var pagedStudents = allStudents.Skip(skip).Take(pageSize).ToList();

                return new PaginatedResponse<Student>
                {
                    Data = pagedStudents,
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

        public async Task<Student?> GetStudentByIdAsync(int id)
        {
            var sql = @"SELECT 
                            s.ID_HOC_VIEN AS StudentId, 
                            s.HO_TEN AS FullName, 
                            s.MA_HOC_VIEN AS StudentCode, 
                            s.GIOI_TINH AS Sex, 
                            s.NGAY_SINH AS DateOfBirth, 
                            s.SO_DIEN_THOAI AS PhoneNumber, 
                            s.DIA_CHI AS Address
                        FROM QLTT_ADMIN.HOC_VIEN s
                        WHERE s.ID_HOC_VIEN = :id";
            return await ExecuteQuerySingleAdminAsync<Student>(sql, new { id });
        }

        public async Task<ApiResponse<Student>> CreateStudentAsync(StudentCreateDto dto)
        {
            try
            {
                // Kiểm tra các trường account bổ sung
                if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(dto.Email))
                {
                    return new ApiResponse<Student>
                    {
                        Success = false,
                        Message = "Thiếu thông tin tài khoản (username / password / email)"
                    };
                }

                using var connection = await GetAdminConnectionAsync();
                using var transaction = connection.BeginTransaction();
                try
                {
                    // 1. Kiểm tra trùng TEN_DANG_NHAP / EMAIL
                    using (var checkUserCmd = new OracleCommand("SELECT COUNT(*) FROM QLTT_ADMIN.TAI_KHOAN WHERE TEN_DANG_NHAP = :u", connection))
                    {
                        checkUserCmd.Transaction = transaction;
                        checkUserCmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = dto.Username.Trim();
                        var exists = Convert.ToInt32(await checkUserCmd.ExecuteScalarAsync());
                        if (exists > 0)
                        {
                            return new ApiResponse<Student> { Success = false, Message = "Tên đăng nhập đã tồn tại" };
                        }
                    }
                    using (var checkEmailCmd = new OracleCommand("SELECT COUNT(*) FROM QLTT_ADMIN.TAI_KHOAN WHERE EMAIL = :e", connection))
                    {
                        checkEmailCmd.Transaction = transaction;
                        checkEmailCmd.Parameters.Add(":e", OracleDbType.Varchar2).Value = dto.Email.Trim();
                        var exists = Convert.ToInt32(await checkEmailCmd.ExecuteScalarAsync());
                        if (exists > 0)
                        {
                            return new ApiResponse<Student> { Success = false, Message = "Email đã được sử dụng" };
                        }
                    }

                    // 2. Lấy ID_VAI_TRO của HOCVIEN (fallback 1)
                    int roleId = 1;
                    try
                    {
                        using var roleCmd = new OracleCommand("SELECT ID_VAI_TRO FROM QLTT_ADMIN.VAI_TRO WHERE UPPER(TEN_VAI_TRO) = 'HOCVIEN' FETCH FIRST 1 ROWS ONLY", connection);
                        roleCmd.Transaction = transaction;
                        var roleObj = await roleCmd.ExecuteScalarAsync();
                        if (roleObj != null && int.TryParse(roleObj.ToString(), out var rid) && rid > 0)
                            roleId = rid;
                    }
                    catch { /* ignore, fallback 1 */ }

                    // 3. Insert TAI_KHOAN + lấy ID_NGUOI_DUNG
                    int newUserId = 0;
                    using (var accCmd = new OracleCommand(@"INSERT INTO QLTT_ADMIN.TAI_KHOAN (TEN_DANG_NHAP, MAT_KHAU, EMAIL, ID_VAI_TRO, TRANG_THAI_KICH_HOAT)
                                                             VALUES (:username, :password, :email, :roleId, 1)
                                                             RETURNING ID_NGUOI_DUNG INTO :p_user_id", connection))
                    {
                        accCmd.Transaction = transaction;
                        accCmd.Parameters.Add(":username", OracleDbType.Varchar2).Value = dto.Username.Trim();
                        accCmd.Parameters.Add(":password", OracleDbType.Varchar2).Value = dto.Password; // TODO: hash
                        accCmd.Parameters.Add(":email", OracleDbType.Varchar2).Value = dto.Email.Trim();
                        accCmd.Parameters.Add(":roleId", OracleDbType.Int32).Value = roleId;
                        var outParam = new OracleParameter(":p_user_id", OracleDbType.Int32, System.Data.ParameterDirection.Output);
                        accCmd.Parameters.Add(outParam);
                        await accCmd.ExecuteNonQueryAsync();
                        if (outParam.Value != null && int.TryParse(outParam.Value.ToString(), out var tmpId))
                            newUserId = tmpId;
                    }
                    if (newUserId <= 0)
                    {
                        transaction.Rollback();
                        return new ApiResponse<Student> { Success = false, Message = "Không lấy được ID người dùng sau khi tạo tài khoản" };
                    }

                    // 4. Insert HOC_VIEN (ID_HOC_VIEN = ID_NGUOI_DUNG) - bỏ MA_HOC_VIEN để trigger tự sinh
                    using (var stuCmd = new OracleCommand(@"INSERT INTO QLTT_ADMIN.HOC_VIEN (ID_HOC_VIEN, HO_TEN, GIOI_TINH, NGAY_SINH, SO_DIEN_THOAI, DIA_CHI)
                                                            VALUES (:id, :fullName, :sex, :dob, :phone, :addr)", connection))
                    {
                        stuCmd.Transaction = transaction;
                        stuCmd.Parameters.Add(":id", OracleDbType.Int32).Value = newUserId;
                        stuCmd.Parameters.Add(":fullName", OracleDbType.NVarchar2).Value = dto.FullName;
                        stuCmd.Parameters.Add(":sex", OracleDbType.NVarchar2).Value = (object?)dto.Sex ?? DBNull.Value;
                        stuCmd.Parameters.Add(":dob", OracleDbType.Date).Value = dto.DateOfBirth;
                        stuCmd.Parameters.Add(":phone", OracleDbType.Varchar2).Value = dto.PhoneNumber;
                        stuCmd.Parameters.Add(":addr", OracleDbType.NVarchar2).Value = (object?)dto.Address ?? DBNull.Value;
                        await stuCmd.ExecuteNonQueryAsync();
                    }

                    // 5. Commit transaction
                    transaction.Commit();

                    // 6. Lấy lại student vừa tạo (bao gồm MA_HOC_VIEN do trigger sinh)
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
                    //Message = "Có lỗi xảy ra khi tạo học viên"
                    Message = "Lỗi: " + ex.Message
                };
            }
        }

        public async Task<ApiResponse<Student>> UpdateStudentAsync(StudentUpdateDto dto)
        {
            try
            {
                // Kiểm tra học viên tồn tại
                var student = await GetStudentByIdAsync(dto.StudentId);
                if (student == null)
                {
                    return new ApiResponse<Student>
                    {
                        Success = false,
                        Message = "Không tìm thấy học viên"
                    };
                }

                // Kiểm tra mã học viên trùng (ngoại trừ chính nó)
                var existingSql = @"
                    SELECT COUNT(*) FROM QLTT_ADMIN.HOC_VIEN 
                    WHERE MA_HOC_VIEN = :studentcode AND ID_HOC_VIEN != :studentid";
                var exists = Convert.ToInt32(await ExecuteScalarAsync(existingSql,
                    new { studentcode = dto.StudentCode, studentid = dto.StudentId }));

                if (exists > 0)
                {
                    return new ApiResponse<Student>
                    {
                        Success = false,
                        Message = "Mã học viên đã tồn tại"
                    };
                }

                var sql = @"
                    UPDATE QLTT_ADMIN.HOC_VIEN SET
                        HO_TEN = :fullname,
                        MA_HOC_VIEN = :studentcode,
                        GIOI_TINH = :sex,
                        NGAY_SINH = :dateofbirth,
                        SO_DIEN_THOAI = :phonenumber,
                        DIA_CHI = :address
                    WHERE ID_HOC_VIEN = :studentid";

                var parameters = new
                {
                    fullname = dto.FullName,
                    studentcode = dto.StudentCode,
                    sex = dto.Sex,
                    dateofbirth = dto.DateOfBirth,
                    phonenumber = dto.PhoneNumber,
                    address = dto.Address,
                    studentid = dto.StudentId
                };

                await ExecuteNonQueryAsync(sql, parameters);

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
                return new ApiResponse<Student>
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi cập nhật học viên"
                };
            }
        }

        public async Task<ApiResponse<bool>> DeleteStudentAsync(int id)
        {
            try
            {
                // Kiểm tra học viên có đăng ký học không
                // Soft delete: đặt TRANG_THAI_KICH_HOAT = 0 cho TAI_KHOAN tương ứng
                using var connection = await GetAdminConnectionAsync();
                using var command = new OracleCommand("BEGIN UPDATE QLTT_ADMIN.TAI_KHOAN SET TRANG_THAI_KICH_HOAT = 0 WHERE ID_NGUOI_DUNG = :id; :rowcount := SQL%ROWCOUNT; END;", connection);
                var idParam = new OracleParameter(":id", OracleDbType.Int32) { Value = id };
                var outParam = new OracleParameter(":rowcount", OracleDbType.Int32, System.Data.ParameterDirection.Output);
                command.Parameters.Add(idParam);
                command.Parameters.Add(outParam);
                await command.ExecuteNonQueryAsync();
                int affected = 0;
                if (outParam.Value != null && int.TryParse(outParam.Value.ToString(), out var tmp)) affected = tmp;

                if (affected == 0)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy học viên hoặc tài khoản đã bị khóa" };
                }
                return new ApiResponse<bool> { Success = true, Message = "Đã vô hiệu hóa tài khoản học viên", Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting student");
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi vô hiệu hóa học viên"
                };
            }
        }

        public async Task<List<Student>> SearchStudentsAsync(string keyword)
        {
                        var sql = @"SELECT 
                            s.ID_HOC_VIEN AS StudentId, 
                            s.HO_TEN AS FullName, 
                            s.MA_HOC_VIEN AS StudentCode, 
                            s.GIOI_TINH AS Sex, 
                            s.NGAY_SINH AS DateOfBirth, 
                            s.SO_DIEN_THOAI AS PhoneNumber, 
                            s.DIA_CHI AS Address
                                                FROM QLTT_ADMIN.HOC_VIEN s
                                                WHERE (UPPER(s.HO_TEN) LIKE UPPER(:keyword) OR UPPER(s.MA_HOC_VIEN) LIKE UPPER(:keyword))
                                                ORDER BY s.HO_TEN";
                        return await ExecuteQueryAdminAsync<Student>(sql, new { keyword = $"%{keyword}%" });
        }
    }
}