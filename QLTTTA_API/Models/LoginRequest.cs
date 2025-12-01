using System.ComponentModel.DataAnnotations;

namespace QLTTTA_API.Models
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Tên tài khoản là bắt buộc")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Loại thiết bị đăng nhập: "pc" hoặc "mobile". Mặc định là "pc" nếu không gửi.
        /// </summary>
        public string? DeviceType { get; set; }
    }

    public class RegisterRequest
    {
        [Required(ErrorMessage = "Họ tên là bắt buộc")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Giới tính là bắt buộc")]
        public string Sex { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ngày sinh là bắt buộc")]
        public DateTime? DateOfBirth { get; set; }

        [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        public string? Address { get; set; }

        [Required(ErrorMessage = "Tên tài khoản là bắt buộc")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên tài khoản phải từ 3-50 ký tự")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6-100 ký tự")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc")]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public UserInfo? User { get; set; }
    }

    public class RegisterResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public UserInfo? User { get; set; }
    }

    public class UserInfo
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string Role { get; set; } = string.Empty;
        public int IsActive { get; set; }
    }

    // ===== OTP + Forgot Password DTOs =====
    public class OtpInitiateResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? CorrelationId { get; set; }
        public int ExpiresInSeconds { get; set; }
    }

    public class OtpVerifyRequest
    {
        [Required]
        public string CorrelationId { get; set; } = string.Empty;
        [Required]
        public string Otp { get; set; } = string.Empty;
    }

    public class ForgotPasswordInitiateRequest
    {
        [Required]
        public string Username { get; set; } = string.Empty;
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class ForgotPasswordInitiateResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? CorrelationId { get; set; }
        public int ExpiresInSeconds { get; set; }
        public bool ShouldRegister { get; set; }
    }

    public class ForgotPasswordVerifyRequest
    {
        [Required]
        public string CorrelationId { get; set; } = string.Empty;
        [Required]
        public string Otp { get; set; } = string.Empty;
        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; } = string.Empty;
        [Required]
        [Compare("NewPassword")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }

    public class BasicResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}