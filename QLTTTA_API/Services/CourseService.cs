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
            try
            {
                using var connection = await GetConnectionAsync();
                using var command = new OracleCommand("SP_GET_COURSES", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("p_page_number", OracleDbType.Int32).Value = pageNumber;
                command.Parameters.Add("p_page_size", OracleDbType.Int32).Value = pageSize;
                command.Parameters.Add("p_search", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(search) ? DBNull.Value : $"%{search}%";
                
                command.Parameters.Add("p_cursor", OracleDbType.RefCursor).Direction = ParameterDirection.Output;
                var pTotal = command.Parameters.Add("p_total", OracleDbType.Int32);
                pTotal.Direction = ParameterDirection.Output;

                var courses = new List<Course>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        courses.Add(MapToObject<Course>(reader));
                    }
                }

                int totalRecords = 0;
                if (pTotal.Value != null && int.TryParse(pTotal.Value.ToString(), out var t)) totalRecords = t;

                return new PaginatedResponse<Course>
                {
                    Data = courses,
                    TotalRecords = totalRecords,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCoursesAsync");
                return new PaginatedResponse<Course> { Data = new List<Course>(), TotalRecords = 0, PageNumber = pageNumber, PageSize = pageSize };
            }
        }

        public async Task<Course?> GetCourseByIdAsync(int id)
        {
            try
            {
                var list = await ExecuteStoredProcedureQueryAsync<Course>("SP_GET_COURSE_BY_ID", new { p_id = id });
                return list.FirstOrDefault();
            }
            catch (UnauthorizedAccessException)
            {
                // Fallback to Admin connection for public access (e.g. QR scan without login)
                var results = new List<Course>();
                using var connection = await GetAdminConnectionAsync();
                using var command = new OracleCommand("SP_GET_COURSE_BY_ID", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
                
                // Manually add cursor if not added by helper (helper adds it if missing, here we do it manually)
                command.Parameters.Add("p_cursor", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapToObject<Course>(reader));
                }
                return results.FirstOrDefault();
            }
        }

        public async Task<ApiResponse<Course>> CreateCourseAsync(CourseCreateDto dto)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                using var cmdCheck = new OracleCommand("SP_CHECK_COURSE_CODE_EXISTS", conn);
                cmdCheck.CommandType = CommandType.StoredProcedure;
                cmdCheck.Parameters.Add("p_code", OracleDbType.Varchar2).Value = dto.CourseCode;
                cmdCheck.Parameters.Add("p_exclude_id", OracleDbType.Int32).Value = DBNull.Value;
                var pCount = cmdCheck.Parameters.Add("p_count", OracleDbType.Int32);
                pCount.Direction = ParameterDirection.Output;
                await cmdCheck.ExecuteNonQueryAsync();
                
                if (Convert.ToInt32(pCount.Value.ToString()) > 0)
                {
                    return new ApiResponse<Course> { Success = false, Message = "Mã khóa học đã tồn tại" };
                }

                var list = await ExecuteStoredProcedureQueryAsync<Course>("SP_CREATE_COURSE", new { 
                    p_code = dto.CourseCode,
                    p_name = dto.CourseName,
                    p_desc = dto.Description,
                    p_fee = dto.StandardFee
                });
                
                return new ApiResponse<Course> { Success = true, Message = "Tạo khóa học thành công", Data = list.FirstOrDefault() };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating course");
                return new ApiResponse<Course> { Success = false, Message = "Có lỗi xảy ra khi tạo khóa học" };
            }
        }

        public async Task<ApiResponse<Course>> UpdateCourseAsync(CourseUpdateDto dto)
        {
            try
            {
                var course = await GetCourseByIdAsync(dto.CourseId);
                if (course == null) return new ApiResponse<Course> { Success = false, Message = "Không tìm thấy khóa học" };

                using var conn = await GetConnectionAsync();
                using var cmdCheck = new OracleCommand("SP_CHECK_COURSE_CODE_EXISTS", conn);
                cmdCheck.CommandType = CommandType.StoredProcedure;
                cmdCheck.Parameters.Add("p_code", OracleDbType.Varchar2).Value = dto.CourseCode;
                cmdCheck.Parameters.Add("p_exclude_id", OracleDbType.Int32).Value = dto.CourseId;
                var pCount = cmdCheck.Parameters.Add("p_count", OracleDbType.Int32);
                pCount.Direction = ParameterDirection.Output;
                await cmdCheck.ExecuteNonQueryAsync();
                
                if (Convert.ToInt32(pCount.Value.ToString()) > 0)
                {
                    return new ApiResponse<Course> { Success = false, Message = "Mã khóa học đã tồn tại" };
                }

                using var cmdUp = new OracleCommand("SP_UPDATE_COURSE", conn);
                cmdUp.CommandType = CommandType.StoredProcedure;
                cmdUp.Parameters.Add("p_id", OracleDbType.Int32).Value = dto.CourseId;
                cmdUp.Parameters.Add("p_code", OracleDbType.Varchar2).Value = dto.CourseCode;
                cmdUp.Parameters.Add("p_name", OracleDbType.NVarchar2).Value = dto.CourseName;
                cmdUp.Parameters.Add("p_desc", OracleDbType.NVarchar2).Value = dto.Description;
                cmdUp.Parameters.Add("p_fee", OracleDbType.Decimal).Value = dto.StandardFee;
                await cmdUp.ExecuteNonQueryAsync();

                var updated = await GetCourseByIdAsync(dto.CourseId);
                return new ApiResponse<Course> { Success = true, Message = "Cập nhật khóa học thành công", Data = updated };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating course");
                return new ApiResponse<Course> { Success = false, Message = "Có lỗi xảy ra khi cập nhật khóa học" };
            }
        }

        public async Task<ApiResponse<bool>> DeleteCourseAsync(int id)
        {
            using var conn = await GetConnectionAsync();
            using var cmd = new OracleCommand("SP_DELETE_COURSE", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
            var pRow = cmd.Parameters.Add("p_rowcount", OracleDbType.Int32);
            pRow.Direction = ParameterDirection.Output;
            
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (OracleException oex) when (oex.Number == 2292)
            {
                return new ApiResponse<bool> { Success = false, Message = "Không thể xóa khóa học đã có lớp học" };
            }

            int affected = 0;
            if (pRow.Value != null && int.TryParse(pRow.Value.ToString(), out var a)) affected = a;
            
            if (affected == 0) return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy khóa học" };
            
            return new ApiResponse<bool> { Success = true, Message = "Xóa khóa học thành công", Data = true };
        }

        public async Task<List<Course>> GetAllCoursesAsync()
        {
            return await ExecuteStoredProcedureQueryAsync<Course>("SP_GET_ALL_COURSES");
        }
    }
}