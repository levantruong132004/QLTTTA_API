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
        private readonly IConfiguration _config;

        public AuthController(IAuthService authService, ILogger<AuthController> logger, IConfiguration config)
        {
            _authService = authService;
            _logger = logger;
            _config = config;
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

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var sid = Request.Headers["X-Session-Id"].FirstOrDefault();
            var deviceType = Request.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
            if (deviceType != "mobile" && deviceType != "pc") deviceType = "pc";
            if (string.IsNullOrWhiteSpace(sid)) return Unauthorized(new { Success = false, Message = "Thiếu phiên đăng nhập" });

            try
            {
                var cs = _config.GetConnectionString("OracleDbConnection") ?? string.Empty;
                using var conn = new Oracle.ManagedDataAccess.Client.OracleConnection(cs);
                await conn.OpenAsync();
                var col = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                var sql = $@"SELECT tk.ID_NGUOI_DUNG, tk.TEN_DANG_NHAP, tk.EMAIL, vt.TEN_VAI_TRO, vt.ID_VAI_TRO, hv.HO_TEN
                               FROM TAI_KHOAN tk
                          LEFT JOIN VAI_TRO vt ON vt.ID_VAI_TRO = tk.ID_VAI_TRO
                          LEFT JOIN HOC_VIEN hv ON hv.ID_HOC_VIEN = tk.ID_NGUOI_DUNG
                              WHERE {col} = :sid";
                using var cmd = new Oracle.ManagedDataAccess.Client.OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(":sid", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2).Value = sid;
                using var rdr = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
                if (await rdr.ReadAsync())
                {
                    var user = new QLTTTA_API.Models.UserInfo
                    {
                        UserId = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0),
                        Username = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1),
                        Email = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2),
                        Role = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3),
                        RoleId = rdr.IsDBNull(4) ? 0 : rdr.GetInt32(4),
                        FullName = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5)
                    };
                    return Ok(new { Success = true, User = user });
                }
                return Unauthorized(new { Success = false, Message = "Phiên không khớp" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Me endpoint error");
                return StatusCode(500, new { Success = false, Message = "Lỗi hệ thống" });
            }
        }
    }
}