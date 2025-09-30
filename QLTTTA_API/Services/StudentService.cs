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
        public StudentService(IConfiguration configuration, ILogger<StudentService> logger)
            : base(configuration, logger) { }

        public async Task<PaginatedResponse<Student>> GetStudentsAsync(int pageNumber = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                // Đơn giản hóa: lấy tất cả trước, sau đó phân trang
                string sql;
                object? parameters = null;

                if (string.IsNullOrEmpty(search))
                {
                    sql = "SELECT * FROM QLTT_ADMIN.STUDENTS ORDER BY STUDENT_ID";
                }
                else
                {
                    sql = @"SELECT * FROM QLTT_ADMIN.STUDENTS 
                           WHERE UPPER(FULL_NAME) LIKE UPPER(:search) OR UPPER(STUDENT_CODE) LIKE UPPER(:search)
                           ORDER BY STUDENT_ID";
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

        public async Task<Student?> GetStudentByIdAsync(int id)
        {
            var sql = "SELECT * FROM QLTT_ADMIN.STUDENTS WHERE STUDENT_ID = :id";
            return await ExecuteQuerySingleAsync<Student>(sql, new { id });
        }

        public async Task<ApiResponse<Student>> CreateStudentAsync(StudentCreateDto dto)
        {
            try
            {
                // Kiểm tra mã học viên đã tồn tại
                var existingSql = "SELECT COUNT(*) FROM QLTT_ADMIN.STUDENTS WHERE STUDENT_CODE = :studentcode";
                var exists = Convert.ToInt32(await ExecuteScalarAsync(existingSql, new { studentcode = dto.StudentCode }));

                if (exists > 0)
                {
                    return new ApiResponse<Student>
                    {
                        Success = false,
                        Message = "Mã học viên đã tồn tại"
                    };
                }

                var sql = @"
                    INSERT INTO QLTT_ADMIN.STUDENTS 
                    (FULL_NAME, STUDENT_CODE, SEX, DATE_OF_BIRTH, PHONE_NUMBER, ADDRESS)
                    VALUES (:fullname, :studentcode, :sex, :dateofbirth, :phonenumber, :address)";

                var parameters = new
                {
                    fullname = dto.FullName,
                    studentcode = dto.StudentCode,
                    sex = dto.Sex,
                    dateofbirth = dto.DateOfBirth,
                    phonenumber = dto.PhoneNumber,
                    address = dto.Address
                };

                await ExecuteNonQueryAsync(sql, parameters);

                // Lấy thông tin học viên vừa tạo
                var newStudent = await ExecuteQuerySingleAsync<Student>(
                    "SELECT * FROM QLTT_ADMIN.STUDENTS WHERE STUDENT_CODE = :studentcode",
                    new { studentcode = dto.StudentCode });

                return new ApiResponse<Student>
                {
                    Success = true,
                    Message = "Tạo học viên thành công",
                    Data = newStudent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating student");
                return new ApiResponse<Student>
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi tạo học viên"
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

        public async Task<ApiResponse<bool>> DeleteStudentAsync(int id)
        {
            try
            {
                // Kiểm tra học viên có đăng ký học không
                var registrationSql = "SELECT COUNT(*) FROM QLTT_ADMIN.REGISTRATIONS WHERE STUDENT_ID = :id";
                var hasRegistrations = Convert.ToInt32(await ExecuteScalarAsync(registrationSql, new { id }));

                if (hasRegistrations > 0)
                {
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Không thể xóa học viên đã có đăng ký học"
                    };
                }

                var sql = "DELETE FROM QLTT_ADMIN.STUDENTS WHERE STUDENT_ID = :id";
                var rowsAffected = await ExecuteNonQueryAsync(sql, new { id });

                if (rowsAffected == 0)
                {
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Không tìm thấy học viên"
                    };
                }

                return new ApiResponse<bool>
                {
                    Success = true,
                    Message = "Xóa học viên thành công",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting student");
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi xóa học viên"
                };
            }
        }

        public async Task<List<Student>> SearchStudentsAsync(string keyword)
        {
            var sql = @"
                SELECT * FROM QLTT_ADMIN.STUDENTS 
                WHERE UPPER(FULL_NAME) LIKE UPPER(:keyword) 
                   OR UPPER(STUDENT_CODE) LIKE UPPER(:keyword)
                ORDER BY FULL_NAME";

            return await ExecuteQueryAsync<Student>(sql, new { keyword = $"%{keyword}%" });
        }
    }
}