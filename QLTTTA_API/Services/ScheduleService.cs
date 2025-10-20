using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;

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
            var sql = @"SELECT ID_LICH_HOC AS SCHEDULE_ID, ID_LOP_HOC AS CLASS_ID, THU_TRONG_TUAN AS DAY_OF_WEEK,
                   GIO_BAT_DAU AS START_TIME,
                   GIO_KET_THUC AS END_TIME
               FROM LICH_HOC WHERE ID_LOP_HOC = :cid ORDER BY THU_TRONG_TUAN, GIO_BAT_DAU";
            return await ExecuteQueryAsync<Schedule>(sql, new { cid = classId });
        }

        public async Task<Schedule?> GetByIdAsync(int scheduleId)
        {
            var sql = @"SELECT ID_LICH_HOC AS SCHEDULE_ID, ID_LOP_HOC AS CLASS_ID, THU_TRONG_TUAN AS DAY_OF_WEEK,
                   GIO_BAT_DAU AS START_TIME,
                   GIO_KET_THUC AS END_TIME
               FROM LICH_HOC WHERE ID_LICH_HOC = :id";
            return await ExecuteQuerySingleAsync<Schedule>(sql, new { id = scheduleId });
        }

        public async Task<ApiResponse<Schedule>> CreateAsync(ScheduleCreateDto dto)
        {
            try
            {
                // Basic validation
                if (dto.DayOfWeek < 2 || dto.DayOfWeek > 8)
                    return new ApiResponse<Schedule> { Success = false, Message = "Thứ phải từ 2 đến 8" };

                var sql = @"INSERT INTO LICH_HOC (ID_LOP_HOC, THU_TRONG_TUAN, GIO_BAT_DAU, GIO_KET_THUC)
                            VALUES (:cid, :dow, :st, :et)";
                await ExecuteNonQueryAsync(sql, new { cid = dto.ClassId, dow = dto.DayOfWeek, st = dto.StartTime, et = dto.EndTime });

                var created = await ExecuteQuerySingleAsync<Schedule>(@"SELECT ID_LICH_HOC AS SCHEDULE_ID, ID_LOP_HOC AS CLASS_ID, THU_TRONG_TUAN AS DAY_OF_WEEK,
                               GIO_BAT_DAU AS START_TIME,
                               GIO_KET_THUC AS END_TIME
                        FROM LICH_HOC WHERE ID_LOP_HOC = :cid AND THU_TRONG_TUAN = :dow AND GIO_BAT_DAU = :st ORDER BY ID_LICH_HOC DESC",
                    new { cid = dto.ClassId, dow = dto.DayOfWeek, st = dto.StartTime });

                return new ApiResponse<Schedule> { Success = true, Message = "Tạo lịch học thành công", Data = created };
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

                var sql = @"UPDATE LICH_HOC SET ID_LOP_HOC=:cid, THU_TRONG_TUAN=:dow, GIO_BAT_DAU=:st, GIO_KET_THUC=:et
                            WHERE ID_LICH_HOC=:id";
                await ExecuteNonQueryAsync(sql, new { cid = dto.ClassId, dow = dto.DayOfWeek, st = dto.StartTime, et = dto.EndTime, id = dto.ScheduleId });
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
                var rows = await ExecuteNonQueryAsync("DELETE FROM LICH_HOC WHERE ID_LICH_HOC = :id", new { id = scheduleId });
                if (rows == 0) return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy lịch học" };
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
