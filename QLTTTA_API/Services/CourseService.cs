using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace QLTTTA_API.Services
{
    public interface ICourseService
    {
        Task<PaginatedResponse<Course>> GetCoursesAsync(int pageNumber = 1, int pageSize = 10, string? search = null);
        Task<Course?> GetCourseByIdAsync(int id);
        Task<ApiResponse<Course>> CreateCourseAsync(CourseCreateDto dto);
        Task<ApiResponse<Course>> UpdateCourseAsync(CourseUpdateDto dto);
        Task<ApiResponse<bool>> DeleteCourseAsync(int id);
        Task<List<Course>> GetAllCoursesAsync();
    }

    public class CourseService : BaseService, ICourseService
    {
        public CourseService(IConfiguration configuration, ILogger<CourseService> logger, IOracleConnectionProvider userConnProvider)
            : base(configuration, logger, userConnProvider) { }

        public async Task<PaginatedResponse<Course>> GetCoursesAsync(int pageNumber = 1, int pageSize = 10, string? search = null)
        {
            var offset = (pageNumber - 1) * pageSize;
            var whereClause = string.IsNullOrEmpty(search) ? "" :
                "WHERE UPPER(COURSE_NAME) LIKE UPPER(:search) OR UPPER(COURSE_CODE) LIKE UPPER(:search)";

            var countSql = $@"
                SELECT COUNT(*) 
                FROM QLTT_ADMIN.COURSES 
                {whereClause}";

            var dataSql = $@"
                SELECT * FROM (
                    SELECT c.*, ROW_NUMBER() OVER (ORDER BY COURSE_ID) as rn
                    FROM QLTT_ADMIN.COURSES c
                    {whereClause}
                ) WHERE rn BETWEEN :offset + 1 AND :offset + :pagesize";

            var parameters = new { search = $"%{search}%", offset, pagesize = pageSize };

            var totalRecords = Convert.ToInt32(await ExecuteScalarAsync(countSql,
                string.IsNullOrEmpty(search) ? null : new { search = $"%{search}%" }));

            var courses = await ExecuteQueryAsync<Course>(dataSql,
                string.IsNullOrEmpty(search) ? new { offset, pagesize = pageSize } : parameters);

            return new PaginatedResponse<Course>
            {
                Data = courses,
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<Course?> GetCourseByIdAsync(int id)
        {
            var sql = "SELECT * FROM QLTT_ADMIN.COURSES WHERE COURSE_ID = :id";
            return await ExecuteQuerySingleAsync<Course>(sql, new { id });
        }

        public async Task<ApiResponse<Course>> CreateCourseAsync(CourseCreateDto dto)
        {
            try
            {
                // Kiểm tra mã khóa học đã tồn tại
                var existingSql = "SELECT COUNT(*) FROM QLTT_ADMIN.COURSES WHERE COURSE_CODE = :coursecode";
                var exists = Convert.ToInt32(await ExecuteScalarAsync(existingSql, new { coursecode = dto.CourseCode }));

                if (exists > 0)
                {
                    return new ApiResponse<Course>
                    {
                        Success = false,
                        Message = "Mã khóa học đã tồn tại"
                    };
                }

                var sql = @"
                    INSERT INTO QLTT_ADMIN.COURSES 
                    (COURSE_CODE, COURSE_NAME, DESCRIPTION, STANDARD_FEE)
                    VALUES (:coursecode, :coursename, :description, :standardfee)";

                var parameters = new
                {
                    coursecode = dto.CourseCode,
                    coursename = dto.CourseName,
                    description = dto.Description,
                    standardfee = dto.StandardFee
                };

                await ExecuteNonQueryAsync(sql, parameters);

                // Lấy thông tin khóa học vừa tạo
                var newCourse = await ExecuteQuerySingleAsync<Course>(
                    "SELECT * FROM QLTT_ADMIN.COURSES WHERE COURSE_CODE = :coursecode",
                    new { coursecode = dto.CourseCode });

                return new ApiResponse<Course>
                {
                    Success = true,
                    Message = "Tạo khóa học thành công",
                    Data = newCourse
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating course");
                return new ApiResponse<Course>
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi tạo khóa học"
                };
            }
        }

        public async Task<ApiResponse<Course>> UpdateCourseAsync(CourseUpdateDto dto)
        {
            try
            {
                // Kiểm tra khóa học tồn tại
                var course = await GetCourseByIdAsync(dto.CourseId);
                if (course == null)
                {
                    return new ApiResponse<Course>
                    {
                        Success = false,
                        Message = "Không tìm thấy khóa học"
                    };
                }

                // Kiểm tra mã khóa học trùng (ngoại trừ chính nó)
                var existingSql = @"
                    SELECT COUNT(*) FROM QLTT_ADMIN.COURSES 
                    WHERE COURSE_CODE = :coursecode AND COURSE_ID != :courseid";
                var exists = Convert.ToInt32(await ExecuteScalarAsync(existingSql,
                    new { coursecode = dto.CourseCode, courseid = dto.CourseId }));

                if (exists > 0)
                {
                    return new ApiResponse<Course>
                    {
                        Success = false,
                        Message = "Mã khóa học đã tồn tại"
                    };
                }

                var sql = @"
                    UPDATE QLTT_ADMIN.COURSES SET
                        COURSE_CODE = :coursecode,
                        COURSE_NAME = :coursename,
                        DESCRIPTION = :description,
                        STANDARD_FEE = :standardfee
                    WHERE COURSE_ID = :courseid";

                var parameters = new
                {
                    coursecode = dto.CourseCode,
                    coursename = dto.CourseName,
                    description = dto.Description,
                    standardfee = dto.StandardFee,
                    courseid = dto.CourseId
                };

                await ExecuteNonQueryAsync(sql, parameters);

                var updatedCourse = await GetCourseByIdAsync(dto.CourseId);

                return new ApiResponse<Course>
                {
                    Success = true,
                    Message = "Cập nhật khóa học thành công",
                    Data = updatedCourse
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating course");
                return new ApiResponse<Course>
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi cập nhật khóa học"
                };
            }
        }

        public async Task<ApiResponse<bool>> DeleteCourseAsync(int id)
        {
            try
            {
                // Kiểm tra khóa học có lớp học không
                var classSql = "SELECT COUNT(*) FROM QLTT_ADMIN.CLASSES WHERE COURSE_ID = :id";
                var hasClasses = Convert.ToInt32(await ExecuteScalarAsync(classSql, new { id }));

                if (hasClasses > 0)
                {
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Không thể xóa khóa học đã có lớp học"
                    };
                }

                var sql = "DELETE FROM QLTT_ADMIN.COURSES WHERE COURSE_ID = :id";
                var rowsAffected = await ExecuteNonQueryAsync(sql, new { id });

                if (rowsAffected == 0)
                {
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Không tìm thấy khóa học"
                    };
                }

                return new ApiResponse<bool>
                {
                    Success = true,
                    Message = "Xóa khóa học thành công",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting course");
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi xóa khóa học"
                };
            }
        }

        public async Task<List<Course>> GetAllCoursesAsync()
        {
            var sql = "SELECT * FROM QLTT_ADMIN.COURSES ORDER BY COURSE_NAME";
            // Cố gắng dùng kết nối user trước; nếu phiên user không còn (sau khi server restart), fallback sang admin cho truy vấn công khai này
            try
            {
                using var conn = await GetConnectionAsync();
                using var cmd = new OracleCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.Default);
                var list = new List<Course>();
                while (await reader.ReadAsync())
                {
                    list.Add(new Course
                    {
                        CourseId = reader.GetInt32(reader.GetOrdinal("COURSE_ID")),
                        CourseCode = reader.GetString(reader.GetOrdinal("COURSE_CODE")),
                        CourseName = reader.GetString(reader.GetOrdinal("COURSE_NAME")),
                        Description = reader.IsDBNull(reader.GetOrdinal("DESCRIPTION")) ? string.Empty : reader.GetString(reader.GetOrdinal("DESCRIPTION")),
                        StandardFee = reader.IsDBNull(reader.GetOrdinal("STANDARD_FEE")) ? 0 : Convert.ToInt32(reader["STANDARD_FEE"]) // NUMBER -> int
                    });
                }
                return list;
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("User session missing/invalid. Falling back to admin connection for public course list.");
                using var conn = await GetAdminConnectionAsync();
                using var cmd = new OracleCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.Default);
                var list = new List<Course>();
                while (await reader.ReadAsync())
                {
                    list.Add(new Course
                    {
                        CourseId = reader.GetInt32(reader.GetOrdinal("COURSE_ID")),
                        CourseCode = reader.GetString(reader.GetOrdinal("COURSE_CODE")),
                        CourseName = reader.GetString(reader.GetOrdinal("COURSE_NAME")),
                        Description = reader.IsDBNull(reader.GetOrdinal("DESCRIPTION")) ? string.Empty : reader.GetString(reader.GetOrdinal("DESCRIPTION")),
                        StandardFee = reader.IsDBNull(reader.GetOrdinal("STANDARD_FEE")) ? 0 : Convert.ToInt32(reader["STANDARD_FEE"]) // NUMBER -> int
                    });
                }
                return list;
            }
        }
    }
}