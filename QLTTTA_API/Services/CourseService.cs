using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;

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
                "WHERE UPPER(TEN_KHOA_HOC) LIKE UPPER(:search) OR UPPER(MA_KHOA_HOC) LIKE UPPER(:search)";

            var countSql = $@"
                SELECT COUNT(*) 
                FROM KHOA_HOC 
                {whereClause}";

            var dataSql = $@"
                SELECT * FROM (
                    SELECT c.*, ROW_NUMBER() OVER (ORDER BY ID_KHOA_HOC) as rn
                    FROM KHOA_HOC c
                    {whereClause}
                ) WHERE rn BETWEEN :offset + 1 AND :offset + :pagesize";

            var parameters = new { search = $"%{search}%", offset, pagesize = pageSize };

            var totalRecords = Convert.ToInt32(await ExecuteScalarAdminAsync(countSql,
                string.IsNullOrEmpty(search) ? null : new { search = $"%{search}%" }));

            var courses = await ExecuteQueryAdminAsync<Course>(dataSql,
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
            var sql = "SELECT * FROM KHOA_HOC WHERE ID_KHOA_HOC = :id";
            return await ExecuteQuerySingleAdminAsync<Course>(sql, new { id });
        }

        public async Task<ApiResponse<Course>> CreateCourseAsync(CourseCreateDto dto)
        {
            try
            {
                // Kiểm tra mã khóa học đã tồn tại
                var existingSql = "SELECT COUNT(*) FROM KHOA_HOC WHERE MA_KHOA_HOC = :coursecode";
                var exists = Convert.ToInt32(await ExecuteScalarAdminAsync(existingSql, new { coursecode = dto.CourseCode }));

                if (exists > 0)
                {
                    return new ApiResponse<Course>
                    {
                        Success = false,
                        Message = "Mã khóa học đã tồn tại"
                    };
                }

                var sql = @"
                    INSERT INTO KHOA_HOC 
                    (MA_KHOA_HOC, TEN_KHOA_HOC, MO_TA, HOC_PHI_TIEU_CHUAN)
                    VALUES (:coursecode, :coursename, :description, :standardfee)";

                var parameters = new
                {
                    coursecode = dto.CourseCode,
                    coursename = dto.CourseName,
                    description = dto.Description,
                    standardfee = dto.StandardFee
                };

                await ExecuteNonQueryAdminAsync(sql, parameters);

                // Lấy thông tin khóa học vừa tạo
                var newCourse = await ExecuteQuerySingleAdminAsync<Course>(
                    "SELECT * FROM KHOA_HOC WHERE MA_KHOA_HOC = :coursecode",
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
                    SELECT COUNT(*) FROM KHOA_HOC 
                    WHERE MA_KHOA_HOC = :coursecode AND ID_KHOA_HOC != :courseid";
                var exists = Convert.ToInt32(await ExecuteScalarAdminAsync(existingSql,
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
                    UPDATE KHOA_HOC SET
                        MA_KHOA_HOC = :coursecode,
                        TEN_KHOA_HOC = :coursename,
                        MO_TA = :description,
                        HOC_PHI_TIEU_CHUAN = :standardfee
                    WHERE ID_KHOA_HOC = :courseid";

                var parameters = new
                {
                    coursecode = dto.CourseCode,
                    coursename = dto.CourseName,
                    description = dto.Description,
                    standardfee = dto.StandardFee,
                    courseid = dto.CourseId
                };

                await ExecuteNonQueryAdminAsync(sql, parameters);

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
                var classSql = "SELECT COUNT(*) FROM LOP_HOC WHERE ID_KHOA_HOC = :id";
                var hasClasses = Convert.ToInt32(await ExecuteScalarAdminAsync(classSql, new { id }));

                if (hasClasses > 0)
                {
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Không thể xóa khóa học đã có lớp học"
                    };
                }

                var sql = "DELETE FROM KHOA_HOC WHERE ID_KHOA_HOC = :id";
                var rowsAffected = await ExecuteNonQueryAdminAsync(sql, new { id });

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
            var sql = "SELECT * FROM KHOA_HOC ORDER BY TEN_KHOA_HOC";
            return await ExecuteQueryAdminAsync<Course>(sql);
        }
    }
}