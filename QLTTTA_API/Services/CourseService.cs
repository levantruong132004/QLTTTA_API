using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace QLTTTA_API.Services
{
    /// <summary>
    /// Interface dịch vụ quản lý khóa học (CRUD + danh sách phân trang).
    /// </summary>
    public interface ICourseService
    {
        /// <summary>Lấy danh sách khóa học phân trang kèm tìm kiếm tên/mã.</summary>
        Task<PaginatedResponse<Course>> GetCoursesAsync(int pageNumber = 1, int pageSize = 10, string? search = null);
        /// <summary>Lấy chi tiết một khóa học theo ID.</summary>
        Task<Course?> GetCourseByIdAsync(int id);
        /// <summary>Tạo khóa học mới.</summary>
        Task<ApiResponse<Course>> CreateCourseAsync(CourseCreateDto dto);
        /// <summary>Cập nhật khóa học.</summary>
        Task<ApiResponse<Course>> UpdateCourseAsync(CourseUpdateDto dto);
        /// <summary>Xóa khóa học nếu không ràng buộc lớp học.</summary>
        Task<ApiResponse<bool>> DeleteCourseAsync(int id);
        /// <summary>Lấy tất cả khóa học (không phân trang) - dùng cho hiển thị tổng quan.</summary>
        Task<List<Course>> GetAllCoursesAsync();
    }

    /// <summary>
    /// Triển khai ICourseService dùng BaseService (per-user connection nếu có) để truy vấn dữ liệu khóa học.
    /// </summary>
    public class CourseService : BaseService, ICourseService
    {
        public CourseService(IConfiguration configuration, ILogger<CourseService> logger, IOracleConnectionProvider userConnProvider)
            : base(configuration, logger, userConnProvider) { }

        /// <summary>
        /// Lấy danh sách khóa học phân trang. Có hỗ trợ tìm kiếm theo COURSE_NAME hoặc COURSE_CODE (không phân biệt hoa thường).
        /// </summary>
        public async Task<PaginatedResponse<Course>> GetCoursesAsync(int pageNumber = 1, int pageSize = 10, string? search = null)
        {
            var offset = (pageNumber - 1) * pageSize;
            var whereClause = string.IsNullOrEmpty(search) ? "" :
                "WHERE UPPER(COURSE_NAME) LIKE UPPER(:search) OR UPPER(COURSE_CODE) LIKE UPPER(:search)";

            var countSql = $@"
                SELECT COUNT(*) 
                FROM KHOA_HOC 
                {whereClause.Replace("COURSE_NAME", "TEN_KHOA_HOC").Replace("COURSE_CODE", "MA_KHOA_HOC")}";

            var dataSql = $@"
                SELECT * FROM (
                    SELECT 
                        c.ID_KHOA_HOC       AS COURSE_ID,
                        c.MA_KHOA_HOC       AS COURSE_CODE,
                        c.TEN_KHOA_HOC      AS COURSE_NAME,
                        c.MO_TA             AS DESCRIPTION,
                        c.HOC_PHI_TIEU_CHUAN AS STANDARD_FEE,
                        ROW_NUMBER() OVER (ORDER BY c.ID_KHOA_HOC) as rn
                    FROM KHOA_HOC c
                    {whereClause.Replace("COURSE_NAME", "TEN_KHOA_HOC").Replace("COURSE_CODE", "MA_KHOA_HOC")}
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

        /// <summary>
        /// Lấy thông tin một khóa học theo ID (không có xử lý đặc biệt).
        /// </summary>
        public async Task<Course?> GetCourseByIdAsync(int id)
        {
            var sql = @"SELECT 
                                     ID_KHOA_HOC       AS COURSE_ID,
                                     MA_KHOA_HOC       AS COURSE_CODE,
                                     TEN_KHOA_HOC      AS COURSE_NAME,
                                     MO_TA             AS DESCRIPTION,
                                     HOC_PHI_TIEU_CHUAN AS STANDARD_FEE
                                 FROM KHOA_HOC WHERE ID_KHOA_HOC = :id";
            return await ExecuteQuerySingleAsync<Course>(sql, new { id });
        }

        /// <summary>
        /// Tạo khóa học mới: kiểm tra mã khóa học trùng trước, sau khi INSERT truy vấn lại bản ghi vừa tạo.
        /// </summary>
        public async Task<ApiResponse<Course>> CreateCourseAsync(CourseCreateDto dto)
        {
            try
            {
                // Kiểm tra mã khóa học đã tồn tại
                var existingSql = "SELECT COUNT(*) FROM KHOA_HOC WHERE MA_KHOA_HOC = :coursecode";
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

                await ExecuteNonQueryAsync(sql, parameters);

                // Lấy thông tin khóa học vừa tạo
                var newCourse = await ExecuteQuerySingleAsync<Course>(
                        @"SELECT ID_KHOA_HOC AS COURSE_ID, MA_KHOA_HOC AS COURSE_CODE, TEN_KHOA_HOC AS COURSE_NAME, MO_TA AS DESCRIPTION, HOC_PHI_TIEU_CHUAN AS STANDARD_FEE 
                                            FROM KHOA_HOC WHERE MA_KHOA_HOC = :coursecode",
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

        /// <summary>
        /// Cập nhật khóa học: kiểm tra tồn tại, kiểm tra trùng mã (trừ chính nó) rồi thực hiện UPDATE.
        /// </summary>
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

        /// <summary>
        /// Xóa khóa học: chỉ cho phép nếu chưa có bản ghi lớp học (CLASSES) tham chiếu.
        /// </summary>
        public async Task<ApiResponse<bool>> DeleteCourseAsync(int id)
        {
            try
            {
                // Kiểm tra khóa học có lớp học không
                var classSql = "SELECT COUNT(*) FROM LOP_HOC WHERE ID_KHOA_HOC = :id";
                var hasClasses = Convert.ToInt32(await ExecuteScalarAsync(classSql, new { id }));

                if (hasClasses > 0)
                {
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Không thể xóa khóa học đã có lớp học"
                    };
                }

                var sql = "DELETE FROM KHOA_HOC WHERE ID_KHOA_HOC = :id";
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

        /// <summary>
        /// Lấy danh sách tất cả khóa học (có thể dùng cho hiển thị tổng quan). Fallback admin nếu phiên user hết hạn.
        /// </summary>
        public async Task<List<Course>> GetAllCoursesAsync()
        {
            var sql = @"SELECT 
                            ID_KHOA_HOC       AS COURSE_ID,
                            MA_KHOA_HOC       AS COURSE_CODE,
                            TEN_KHOA_HOC      AS COURSE_NAME,
                            MO_TA             AS DESCRIPTION,
                            HOC_PHI_TIEU_CHUAN AS STANDARD_FEE
                        FROM KHOA_HOC ORDER BY TEN_KHOA_HOC";
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
                        CourseCode = reader.IsDBNull(reader.GetOrdinal("COURSE_CODE")) ? null : reader.GetString(reader.GetOrdinal("COURSE_CODE")),
                        CourseName = reader.IsDBNull(reader.GetOrdinal("COURSE_NAME")) ? null : reader.GetString(reader.GetOrdinal("COURSE_NAME")),
                        Description = reader.IsDBNull(reader.GetOrdinal("DESCRIPTION")) ? string.Empty : reader.GetString(reader.GetOrdinal("DESCRIPTION")),
                        StandardFee = reader.IsDBNull(reader.GetOrdinal("STANDARD_FEE")) ? 0 : Convert.ToInt32(Math.Round(Convert.ToDecimal(reader["STANDARD_FEE"])))
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
                        CourseCode = reader.IsDBNull(reader.GetOrdinal("COURSE_CODE")) ? null : reader.GetString(reader.GetOrdinal("COURSE_CODE")),
                        CourseName = reader.IsDBNull(reader.GetOrdinal("COURSE_NAME")) ? null : reader.GetString(reader.GetOrdinal("COURSE_NAME")),
                        Description = reader.IsDBNull(reader.GetOrdinal("DESCRIPTION")) ? string.Empty : reader.GetString(reader.GetOrdinal("DESCRIPTION")),
                        StandardFee = reader.IsDBNull(reader.GetOrdinal("STANDARD_FEE")) ? 0 : Convert.ToInt32(Math.Round(Convert.ToDecimal(reader["STANDARD_FEE"])))
                    });
                }
                return list;
            }
        }
    }
}