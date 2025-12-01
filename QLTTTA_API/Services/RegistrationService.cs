using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;
using Microsoft.AspNetCore.Http;
using System.Data;

namespace QLTTTA_API.Services
{
    public interface IRegistrationService
    {
        Task<List<Registration>> GetRegistrationsAsync(string? status = null, int? classId = null, string? classCode = null);
        Task<Registration?> GetByIdAsync(int id);
        Task<ApiResponse<bool>> ApproveAsync(int registrationId, int? newClassId = null);
        Task<ApiResponse<bool>> RejectAsync(int registrationId);
        Task<List<Registration>> GetMyRegistrationsAsync();
        Task<List<AccountantRegDetail>> GetAccountantRegistrationsAsync(int? courseId = null, int? classId = null);
        Task<AccountantRegDetail?> GetAccountantRegistrationByIdAsync(int registrationId);
        Task<List<Registration>> SearchAsync(string q);
    }

    public class RegistrationService : BaseService, IRegistrationService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public RegistrationService(IConfiguration configuration, ILogger<RegistrationService> logger, IOracleConnectionProvider userConnProvider, IHttpContextAccessor httpContextAccessor)
            : base(configuration, logger, userConnProvider) { _httpContextAccessor = httpContextAccessor; }

        public async Task<List<Registration>> GetRegistrationsAsync(string? status = null, int? classId = null, string? classCode = null)
        {
            return await ExecuteStoredProcedureQueryAsync<Registration>("SP_GET_REGISTRATIONS", new { 
                p_status = status, 
                p_class_id = classId, 
                p_class_code = classCode 
            });
        }

        public async Task<Registration?> GetByIdAsync(int id)
        {
            var list = await ExecuteStoredProcedureQueryAsync<Registration>("SP_GET_REGISTRATION_BY_ID", new { p_id = id });
            return list.FirstOrDefault();
        }

        private class ClassSizeDto { public int Cnt { get; set; } public int Maxs { get; set; } }

        public async Task<ApiResponse<bool>> ApproveAsync(int registrationId, int? newClassId = null)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                
                // 1. Get Info
                var reg = await GetByIdAsync(registrationId);
                if (reg == null) return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy đơn đăng ký" };
                int classIdToUse = reg.ClassId;

                // 2. Update Class if needed
                if (newClassId.HasValue && newClassId.Value != classIdToUse)
                {
                    using var cmd = new OracleCommand("SP_UPDATE_REGISTRATION_CLASS", conn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_reg_id", OracleDbType.Int32).Value = registrationId;
                    cmd.Parameters.Add("p_class_id", OracleDbType.Int32).Value = newClassId.Value;
                    await cmd.ExecuteNonQueryAsync();
                    classIdToUse = newClassId.Value;
                }

                // 3. Check Size
                var sizes = await ExecuteStoredProcedureQueryAsync<ClassSizeDto>("SP_CHECK_CLASS_SIZE", new { p_class_id = classIdToUse });
                var size = sizes.FirstOrDefault();
                if (size != null && size.Cnt >= size.Maxs)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Lớp đã đủ sĩ số" };
                }

