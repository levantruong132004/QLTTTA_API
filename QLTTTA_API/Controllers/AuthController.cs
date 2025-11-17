using Microsoft.AspNetCore.Mvc;
using QLTTTA_API.Models;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new LoginResponse
                {
                    Success = false,
                    Message = "Dữ liệu đầu vào không hợp lệ"
                });
            }

            var result = await _authService.AuthenticateAsync(request);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return Unauthorized(result);
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var sid = Request.Headers["X-Session-Id"].FirstOrDefault() ?? Request.Query["sessionId"].FirstOrDefault();
            var username = Request.Query["username"].FirstOrDefault() ?? string.Empty;
            await _authService.LogoutAsync(username, sid ?? "");
            return Ok(new { Success = true, Message = "Đăng xuất thành công" });
        }

        [HttpPost("register")]
        public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new RegisterResponse
                {
                    Success = false,
                    Message = "Dữ liệu đầu vào không hợp lệ"
                });
            }

            var result = await _authService.RegisterAsync(request);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }

        // ===== Registration with OTP =====
        [HttpPost("register/initiate-otp")]
        public async Task<ActionResult<OtpInitiateResponse>> InitiateRegisterOtp([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new OtpInitiateResponse { Success = false, Message = "Dữ liệu không hợp lệ" });
            }
            var res = await _authService.InitiateRegisterOtpAsync(request);
            if (res.Success) return Ok(res);
            return BadRequest(res);
        }

        [HttpPost("register/verify-otp")]
        public async Task<ActionResult<RegisterResponse>> VerifyRegisterOtp([FromBody] OtpVerifyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new RegisterResponse { Success = false, Message = "Dữ liệu không hợp lệ" });
            }
            var res = await _authService.VerifyRegisterOtpAsync(request);
            if (res.Success) return Ok(res);
            return BadRequest(res);
        }

        // ===== Forgot password with OTP =====
        [HttpPost("forgot/initiate")]
        public async Task<ActionResult<ForgotPasswordInitiateResponse>> InitiateForgot([FromBody] ForgotPasswordInitiateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ForgotPasswordInitiateResponse { Success = false, Message = "Dữ liệu không hợp lệ" });
            }
            var res = await _authService.InitiateForgotPasswordAsync(request);
            if (res.Success) return Ok(res);
            // If ShouldRegister = true, still return 200 to allow client redirect logic
            if (res.ShouldRegister) return Ok(res);
            return BadRequest(res);
        }

        [HttpPost("forgot/verify")]
        public async Task<ActionResult<BasicResponse>> VerifyForgot([FromBody] ForgotPasswordVerifyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new BasicResponse { Success = false, Message = "Dữ liệu không hợp lệ" });
            }
            var res = await _authService.VerifyForgotPasswordAsync(request);
            if (res.Success) return Ok(res);
            return BadRequest(res);
        }

        [HttpGet("test-db")]
        public async Task<IActionResult> TestDatabase()
        {
            try
            {
                var result = await _authService.TestDatabaseAsync();
                return Ok(new { Success = true, Message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("check-session")]
        public async Task<IActionResult> CheckSession([FromQuery] string username, [FromQuery] string sessionId)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(sessionId))
            {
                return BadRequest(new { status = "invalid", reason = "missing" });
            }
            // Lấy loại thiết bị ưu tiên từ header, fallback query, mặc định "pc"
            var deviceType = Request.Headers["X-Device-Type"].FirstOrDefault()
                             ?? Request.Query["deviceType"].FirstOrDefault()
                             ?? "pc";
            var valid = await _authService.CheckSessionAsync(username, sessionId, deviceType);
            return Ok(new { status = valid ? "valid" : "invalid" });
        }

        [HttpGet("debug-session")]
        public async Task<IActionResult> DebugSession([FromQuery] string? username, [FromQuery] string? sessionId, [FromQuery] string? deviceType)
        {
            var headerSession = Request.Headers["X-Session-Id"].FirstOrDefault();
            var cookieSession = Request.Cookies["SessionId"];
            var hdrDevice = Request.Headers["X-Device-Type"].FirstOrDefault();
            deviceType = (deviceType ?? hdrDevice ?? "pc").Trim().ToLowerInvariant();
            if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
            string? resolvedUsername = null;
            int? userId = null;
            string? dbSidPc = null;
            string? dbSidMobile = null;
            bool? isActive = null;
            try
            {
                using var conn = new Oracle.ManagedDataAccess.Client.OracleConnection(_authService is AuthService svc ? svc.GetType().GetField("_connectionString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(svc)?.ToString() : null);
                if (!string.IsNullOrEmpty(conn.ConnectionString))
                {
                    await conn.OpenAsync();
                    // Ưu tiên tra theo username; nếu không có thì tra theo sessionId cung cấp hoặc header/cookie
                    if (!string.IsNullOrWhiteSpace(username))
                    {
                        using var cmd = new Oracle.ManagedDataAccess.Client.OracleCommand(@"SELECT ID_NGUOI_DUNG, TEN_DANG_NHAP, SESSION_ID_PC, SESSION_ID_MOBILE, TRANG_THAI_KICH_HOAT FROM TAI_KHOAN WHERE UPPER(TEN_DANG_NHAP)=UPPER(:u)", conn) { BindByName = true };
                        cmd.Parameters.Add(":u", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2).Value = username;
                        using var rdr = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
                        if (await rdr.ReadAsync())
                        {
                            userId = rdr.IsDBNull(0) ? null : (int?)rdr.GetInt32(0);
                            resolvedUsername = rdr.IsDBNull(1) ? null : rdr.GetString(1);
                            dbSidPc = rdr.IsDBNull(2) ? null : rdr.GetString(2);
                            dbSidMobile = rdr.IsDBNull(3) ? null : rdr.GetString(3);
                            isActive = !rdr.IsDBNull(4) && rdr.GetInt32(4) == 1;
                        }
                    }
                    else
                    {
                        var sidSearch = sessionId ?? headerSession ?? cookieSession;
                        if (!string.IsNullOrWhiteSpace(sidSearch))
                        {
                            using var cmd = new Oracle.ManagedDataAccess.Client.OracleCommand(@"SELECT ID_NGUOI_DUNG, TEN_DANG_NHAP, SESSION_ID_PC, SESSION_ID_MOBILE, TRANG_THAI_KICH_HOAT FROM TAI_KHOAN WHERE SESSION_ID_PC=:sid OR SESSION_ID_MOBILE=:sid", conn) { BindByName = true };
                            cmd.Parameters.Add(":sid", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2).Value = sidSearch;
                            using var rdr = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
                            if (await rdr.ReadAsync())
                            {
                                userId = rdr.IsDBNull(0) ? null : (int?)rdr.GetInt32(0);
                                resolvedUsername = rdr.IsDBNull(1) ? null : rdr.GetString(1);
                                dbSidPc = rdr.IsDBNull(2) ? null : rdr.GetString(2);
                                dbSidMobile = rdr.IsDBNull(3) ? null : rdr.GetString(3);
                                isActive = !rdr.IsDBNull(4) && rdr.GetInt32(4) == 1;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DebugSession error");
                return StatusCode(500, new { error = ex.Message });
            }
            return Ok(new
            {
                ProvidedQueryUsername = username,
                ProvidedQuerySessionId = sessionId,
                HeaderSessionId = headerSession,
                CookieSessionId = cookieSession,
                DeviceType = deviceType,
                ResolvedUsername = resolvedUsername,
                UserId = userId,
                DbSessionPc = dbSidPc,
                DbSessionMobile = dbSidMobile,
                MatchesHeader = !string.IsNullOrEmpty(headerSession) && (headerSession == dbSidPc || headerSession == dbSidMobile),
                MatchesCookie = !string.IsNullOrEmpty(cookieSession) && (cookieSession == dbSidPc || cookieSession == dbSidMobile),
                IsActive = isActive,
                Source = !string.IsNullOrWhiteSpace(username) ? "BY_USERNAME" : "BY_SESSION",
                Note = "Dùng để đối chiếu vì sao profile rỗng"
            });
        }
    }
}