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
            var valid = await _authService.CheckSessionAsync(username, sessionId);
            return Ok(new { status = valid ? "valid" : "invalid" });
        }
    }
}