                // 4. Find Staff
                int staffId = 0;
                var sid = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(sid))
                {
                    try
                    {
                        using var adminConn = await GetAdminConnectionAsync();
                        using var cmdStaff = new OracleCommand("SP_FIND_STAFF_BY_SESSION", adminConn);
                        cmdStaff.CommandType = CommandType.StoredProcedure;
                        cmdStaff.Parameters.Add("p_session_id", OracleDbType.Varchar2).Value = sid;
                        var pStaffId = cmdStaff.Parameters.Add("p_staff_id", OracleDbType.Int32);
                        pStaffId.Direction = ParameterDirection.Output;
                        await cmdStaff.ExecuteNonQueryAsync();
                        if (pStaffId.Value != null && int.TryParse(pStaffId.Value.ToString(), out var s)) staffId = s;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not resolve staff ID");
                    }
                }

                // 5. Approve
                using var cmdApprove = new OracleCommand("SP_APPROVE_REGISTRATION", conn);
                cmdApprove.CommandType = CommandType.StoredProcedure;
                cmdApprove.Parameters.Add("p_id", OracleDbType.Int32).Value = registrationId;
                cmdApprove.Parameters.Add("p_staff_id", OracleDbType.Int32).Value = staffId;
                await cmdApprove.ExecuteNonQueryAsync();

                // 6. Update Class Status if full
                var sizes2 = await ExecuteStoredProcedureQueryAsync<ClassSizeDto>("SP_CHECK_CLASS_SIZE", new { p_class_id = classIdToUse });
                var size2 = sizes2.FirstOrDefault();
                if (size2 != null && size2.Cnt >= size2.Maxs)
                {
                    using var cmdFull = new OracleCommand("SP_UPDATE_CLASS_STATUS_FULL", conn);
                    cmdFull.CommandType = CommandType.StoredProcedure;
                    cmdFull.Parameters.Add("p_class_id", OracleDbType.Int32).Value = classIdToUse;
                    await cmdFull.ExecuteNonQueryAsync();
                }

                return new ApiResponse<bool> { Success = true, Message = "Duyệt đơn thành công", Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Approve registration failed");
                return new ApiResponse<bool> { Success = false, Message = "Có lỗi xảy ra khi duyệt đơn: " + ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> RejectAsync(int registrationId)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                
                // Find Staff
                int staffId = 0;
                var sid = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(sid))
                {
                    try
                    {
                        using var adminConn = await GetAdminConnectionAsync();
                        using var cmdStaff = new OracleCommand("SP_FIND_STAFF_BY_SESSION", adminConn);
                        cmdStaff.CommandType = CommandType.StoredProcedure;
                        cmdStaff.Parameters.Add("p_session_id", OracleDbType.Varchar2).Value = sid;
                        var pStaffId = cmdStaff.Parameters.Add("p_staff_id", OracleDbType.Int32);
                        pStaffId.Direction = ParameterDirection.Output;
                        await cmdStaff.ExecuteNonQueryAsync();
                        if (pStaffId.Value != null && int.TryParse(pStaffId.Value.ToString(), out var s)) staffId = s;
                    }
                    catch {}
                }

                using var cmdReject = new OracleCommand("SP_REJECT_REGISTRATION", conn);
                cmdReject.CommandType = CommandType.StoredProcedure;
                cmdReject.Parameters.Add("p_id", OracleDbType.Int32).Value = registrationId;
                cmdReject.Parameters.Add("p_staff_id", OracleDbType.Int32).Value = staffId;
                await cmdReject.ExecuteNonQueryAsync();

                return new ApiResponse<bool> { Success = true, Message = "Từ chối đơn thành công", Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reject registration failed");
                return new ApiResponse<bool> { Success = false, Message = "Có lỗi xảy ra khi từ chối đơn" };
            }
        }

        public async Task<List<Registration>> GetMyRegistrationsAsync()
        {
            // Get current user ID from context or connection?
            // SP_GET_MY_REGISTRATIONS takes p_student_id.
            // I need to resolve student ID.
            // If I use GetConnectionAsync(), I am connected as the user.
            // But I need the ID to pass to SP.
            // I can query "SELECT ID_HOC_VIEN FROM HOC_VIEN WHERE ID_HOC_VIEN = SYS_CONTEXT('USERENV','SESSION_USER')" or similar?
            // Or just use the username from context.
            // The original code used `SELECT ID_HOC_VIEN FROM HOC_VIEN WHERE ID_HOC_VIEN = (SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE TEN_DANG_NHAP = USER)`.
            // I can do that or pass it from C#.
            
            // Let's resolve ID first.
            // Or I can update SP to use `SYS_CONTEXT` internally?
            // SP_GET_MY_REGISTRATIONS takes p_student_id.
            // I'll resolve it in C#.
            
            int studentId = 0;
            using (var conn = await GetConnectionAsync())
            {
                using var cmd = new OracleCommand("SELECT ID_NGUOI_DUNG FROM QLTT_ADMIN.TAI_KHOAN WHERE UPPER(TEN_DANG_NHAP) = SYS_CONTEXT('USERENV', 'SESSION_USER')", conn);
                var obj = await cmd.ExecuteScalarAsync();
                if (obj != null) studentId = Convert.ToInt32(obj);
            }
            
            if (studentId == 0) return new List<Registration>();

            return await ExecuteStoredProcedureQueryAsync<Registration>("SP_GET_MY_REGISTRATIONS", new { p_student_id = studentId });
        }

        public async Task<List<AccountantRegDetail>> GetAccountantRegistrationsAsync(int? courseId = null, int? classId = null)
        {
            return await ExecuteStoredProcedureQueryAsync<AccountantRegDetail>("SP_GET_ACCOUNTANT_REGS", new { p_course_id = courseId, p_class_id = classId });
        }

        public async Task<AccountantRegDetail?> GetAccountantRegistrationByIdAsync(int registrationId)
        {
            var list = await ExecuteStoredProcedureQueryAsync<AccountantRegDetail>("SP_GET_ACCOUNTANT_REG_BY_ID", new { p_id = registrationId });
            return list.FirstOrDefault();
        }

        public async Task<List<Registration>> SearchAsync(string q)
        {
            int? id = null;
            if (int.TryParse(q, out var i)) id = i;
            return await ExecuteStoredProcedureQueryAsync<Registration>("SP_SEARCH_REGISTRATIONS", new { p_id = id, p_keyword = $"%{q}%" });
        }
    }
}
