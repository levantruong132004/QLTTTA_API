using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/auth/qr")]
    public class QrAuthController : ControllerBase
    {
        private readonly IQrLoginService _qr;
        private readonly IUserCredentialCache _credCache;
        private readonly IConfiguration _config;
        private readonly ILogger<QrAuthController> _logger;

        public QrAuthController(IQrLoginService qr, IUserCredentialCache credCache, IConfiguration config, ILogger<QrAuthController> logger)
        {
            _qr = qr;
            _credCache = credCache;
            _config = config;
            _logger = logger;
        }

        public class CreateChallengeRequest { public string? RequesterDevice { get; set; } public string? RequesterInfo { get; set; } }
        public class CreateChallengeResponse { public bool Success { get; set; } public string? Id { get; set; } public string? Payload { get; set; } public int ExpiresInSeconds { get; set; } public string? Message { get; set; } }
        public class ScanRequest { public string? Id { get; set; } }
        public class ApproveRequest { public string? Id { get; set; } public bool Approve { get; set; } }
        public class StatusResponse { public bool Success { get; set; } public string? Status { get; set; } public string? Id { get; set; } public string? GrantToken { get; set; } public string? ApproverDisplayName { get; set; } public string? Message { get; set; } }
        public class ConsumeRequest { public string? Id { get; set; } public string? GrantToken { get; set; } public string? DeviceType { get; set; } }

        [HttpPost("challenge")]
        public ActionResult<CreateChallengeResponse> CreateChallenge([FromBody] CreateChallengeRequest req)
        {
            var device = (req?.RequesterDevice ?? "pc").Trim().ToLowerInvariant();
            if (device != "mobile" && device != "pc") device = "pc";
            var c = _qr.Create(device, req?.RequesterInfo, TimeSpan.FromMinutes(3));
            // Encode a simple payload string; both web and mobile scanners will parse this string
            var payload = $"qlttta://qr-login?id={c.Id}";
            return Ok(new CreateChallengeResponse { Success = true, Id = c.Id, Payload = payload, ExpiresInSeconds = (int)(c.ExpiresAt - DateTime.UtcNow).TotalSeconds });
        }

        [HttpPost("scan")] // must be authenticated via existing session (X-Session-Id)
        public ActionResult<StatusResponse> Scan([FromBody] ScanRequest req)
        {
            var id = req?.Id;
            if (string.IsNullOrWhiteSpace(id)) return BadRequest(new StatusResponse { Success = false, Message = "Thiếu id" });

            var sid = Request.Headers["X-Session-Id"].FirstOrDefault();
            var device = (Request.Headers["X-Device-Type"].FirstOrDefault() ?? "pc").Trim().ToLowerInvariant();
            if (device != "mobile") device = "pc";
            if (string.IsNullOrWhiteSpace(sid)) return Unauthorized(new StatusResponse { Success = false, Message = "Thiếu phiên (X-Session-Id)" });
            if (!_credCache.TryGet(sid!, out var cred)) return Unauthorized(new StatusResponse { Success = false, Message = "Phiên không hợp lệ" });

            // Lookup userId + display name via DB (admin connection)
            int? userId = null; string display = cred.Username;
            try
            {
                var adminCs = _config.GetConnectionString("OracleDbConnection") ?? string.Empty;
                using var conn = new OracleConnection(adminCs);
                conn.Open();
                using var cmd = new OracleCommand("SELECT ID_NGUOI_DUNG, NVL(HO_TEN, TEN_DANG_NHAP) HO_TEN FROM TAI_KHOAN tk LEFT JOIN HOC_VIEN hv ON hv.ID_HOC_VIEN = tk.ID_NGUOI_DUNG WHERE UPPER(tk.TEN_DANG_NHAP)=UPPER(:u)", conn) { BindByName = true };
                cmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = cred.Username;
                using var rdr = cmd.ExecuteReader();
                if (rdr.Read())
                {
                    userId = rdr.IsDBNull(0) ? null : rdr.GetInt32(0);
                    display = rdr.IsDBNull(1) ? cred.Username : rdr.GetString(1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Scan lookup user failed");
            }
            if (!userId.HasValue) return Unauthorized(new StatusResponse { Success = false, Message = "Không xác định được người dùng" });

            _qr.TryScan(id!, sid!, device, userId.Value, cred.Username, display, out var c);
            return Ok(new StatusResponse { Success = true, Status = c?.Status, Id = id, ApproverDisplayName = display });
        }

        [HttpPost("approve")] // authenticated approver
        public ActionResult<StatusResponse> Approve([FromBody] ApproveRequest req)
        {
            var id = req?.Id;
            if (string.IsNullOrWhiteSpace(id)) return BadRequest(new StatusResponse { Success = false, Message = "Thiếu id" });
            var sid = Request.Headers["X-Session-Id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(sid)) return Unauthorized(new StatusResponse { Success = false, Message = "Thiếu phiên" });
            if (!_credCache.TryGet(sid!, out var _)) return Unauthorized(new StatusResponse { Success = false, Message = "Phiên không hợp lệ" });

            if (!_qr.TryApprove(id!, req!.Approve, out var c) || c == null)
            {
                return NotFound(new StatusResponse { Success = false, Message = "Không tìm thấy challenge" });
            }
            return Ok(new StatusResponse { Success = true, Status = c.Status, Id = id, GrantToken = c.GrantToken });
        }

        [HttpGet("status")] // requester polls by id
        public ActionResult<StatusResponse> Status([FromQuery] string id)
        {
            if (!_qr.TryGet(id, out var c) || c == null)
            {
                return NotFound(new StatusResponse { Success = false, Message = "Không tìm thấy challenge" });
            }
            return Ok(new StatusResponse
            {
                Success = true,
                Id = id,
                Status = c.Status,
                GrantToken = c.Status == "approved" ? c.GrantToken : null,
                ApproverDisplayName = c.ApproverDisplayName
            });
        }

        [HttpPost("consume")] // requester exchanges grant for session on target device
        public async Task<ActionResult<LoginResponse>> Consume([FromBody] ConsumeRequest req)
        {
            var id = req?.Id; var token = req?.GrantToken;
            var device = (req?.DeviceType ?? "pc").Trim().ToLowerInvariant();
            if (device != "mobile" && device != "pc") device = "pc";
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new LoginResponse { Success = false, Message = "Thiếu tham số" });
            }
            if (!_qr.TryGet(id!, out var c) || c == null) return NotFound(new LoginResponse { Success = false, Message = "Challenge không tồn tại" });
            if (c.Status != "approved" || c.GrantToken != token) return Unauthorized(new LoginResponse { Success = false, Message = "Chưa được phê duyệt" });
            if (c.Consumed) return Unauthorized(new LoginResponse { Success = false, Message = "Đã dùng" });
            if (string.IsNullOrWhiteSpace(c.ApproverSessionId) || string.IsNullOrWhiteSpace(c.ApproverUsername) || !c.ApproverUserId.HasValue)
            {
                return Unauthorized(new LoginResponse { Success = false, Message = "Thiếu thông tin phê duyệt" });
            }
            if (!_credCache.TryGet(c.ApproverSessionId!, out var approverCred))
            {
                return Unauthorized(new LoginResponse { Success = false, Message = "Phiên phê duyệt đã hết hạn" });
            }
            _logger.LogInformation("[QR] Consume start id={Id} device={Device} approverUserId={UserId} approver={Approver}", id, device, c.ApproverUserId, c.ApproverUsername);

            // Create new session for requester device (keep the other device signed in)
            var newSessionId = Guid.NewGuid().ToString("N");
            try
            {
                var adminCs = _config.GetConnectionString("OracleDbConnection") ?? string.Empty;
                using var conn = new OracleConnection(adminCs);
                await conn.OpenAsync();
                using var tx = conn.BeginTransaction();
                // Lock row
                using (var lockCmd = new OracleCommand("SELECT SESSION_ID_PC, SESSION_ID_MOBILE FROM TAI_KHOAN WHERE ID_NGUOI_DUNG = :id FOR UPDATE", conn) { BindByName = true, Transaction = tx })
                {
                    lockCmd.Parameters.Add(":id", OracleDbType.Int32).Value = c.ApproverUserId!.Value;
                    string? oldPc = null, oldMobile = null;
                    using (var rdr = await lockCmd.ExecuteReaderAsync())
                    {
                        if (await rdr.ReadAsync())
                        {
                            oldPc = rdr.IsDBNull(0) ? null : rdr.GetString(0);
                            oldMobile = rdr.IsDBNull(1) ? null : rdr.GetString(1);
                        }
                    }
                    _logger.LogInformation("[QR] Existing sessions before update oldPc={OldPc} oldMobile={OldMobile}", oldPc, oldMobile);
                    string sql = device == "mobile"
                        ? "UPDATE TAI_KHOAN SET SESSION_ID_MOBILE = :sid WHERE ID_NGUOI_DUNG = :id"
                        : "UPDATE TAI_KHOAN SET SESSION_ID_PC = :sid WHERE ID_NGUOI_DUNG = :id";
                    using var up = new OracleCommand(sql, conn) { BindByName = true, Transaction = tx };
                    up.Parameters.Add(":sid", OracleDbType.Varchar2).Value = newSessionId;
                    up.Parameters.Add(":id", OracleDbType.Int32).Value = c.ApproverUserId!.Value;
                    var affected = await up.ExecuteNonQueryAsync();
                    if (affected != 1)
                    {
                        await tx.RollbackAsync();
                        return Problem("Không thể cập nhật phiên đăng nhập", statusCode: 500);
                    }
                    await tx.CommitAsync();
                    _logger.LogInformation("[QR] Session column updated for device={Device} newSessionId={SessionId}", device, newSessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Consume QR error updating sessions");
                return Problem("Lỗi hệ thống", statusCode: 500);
            }

            // Set credentials for new session using approver's stored credentials
            _credCache.Set(newSessionId, approverCred.Username, approverCred.Password, TimeSpan.FromHours(1));
            _logger.LogInformation("[QR] Credential cache set newSessionId={SessionId} username={Username}", newSessionId, approverCred.Username);

            c.Status = "used"; c.Consumed = true;
            _logger.LogInformation("[QR] Challenge consumed id={Id} finalStatus={Status}", id, c.Status);

            // Build user info
            var user = new UserInfo { UserId = c.ApproverUserId!.Value, Username = c.ApproverUsername!, Email = string.Empty, Role = string.Empty, RoleId = 0, FullName = c.ApproverDisplayName ?? c.ApproverUsername };
            return Ok(new LoginResponse { Success = true, Message = "Đăng nhập bằng QR thành công", SessionId = newSessionId, Token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{user.UserId}:{user.Username}:{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}")), User = user });
        }
    }
}
