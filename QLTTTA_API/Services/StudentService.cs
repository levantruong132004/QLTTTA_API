using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;
using Oracle.ManagedDataAccess.Client;

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
        /// <summary>Đồng bộ một học viên vào bảng legacy HOC_VIEN nếu thiếu.</summary>
        Task<bool> SyncLegacyHocVienAsync(int studentId);
    }

    /// <summary>
    /// Triển khai IStudentService. Dựa trên BaseService để tận dụng kết nối per-user nếu cần và tiện ích truy vấn.
    /// </summary>
    public class StudentService : BaseService, IStudentService
    {
        public StudentService(IConfiguration configuration, ILogger<StudentService> logger, IOracleConnectionProvider userConnProvider, IHttpContextAccessor httpContextAccessor)
            : base(configuration, logger, userConnProvider, httpContextAccessor) { }

        /// <summary>
        /// Lấy danh sách học viên phân trang. Hiện tại đơn giản là SELECT toàn bộ rồi thực hiện Skip/Take ở memory.
        /// Có thể tối ưu bằng ROWNUM/ROW_NUMBER trong SQL nếu dữ liệu lớn.
        /// </summary>
        public async Task<PaginatedResponse<Student>> GetStudentsAsync(int pageNumber = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                // Đơn giản hóa: lấy tất cả trước, sau đó phân trang
                string sql;
                object? parameters = null;

                if (string.IsNullOrEmpty(search))
                {
                    sql = @"SELECT s.* FROM QLTT_ADMIN.STUDENTS s 
                            INNER JOIN QLTT_ADMIN.ACCOUNTS a ON a.USER_ID = s.STUDENT_ID
                            WHERE a.IS_ACTIVE = 1
                            ORDER BY s.STUDENT_ID";
                }
                else
                {
                    sql = @"SELECT s.* FROM QLTT_ADMIN.STUDENTS s 
                            INNER JOIN QLTT_ADMIN.ACCOUNTS a ON a.USER_ID = s.STUDENT_ID
                            WHERE a.IS_ACTIVE = 1
                              AND (UPPER(s.FULL_NAME) LIKE UPPER(:search) OR UPPER(s.STUDENT_CODE) LIKE UPPER(:search))
                            ORDER BY s.STUDENT_ID";
                    parameters = new { search = $"%{search}%" };
                }

                var allStudents = await ExecuteQueryAsync<Student>(sql, parameters);
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

        /// <summary>
        /// Lấy chi tiết một học viên theo ID (chỉ lấy nếu tài khoản active).
        /// </summary>
        public async Task<Student?> GetStudentByIdAsync(int id)
        {
            // Thử lấy từ bảng chuẩn mới STUDENTS
            var sql = @"SELECT s.* FROM QLTT_ADMIN.STUDENTS s
                        INNER JOIN QLTT_ADMIN.ACCOUNTS a ON a.USER_ID = s.STUDENT_ID
                        WHERE s.STUDENT_ID = :id AND a.IS_ACTIVE = 1";
            var student = await ExecuteQuerySingleAsync<Student>(sql, new { id });
            if (student != null) return student;

            // Fallback: lấy từ bảng legacy HOC_VIEN (nếu hệ thống cũ chưa migrate hoàn toàn)
            try
            {
                var legacySql = @"SELECT hv.ID_HOC_VIEN, hv.HO_TEN, hv.MA_HOC_VIEN, hv.GIOI_TINH, hv.NGAY_SINH, hv.SO_DIEN_THOAI, hv.DIA_CHI
                                   FROM HOC_VIEN hv
                                   JOIN TAI_KHOAN tk ON tk.ID_NGUOI_DUNG = hv.ID_HOC_VIEN AND tk.IS_ACTIVE = 1
                                   WHERE hv.ID_HOC_VIEN = :id";
                using var conn = await GetConnectionAsync();
                using var cmd = new OracleCommand(legacySql, conn) { BindByName = true };
                cmd.Parameters.Add(":id", OracleDbType.Int32).Value = id;
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new Student
                    {
                        StudentId = reader.GetInt32(0),
                        FullName = reader.IsDBNull(1) ? null : reader.GetString(1),
                        StudentCode = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Sex = reader.IsDBNull(3) ? null : reader.GetString(3),
                        DateOfBirth = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                        PhoneNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Address = reader.IsDBNull(6) ? null : reader.GetString(6)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fallback HOC_VIEN load failed for student {StudentId}", id);
            }
            return null;
        }

        /// <summary>
        /// Tạo học viên mới kèm tạo tài khoản. Gồm các bước:
        /// 1) Kiểm tra trùng username/email
        /// 2) Lấy ROLE_ID cho STUDENT
        /// 3) INSERT ACCOUNT (lấy USER_ID)
        /// 4) INSERT STUDENT (STUDENT_ID = USER_ID), trigger sẽ sinh STUDENT_CODE nếu có.
        /// 5) Commit hoặc rollback nếu lỗi.
        /// </summary>
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

                using var connection = await GetConnectionAsync();
                using var transaction = connection.BeginTransaction();
                try
                {
                    // 1. Kiểm tra trùng USERNAME / EMAIL
                    using (var checkUserCmd = new OracleCommand("SELECT COUNT(*) FROM QLTT_ADMIN.ACCOUNTS WHERE USERNAME = :u", connection))
                    {
                        checkUserCmd.Transaction = transaction;
                        checkUserCmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = dto.Username.Trim();
                        var exists = Convert.ToInt32(await checkUserCmd.ExecuteScalarAsync());
                        if (exists > 0)
                        {
                            return new ApiResponse<Student> { Success = false, Message = "Tên đăng nhập đã tồn tại" };
                        }
                    }
                    using (var checkEmailCmd = new OracleCommand("SELECT COUNT(*) FROM QLTT_ADMIN.ACCOUNTS WHERE EMAIL = :e", connection))
                    {
                        checkEmailCmd.Transaction = transaction;
                        checkEmailCmd.Parameters.Add(":e", OracleDbType.Varchar2).Value = dto.Email.Trim();
                        var exists = Convert.ToInt32(await checkEmailCmd.ExecuteScalarAsync());
                        if (exists > 0)
                        {
                            return new ApiResponse<Student> { Success = false, Message = "Email đã được sử dụng" };
                        }
                    }

                    // 2. Lấy ROLE_ID của STUDENT (fallback 1)
                    int roleId = 1;
                    try
                    {
                        using var roleCmd = new OracleCommand("SELECT ROLE_ID FROM QLTT_ADMIN.ROLES WHERE UPPER(ROLE_NAME) IN ('STUDENT','HỌC VIÊN','HOC VIEN') FETCH FIRST 1 ROWS ONLY", connection);
                        roleCmd.Transaction = transaction;
                        var roleObj = await roleCmd.ExecuteScalarAsync();
                        if (roleObj != null && int.TryParse(roleObj.ToString(), out var rid) && rid > 0)
                            roleId = rid;
                    }
                    catch { /* ignore, fallback 1 */ }

                    // 3. Insert ACCOUNT (identity) + lấy USER_ID
                    int newUserId = 0;
                    using (var accCmd = new OracleCommand(@"INSERT INTO QLTT_ADMIN.ACCOUNTS (USERNAME,PASSWORD,EMAIL,ROLE_ID,IS_ACTIVE)
                                                             VALUES (:username,:password,:email,:roleId,1)
                                                             RETURNING USER_ID INTO :p_user_id", connection))
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
                        return new ApiResponse<Student> { Success = false, Message = "Không lấy được USER_ID sau khi tạo tài khoản" };
                    }

                    // 4. Insert STUDENT (STUDENT_ID = USER_ID) - bỏ STUDENT_CODE để trigger tự sinh
                    using (var stuCmd = new OracleCommand(@"INSERT INTO QLTT_ADMIN.STUDENTS (STUDENT_ID,FULL_NAME,SEX,DATE_OF_BIRTH,PHONE_NUMBER,ADDRESS)
                                                            VALUES (:id,:fullName,:sex,:dob,:phone,:addr)", connection))
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

                    // 6. Lấy lại student vừa tạo (bao gồm STUDENT_CODE do trigger sinh)
                    Student? newStudent;
                    using (var fetchCmd = new OracleCommand("SELECT * FROM QLTT_ADMIN.STUDENTS WHERE STUDENT_ID = :sid", connection))
                    {
                        fetchCmd.Parameters.Add(":sid", OracleDbType.Int32).Value = newUserId;
                        using var reader = await fetchCmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            newStudent = new Student
                            {
                                StudentId = newUserId,
                                FullName = reader.IsDBNull(reader.GetOrdinal("FULL_NAME")) ? null : reader.GetString(reader.GetOrdinal("FULL_NAME")),
                                StudentCode = reader.IsDBNull(reader.GetOrdinal("STUDENT_CODE")) ? null : reader.GetString(reader.GetOrdinal("STUDENT_CODE")),
                                Sex = reader.IsDBNull(reader.GetOrdinal("SEX")) ? null : reader.GetString(reader.GetOrdinal("SEX")),
                                DateOfBirth = reader.IsDBNull(reader.GetOrdinal("DATE_OF_BIRTH")) ? null : reader.GetDateTime(reader.GetOrdinal("DATE_OF_BIRTH")),
                                PhoneNumber = reader.IsDBNull(reader.GetOrdinal("PHONE_NUMBER")) ? null : reader.GetString(reader.GetOrdinal("PHONE_NUMBER")),
                                Address = reader.IsDBNull(reader.GetOrdinal("ADDRESS")) ? null : reader.GetString(reader.GetOrdinal("ADDRESS"))
                            };
                        }
                        else
                        {
                            newStudent = null;
                        }
                    }

                    // 7. Đồng bộ sang bảng legacy HOC_VIEN nếu chưa có
                    if (newStudent != null)
                    {
                        try
                        {
                            using var checkHV = new OracleCommand("SELECT COUNT(*) FROM HOC_VIEN WHERE ID_HOC_VIEN = :id", connection);
                            checkHV.Parameters.Add(":id", OracleDbType.Int32).Value = newStudent.StudentId;
                            var existHV = Convert.ToInt32(await checkHV.ExecuteScalarAsync());
                            if (existHV == 0)
                            {
                                using var insHV = new OracleCommand(@"INSERT INTO HOC_VIEN (ID_HOC_VIEN, HO_TEN, MA_HOC_VIEN, GIOI_TINH, NGAY_SINH, SO_DIEN_THOAI, DIA_CHI)
                                                                     VALUES (:id, :hoten, :ma, :sex, :dob, :phone, :addr)", connection);
                                insHV.Parameters.Add(":id", OracleDbType.Int32).Value = newStudent.StudentId;
                                insHV.Parameters.Add(":hoten", OracleDbType.NVarchar2).Value = (object?)newStudent.FullName ?? DBNull.Value;
                                insHV.Parameters.Add(":ma", OracleDbType.Varchar2).Value = (object?)newStudent.StudentCode ?? $"STU_{newStudent.StudentId}";
                                insHV.Parameters.Add(":sex", OracleDbType.NVarchar2).Value = (object?)newStudent.Sex ?? DBNull.Value;
                                insHV.Parameters.Add(":dob", OracleDbType.Date).Value = (object?)newStudent.DateOfBirth ?? DBNull.Value;
                                insHV.Parameters.Add(":phone", OracleDbType.Varchar2).Value = (object?)newStudent.PhoneNumber ?? DBNull.Value;
                                insHV.Parameters.Add(":addr", OracleDbType.NVarchar2).Value = (object?)newStudent.Address ?? DBNull.Value;
                                await insHV.ExecuteNonQueryAsync();
                                _logger.LogInformation("Synced new student {StudentId} into HOC_VIEN", newStudent.StudentId);
                            }
                        }
                        catch (Exception syncEx)
                        {
                            _logger.LogWarning(syncEx, "Cannot sync student {StudentId} to HOC_VIEN", newStudent.StudentId);
                        }
                    }

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
                    SELECT COUNT(*) FROM QLTT_ADMIN.STUDENTS 
                    WHERE STUDENT_CODE = :studentcode AND STUDENT_ID != :studentid";
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
                    UPDATE QLTT_ADMIN.STUDENTS SET
                        FULL_NAME = :fullname,
                        STUDENT_CODE = :studentcode,
                        SEX = :sex,
                        DATE_OF_BIRTH = :dateofbirth,
                        PHONE_NUMBER = :phonenumber,
                        ADDRESS = :address
                    WHERE STUDENT_ID = :studentid";

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

        /// <summary>
        /// Vô hiệu hóa học viên bằng cách đặt IS_ACTIVE = 0 trên ACCOUNTS thay vì xóa dữ liệu STUDENTS (soft delete).
        /// </summary>
        public async Task<ApiResponse<bool>> DeleteStudentAsync(int id)
        {
            try
            {
                // Kiểm tra học viên có đăng ký học không
                // Soft delete: đặt IS_ACTIVE = 0 cho ACCOUNT tương ứng (không xóa dữ liệu STUDENTS)
                using var connection = await GetConnectionAsync();
                using var command = new OracleCommand("BEGIN UPDATE QLTT_ADMIN.ACCOUNTS SET IS_ACTIVE = 0 WHERE USER_ID = :id; :rowcount := SQL%ROWCOUNT; END;", connection);
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

        /// <summary>
        /// Tìm kiếm nhanh học viên theo FULL_NAME hoặc STUDENT_CODE (chỉ các tài khoản active).
        /// </summary>
        public async Task<List<Student>> SearchStudentsAsync(string keyword)
        {
            var sql = @"SELECT s.* FROM QLTT_ADMIN.STUDENTS s
                        INNER JOIN QLTT_ADMIN.ACCOUNTS a ON a.USER_ID = s.STUDENT_ID
                        WHERE a.IS_ACTIVE = 1
                          AND (UPPER(s.FULL_NAME) LIKE UPPER(:keyword) OR UPPER(s.STUDENT_CODE) LIKE UPPER(:keyword))
                        ORDER BY s.FULL_NAME";
            return await ExecuteQueryAsync<Student>(sql, new { keyword = $"%{keyword}%" });
        }

        /// <summary>
        /// Đồng bộ một học viên vào bảng legacy HOC_VIEN nếu thiếu.
        /// </summary>
        public async Task<bool> SyncLegacyHocVienAsync(int studentId)
        {
            try
            {
                var stu = await GetStudentByIdAsync(studentId);
                if (stu == null) return false;
                using var conn = await GetConnectionAsync();
                using var chk = new OracleCommand("SELECT COUNT(*) FROM HOC_VIEN WHERE ID_HOC_VIEN = :id", conn) { BindByName = true };
                chk.Parameters.Add(":id", OracleDbType.Int32).Value = studentId;
                var cnt = Convert.ToInt32(await chk.ExecuteScalarAsync());
                if (cnt > 0) return true; // đã có
                using var ins = new OracleCommand(@"INSERT INTO HOC_VIEN (ID_HOC_VIEN, HO_TEN, MA_HOC_VIEN, GIOI_TINH, NGAY_SINH, SO_DIEN_THOAI, DIA_CHI)
                                                   VALUES (:id,:hoten,:ma,:sex,:dob,:phone,:addr)", conn) { BindByName = true };
                ins.Parameters.Add(":id", OracleDbType.Int32).Value = stu.StudentId;
                ins.Parameters.Add(":hoten", OracleDbType.NVarchar2).Value = (object?)stu.FullName ?? DBNull.Value;
                ins.Parameters.Add(":ma", OracleDbType.Varchar2).Value = (object?)stu.StudentCode ?? $"STU_{stu.StudentId}";
                ins.Parameters.Add(":sex", OracleDbType.NVarchar2).Value = (object?)stu.Sex ?? DBNull.Value;
                ins.Parameters.Add(":dob", OracleDbType.Date).Value = (object?)stu.DateOfBirth ?? DBNull.Value;
                ins.Parameters.Add(":phone", OracleDbType.Varchar2).Value = (object?)stu.PhoneNumber ?? DBNull.Value;
                ins.Parameters.Add(":addr", OracleDbType.NVarchar2).Value = (object?)stu.Address ?? DBNull.Value;
                await ins.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SyncLegacyHocVienAsync failed for {StudentId}", studentId);
                return false;
            }
        }
    }
}