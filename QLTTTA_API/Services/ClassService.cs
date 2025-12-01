using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;
using System.Data;

namespace QLTTTA_API.Services
{
    public interface IClassService
    {
        Task<List<Class>> GetClassesAsync(int? courseId = null, string? search = null);
        Task<Class?> GetClassByIdAsync(int id);
        Task<ApiResponse<Class>> CreateClassAsync(ClassCreateDto dto);
        Task<ApiResponse<Class>> UpdateClassAsync(ClassUpdateDto dto);
        Task<ApiResponse<bool>> DeleteClassAsync(int id);
        Task<List<Student>> GetRosterAsync(int classId);
    }

    public class ClassService : BaseService, IClassService
    {
        public ClassService(IConfiguration configuration, ILogger<ClassService> logger, IOracleConnectionProvider userConnProvider)
            : base(configuration, logger, userConnProvider) { }

        public async Task<List<Class>> GetClassesAsync(int? courseId = null, string? search = null)
        {
            return await ExecuteStoredProcedureQueryAsync<Class>("SP_GET_CLASSES", new { 
                p_course_id = courseId, 
                p_search = string.IsNullOrEmpty(search) ? null : $"%{search}%" 
            });
        }

        public async Task<Class?> GetClassByIdAsync(int id)
        {
            var list = await ExecuteStoredProcedureQueryAsync<Class>("SP_GET_CLASS_BY_ID", new { p_id = id });
            return list.FirstOrDefault();
        }

        public async Task<ApiResponse<Class>> CreateClassAsync(ClassCreateDto dto)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                using var cmdCheck = new OracleCommand("SP_CHECK_CLASS_CODE_EXISTS", conn);
                cmdCheck.CommandType = CommandType.StoredProcedure;
                cmdCheck.Parameters.Add("p_code", OracleDbType.Varchar2).Value = dto.ClassCode;
                cmdCheck.Parameters.Add("p_exclude_id", OracleDbType.Int32).Value = DBNull.Value;
                var pCount = cmdCheck.Parameters.Add("p_count", OracleDbType.Int32);
                pCount.Direction = ParameterDirection.Output;
                await cmdCheck.ExecuteNonQueryAsync();
                
                if (Convert.ToInt32(pCount.Value.ToString()) > 0)
                {
                    return new ApiResponse<Class> { Success = false, Message = "Mã lớp đã tồn tại" };
                }

                var list = await ExecuteStoredProcedureQueryAsync<Class>("SP_CREATE_CLASS", new { 
                    p_code = dto.ClassCode,
                    p_name = dto.ClassName,
                    p_start = dto.StartDate,
                    p_end = dto.EndDate,
                    p_max = dto.MaxSize,
                    p_course_id = dto.CourseId,
                    p_teacher_id = dto.TeacherId
                });
                
                return new ApiResponse<Class> { Success = true, Message = "Tạo lớp học thành công", Data = list.FirstOrDefault() };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating class");
                return new ApiResponse<Class> { Success = false, Message = "Có lỗi xảy ra khi tạo lớp học" };
            }
        }

        public async Task<ApiResponse<Class>> UpdateClassAsync(ClassUpdateDto dto)
        {
            try
            {
                var entity = await GetClassByIdAsync(dto.ClassId);
                if (entity == null) return new ApiResponse<Class> { Success = false, Message = "Không tìm thấy lớp" };

                using var conn = await GetConnectionAsync();
                using var cmdCheck = new OracleCommand("SP_CHECK_CLASS_CODE_EXISTS", conn);
                cmdCheck.CommandType = CommandType.StoredProcedure;
                cmdCheck.Parameters.Add("p_code", OracleDbType.Varchar2).Value = dto.ClassCode;
                cmdCheck.Parameters.Add("p_exclude_id", OracleDbType.Int32).Value = dto.ClassId;
                var pCount = cmdCheck.Parameters.Add("p_count", OracleDbType.Int32);
                pCount.Direction = ParameterDirection.Output;
                await cmdCheck.ExecuteNonQueryAsync();
                
                if (Convert.ToInt32(pCount.Value.ToString()) > 0)
                {
                    return new ApiResponse<Class> { Success = false, Message = "Mã lớp đã tồn tại" };
                }

                using var cmdUp = new OracleCommand("SP_UPDATE_CLASS", conn);
                cmdUp.CommandType = CommandType.StoredProcedure;
                cmdUp.Parameters.Add("p_id", OracleDbType.Int32).Value = dto.ClassId;
                cmdUp.Parameters.Add("p_code", OracleDbType.Varchar2).Value = dto.ClassCode;
                cmdUp.Parameters.Add("p_name", OracleDbType.NVarchar2).Value = dto.ClassName;
                cmdUp.Parameters.Add("p_start", OracleDbType.Date).Value = dto.StartDate;
                cmdUp.Parameters.Add("p_end", OracleDbType.Date).Value = dto.EndDate;
                cmdUp.Parameters.Add("p_max", OracleDbType.Int32).Value = dto.MaxSize;
                cmdUp.Parameters.Add("p_course_id", OracleDbType.Int32).Value = dto.CourseId;
                cmdUp.Parameters.Add("p_teacher_id", OracleDbType.Int32).Value = dto.TeacherId;
                cmdUp.Parameters.Add("p_status", OracleDbType.NVarchar2).Value = string.IsNullOrEmpty(dto.Status) ? DBNull.Value : dto.Status;
                await cmdUp.ExecuteNonQueryAsync();

                var updated = await GetClassByIdAsync(dto.ClassId);
                return new ApiResponse<Class> { Success = true, Message = "Cập nhật lớp học thành công", Data = updated };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating class");
                return new ApiResponse<Class> { Success = false, Message = "Có lỗi xảy ra khi cập nhật lớp học" };
            }
        }

        public async Task<ApiResponse<bool>> DeleteClassAsync(int id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                using var cmd = new OracleCommand("SP_DELETE_CLASS", conn);
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
                     return new ApiResponse<bool> { Success = false, Message = "Không thể xóa lớp đã có đăng ký hoặc lịch học" };
                }
                catch (OracleException oex) when (oex.Number == 20001)
                {
                     return new ApiResponse<bool> { Success = false, Message = oex.Message.Contains("ORA-20001") ? oex.Message.Split('\n')[0].Replace("ORA-20001: ", "") : "Không thể xóa lớp học đã có học viên đăng ký." };
                }

                int affected = 0;
                if (pRow.Value != null && int.TryParse(pRow.Value.ToString(), out var a)) affected = a;
                
                if (affected == 0) return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy lớp" };
                
                return new ApiResponse<bool> { Success = true, Message = "Xóa lớp học thành công", Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting class");
                return new ApiResponse<bool> { Success = false, Message = "Có lỗi xảy ra khi xóa lớp học: " + ex.Message };
            }
        }

        public async Task<List<Student>> GetRosterAsync(int classId)
        {
            return await ExecuteStoredProcedureQueryAsync<Student>("SP_GET_CLASS_ROSTER", new { p_class_id = classId });
        }
    }
}
