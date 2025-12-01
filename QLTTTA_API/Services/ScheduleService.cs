using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;
using System.Data;

namespace QLTTTA_API.Services
{
    public interface IScheduleService
    {
        Task<List<Schedule>> GetByClassAsync(int classId);
        Task<Schedule?> GetByIdAsync(int scheduleId);
        Task<ApiResponse<Schedule>> CreateAsync(ScheduleCreateDto dto);
        Task<ApiResponse<Schedule>> UpdateAsync(ScheduleUpdateDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int scheduleId);
    }

    public class ScheduleService : BaseService, IScheduleService
    {
        public ScheduleService(IConfiguration configuration, ILogger<ScheduleService> logger, IOracleConnectionProvider userConnProvider)
            : base(configuration, logger, userConnProvider) { }

        public async Task<List<Schedule>> GetByClassAsync(int classId)
        {
            return await ExecuteStoredProcedureQueryAsync<Schedule>("SP_GET_SCHEDULE_BY_CLASS", new { p_class_id = classId });
        }

        public async Task<Schedule?> GetByIdAsync(int scheduleId)
        {
            var list = await ExecuteStoredProcedureQueryAsync<Schedule>("SP_GET_SCHEDULE_BY_ID", new { p_id = scheduleId });
            return list.FirstOrDefault();
        }

        public async Task<ApiResponse<Schedule>> CreateAsync(ScheduleCreateDto dto)
        {
            try
            {
                if (dto.DayOfWeek < 2 || dto.DayOfWeek > 8)
                    return new ApiResponse<Schedule> { Success = false, Message = "Thứ phải từ 2 đến 8" };

                var list = await ExecuteStoredProcedureQueryAsync<Schedule>("SP_CREATE_SCHEDULE", new { 
                    p_class_id = dto.ClassId,
                    p_dow = dto.DayOfWeek,
                    p_start = dto.StartTime,
                    p_end = dto.EndTime
                });
                
                return new ApiResponse<Schedule> { Success = true, Message = "Tạo lịch học thành công", Data = list.FirstOrDefault() };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating schedule");
                return new ApiResponse<Schedule> { Success = false, Message = "Có lỗi xảy ra khi tạo lịch học" };
            }
        }

        public async Task<ApiResponse<Schedule>> UpdateAsync(ScheduleUpdateDto dto)
        {
            try
            {
                var exists = await GetByIdAsync(dto.ScheduleId);
                if (exists == null) return new ApiResponse<Schedule> { Success = false, Message = "Không tìm thấy lịch học" };

                using var conn = await GetConnectionAsync();
                using var cmd = new OracleCommand("SP_UPDATE_SCHEDULE", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = dto.ScheduleId;
                cmd.Parameters.Add("p_class_id", OracleDbType.Int32).Value = dto.ClassId;
                cmd.Parameters.Add("p_dow", OracleDbType.Int32).Value = dto.DayOfWeek;
                cmd.Parameters.Add("p_start", OracleDbType.Varchar2).Value = dto.StartTime;
                cmd.Parameters.Add("p_end", OracleDbType.Varchar2).Value = dto.EndTime;
                await cmd.ExecuteNonQueryAsync();

                var updated = await GetByIdAsync(dto.ScheduleId);
                return new ApiResponse<Schedule> { Success = true, Message = "Cập nhật lịch học thành công", Data = updated };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating schedule");
                return new ApiResponse<Schedule> { Success = false, Message = "Có lỗi xảy ra khi cập nhật lịch học" };
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int scheduleId)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                using var cmd = new OracleCommand("SP_DELETE_SCHEDULE", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = scheduleId;
                var pRow = cmd.Parameters.Add("p_rowcount", OracleDbType.Int32);
                pRow.Direction = ParameterDirection.Output;
                await cmd.ExecuteNonQueryAsync();
                
                int affected = 0;
                if (pRow.Value != null && int.TryParse(pRow.Value.ToString(), out var a)) affected = a;

                if (affected == 0) return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy lịch học" };
                return new ApiResponse<bool> { Success = true, Message = "Xóa lịch học thành công", Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting schedule");
                return new ApiResponse<bool> { Success = false, Message = "Có lỗi xảy ra khi xóa lịch học" };
            }
        }
    }
}
