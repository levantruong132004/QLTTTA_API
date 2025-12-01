using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models.DTOs;
using System.Data;

namespace QLTTTA_API.Services
{
    public interface IAdminStaffService
    {
        Task<List<StaffListItemDto>> GetStaffAsync();
        Task<StaffListItemDto?> GetStaffByIdAsync(int id);
        Task<ApiResponse<StaffListItemDto>> CreateStaffAsync(StaffCreateDto dto);
        Task<ApiResponse<StaffListItemDto>> UpdateStaffAsync(int id, StaffUpdateDto dto);
        Task<ApiResponse<bool>> LockStaffAsync(int id);
        Task<ApiResponse<bool>> UnlockStaffAsync(int id);
        Task<List<TeacherDto>> GetTeachersAsync();
    }

    public class AdminStaffService : BaseService, IAdminStaffService
    {
        public AdminStaffService(IConfiguration configuration, ILogger<AdminStaffService> logger, IOracleConnectionProvider userConnProvider)
            : base(configuration, logger, userConnProvider) { }

        public async Task<List<StaffListItemDto>> GetStaffAsync()
        {
            return await ExecuteStoredProcedureQueryAsync<StaffListItemDto>("SP_GET_STAFF_LIST");
        }

        public async Task<StaffListItemDto?> GetStaffByIdAsync(int id)
        {
            var list = await ExecuteStoredProcedureQueryAsync<StaffListItemDto>("SP_GET_STAFF_BY_ID", new { p_id = id });
            return list.FirstOrDefault();
        }

        public async Task<ApiResponse<StaffListItemDto>> CreateStaffAsync(StaffCreateDto dto)
        {
            if (dto.RoleId != 3 && dto.RoleId != 4)
            {
                return new ApiResponse<StaffListItemDto> { Success = false, Message = "Chỉ được tạo nhân viên vai trò Kế toán (3) hoặc Nhân viên học vụ (4)" };
            }
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(dto.Email))
            {
                return new ApiResponse<StaffListItemDto> { Success = false, Message = "Thiếu thông tin bắt buộc" };
            }

            try
            {
                using var conn = await GetConnectionAsync();
                using var cmd = new OracleCommand(dto.RoleId == 4 ? "SP_DANG_KY_NHAN_VIEN_HOC_VU" : "SP_DANG_KY_KE_TOAN", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("p_ten_dang_nhap", OracleDbType.Varchar2).Value = dto.Username.Trim();
                cmd.Parameters.Add("p_mat_khau", OracleDbType.Varchar2).Value = dto.Password;
                cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = dto.Email.Trim();
                cmd.Parameters.Add("p_ho_ten", OracleDbType.NVarchar2).Value = (object?)dto.FullName ?? DBNull.Value;
                cmd.Parameters.Add("p_gioi_tinh", OracleDbType.NVarchar2).Value = (object?)dto.Sex ?? DBNull.Value;
                cmd.Parameters.Add("p_sdt", OracleDbType.Varchar2).Value = (object?)dto.Phone ?? DBNull.Value;

                var outMsg = new OracleParameter("p_ket_qua", OracleDbType.NVarchar2, 4000)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(outMsg);

                await cmd.ExecuteNonQueryAsync();
                var msg = outMsg.Value?.ToString();

                if (msg != null && msg.Contains("thành công", StringComparison.OrdinalIgnoreCase))
                {
                    return new ApiResponse<StaffListItemDto> { Success = true, Message = msg };
                }
                return new ApiResponse<StaffListItemDto> { Success = false, Message = msg ?? "Lỗi tạo nhân viên" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create staff failed");
                return new ApiResponse<StaffListItemDto> { Success = false, Message = "Lỗi: " + ex.Message };
            }
        }

        public async Task<ApiResponse<StaffListItemDto>> UpdateStaffAsync(int id, StaffUpdateDto dto)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                using var cmd = new OracleCommand("SP_UPDATE_STAFF_INFO", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
                cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = dto.Email;
                cmd.Parameters.Add("p_role_id", OracleDbType.Int32).Value = dto.RoleId;
                cmd.Parameters.Add("p_fullname", OracleDbType.NVarchar2).Value = dto.FullName;
                cmd.Parameters.Add("p_sex", OracleDbType.NVarchar2).Value = dto.Sex;
                cmd.Parameters.Add("p_phone", OracleDbType.Varchar2).Value = dto.Phone;
                var pStatus = cmd.Parameters.Add("p_status", OracleDbType.Varchar2, 4000);
                pStatus.Direction = ParameterDirection.Output;

                await cmd.ExecuteNonQueryAsync();
                var status = pStatus.Value?.ToString();

                if (status == "SUCCESS")
                {
                    return new ApiResponse<StaffListItemDto> { Success = true, Message = "Cập nhật thành công" };
                }
                return new ApiResponse<StaffListItemDto> { Success = false, Message = "Lỗi: " + status };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update staff failed");
                return new ApiResponse<StaffListItemDto> { Success = false, Message = "Lỗi: " + ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> LockStaffAsync(int id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                using var cmd = new OracleCommand("SP_LOCK_ACCOUNT", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
                var pRows = cmd.Parameters.Add("p_rows", OracleDbType.Int32);
                pRows.Direction = ParameterDirection.Output;
                await cmd.ExecuteNonQueryAsync();
                
                int rowsAffected = 0;
                if (pRows.Value != null && int.TryParse(pRows.Value.ToString(), out var r)) rowsAffected = r;

                if (rowsAffected > 0)
                {
                    return new ApiResponse<bool> { Success = true, Message = "Đã khóa tài khoản", Data = true };
                }
                return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy tài khoản hoặc không thể khóa" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lock staff failed");
                return new ApiResponse<bool> { Success = false, Message = "Lỗi: " + ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> UnlockStaffAsync(int id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                using var cmd = new OracleCommand("SP_UNLOCK_ACCOUNT", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
                var pRows = cmd.Parameters.Add("p_rows", OracleDbType.Int32);
                pRows.Direction = ParameterDirection.Output;
                await cmd.ExecuteNonQueryAsync();

                int rowsAffected = 0;
                if (pRows.Value != null && int.TryParse(pRows.Value.ToString(), out var r)) rowsAffected = r;

                if (rowsAffected > 0)
                {
                    return new ApiResponse<bool> { Success = true, Message = "Đã mở khóa tài khoản", Data = true };
                }
                return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy tài khoản hoặc không thể mở khóa" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unlock staff failed");
                return new ApiResponse<bool> { Success = false, Message = "Lỗi: " + ex.Message };
            }
        }
        public async Task<List<TeacherDto>> GetTeachersAsync()
        {
            try
            {
                using var connection = await GetConnectionAsync();
                using var command = new OracleCommand("SP_GET_ALL_TEACHERS", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("p_cursor", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

                var result = new List<TeacherDto>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Add(new TeacherDto
                        {
                            TeacherId = reader.GetInt32(reader.GetOrdinal("ID_GIANG_VIEN")),
                            FullName = reader.GetString(reader.GetOrdinal("HO_TEN")),
                            TeacherCode = reader.IsDBNull(reader.GetOrdinal("MA_GIANG_VIEN")) ? "" : reader.GetString(reader.GetOrdinal("MA_GIANG_VIEN"))
                        });
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting teachers");
                return new List<TeacherDto>();
            }
        }
    }
}
