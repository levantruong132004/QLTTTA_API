using Microsoft.AspNetCore.Mvc;
using QLTTTA_WEB.Models;
using System.Text;
using System.Text.Json;

namespace QLTTTA_WEB.Controllers
{
    public class AccountController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AccountController> _logger;

        public AccountController(IHttpClientFactory httpClientFactory, ILogger<AccountController> logger)
        {
            _httpClient = httpClientFactory.CreateClient("ApiClient");
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login()
        {
            // Kiểm tra nếu đã đăng nhập rồi thì redirect về trang chính
            if (HttpContext.Session.GetString("UserId") != null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var loginRequest = new LoginApiRequest
                {
                    Username = model.Username,
                    Password = model.Password
                };

                var json = JsonSerializer.Serialize(loginRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("api/auth/login", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Login API raw status: {StatusCode}, body: {Body}", response.StatusCode, responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = JsonSerializer.Deserialize<LoginApiResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (loginResponse?.Success == true && loginResponse.User != null)
                    {
                        // Lưu thông tin user vào session
                        HttpContext.Session.SetString("UserId", loginResponse.User.UserId.ToString());
                        HttpContext.Session.SetString("Username", loginResponse.User.Username);
                        HttpContext.Session.SetString("Email", loginResponse.User.Email);
                        HttpContext.Session.SetString("Role", loginResponse.User.Role);
                        HttpContext.Session.SetString("Token", loginResponse.Token);

                        // Lưu SessionId vào cookie cho short polling kiểm tra đăng nhập đồng thời
                        if (!string.IsNullOrEmpty(loginResponse.SessionId))
                        {
                            Response.Cookies.Append("SessionId", loginResponse.SessionId, new CookieOptions
                            {
                                HttpOnly = false,
                                SameSite = SameSiteMode.Lax,
                                Secure = false,
                                Expires = DateTimeOffset.UtcNow.AddHours(1)
                            });
                        }

                        TempData["SuccessMessage"] = "Đăng nhập thành công!";
                        return RedirectToAction("Index", "Home");
                    }
                }

                try
                {
                    var errorResponse = JsonSerializer.Deserialize<LoginApiResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    ModelState.AddModelError("", errorResponse?.Message ?? $"Đăng nhập thất bại (HTTP {response.StatusCode})");
                }
                catch (Exception deserEx)
                {
                    _logger.LogError(deserEx, "Deserialize login error body failed. Raw body: {Body}", responseContent);
                    ModelState.AddModelError("", $"Đăng nhập thất bại và không phân tích được phản hồi (HTTP {response.StatusCode})");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đăng nhập - request Username={Username}", model?.Username);
                ModelState.AddModelError("", $"Có lỗi xảy ra trong quá trình đăng nhập: {ex.Message}");
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            try
            {
                // Gọi API logout nếu cần
                var sid = Request.Cookies["SessionId"];
                if (!string.IsNullOrEmpty(sid))
                {
                    var req = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
                    req.Headers.Add("X-Session-Id", sid);
                    await _httpClient.SendAsync(req);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đăng xuất");
            }

            // Xóa session
            HttpContext.Session.Clear();
            // Xóa cookie
            Response.Cookies.Delete("SessionId");

            TempData["InfoMessage"] = "Bạn đã đăng xuất thành công!";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Register()
        {
            // Kiểm tra nếu đã đăng nhập rồi thì redirect về trang chính
            if (HttpContext.Session.GetString("UserId") != null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            _httpClient.Timeout = TimeSpan.FromSeconds(30); // Giới hạn thời gian gửi request

            try
            {
                _logger.LogInformation("Bắt đầu đăng ký user: {Username}, Email: {Email}", model.Username, model.Email);

                var registerRequest = new RegisterApiRequest
                {
                    FullName = model.FullName,
                    Sex = model.Sex,
                    DateOfBirth = model.DateOfBirth ?? DateTime.Now.AddYears(-18), // Default nếu null
                    PhoneNumber = model.PhoneNumber,
                    Email = model.Email,
                    Address = model.Address,
                    Username = model.Username,
                    Password = model.Password,
                    ConfirmPassword = model.ConfirmPassword
                };

                var json = JsonSerializer.Serialize(registerRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation("Gọi API register với data: {Json}", json);

                var response = await _httpClient.PostAsync("api/auth/register", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("API response status: {StatusCode}, content: {Content}", response.StatusCode, responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var registerResponse = JsonSerializer.Deserialize<RegisterApiResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (registerResponse?.Success == true)
                    {
                        // Điều hướng sang trang nhập OTP
                        return RedirectToAction("VerifyOtp", new { username = model.Username, infoMessage = "Một mã OTP đã được gửi đến email của bạn." });
                    }
                }

                var errorResponse = JsonSerializer.Deserialize<RegisterApiResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                ModelState.AddModelError("", errorResponse?.Message ?? "Đăng ký thất bại");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đăng ký - Username: {Username}, ErrorMessage: {Message}",
                    model.Username, ex.Message);
                ModelState.AddModelError("", $"Có lỗi xảy ra trong quá trình đăng ký: {ex.Message}");
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult VerifyOtp(string username, string? infoMessage = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToAction("Register");

            var vm = new VerifyOtpViewModel { Username = username, InfoMessage = infoMessage };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var req = new VerifyOtpRequest { Username = model.Username, Otp = model.Otp };
                var json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/auth/verify-otp", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                var api = JsonSerializer.Deserialize<OtpResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (response.IsSuccessStatusCode && api?.Success == true)
                {
                    TempData["SuccessMessage"] = "Xác thực thành công. Đăng ký hoàn tất. Vui lòng đăng nhập.";
                    return RedirectToAction("Login");
                }

                ModelState.AddModelError("", api?.Message ?? "Xác thực OTP thất bại");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyOtp error for user {Username}", model.Username);
                ModelState.AddModelError("", "Có lỗi xảy ra khi xác thực OTP");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtp(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToAction("Register");

            try
            {
                var req = new ResendOtpRequest { Username = username };
                var json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/auth/resend-otp", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                var api = JsonSerializer.Deserialize<OtpResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var info = (response.IsSuccessStatusCode && api?.Success == true)
                    ? "Đã gửi lại OTP đến email của bạn."
                    : (api?.Message ?? "Gửi lại OTP thất bại");

                return RedirectToAction("VerifyOtp", new { username, infoMessage = info });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ResendOtp error for user {Username}", username);
                return RedirectToAction("VerifyOtp", new { username, infoMessage = "Có lỗi khi gửi lại OTP" });
            }
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var req = new ForgotPasswordApiRequest { Username = model.Username, Email = model.Email };
                var json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp = await _httpClient.PostAsync("api/auth/forgot-password", content);
                var body = await resp.Content.ReadAsStringAsync();

                OtpResponse? api = null;
                try { api = JsonSerializer.Deserialize<OtpResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch { }

                if (resp.IsSuccessStatusCode && api?.Success == true)
                {
                    return RedirectToAction("ResetPassword", new { username = model.Username, infoMessage = "Đã gửi OTP xác thực đến email của bạn." });
                }

                var msg = api?.Message ?? (string.IsNullOrWhiteSpace(body) ? "Không thể xử lý yêu cầu." : body);
                if (msg.Contains("Email chưa được đăng ký", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "Tài Khoản Chưa Được Đăng Ký. Vui Lòng Đăng Ký Tài Khoản!";
                    return RedirectToAction("Register");
                }

                ModelState.AddModelError(string.Empty, msg);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ForgotPassword error for user {Username}", model.Username);
                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra. Vui lòng thử lại sau.");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult ResetPassword(string? username = null, string? infoMessage = null)
        {
            var vm = new ResetPasswordViewModel { Username = username ?? string.Empty, InfoMessage = infoMessage };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var req = new ResetPasswordApiRequest { Username = model.Username, Otp = model.Otp, NewPassword = model.NewPassword };
                var json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp = await _httpClient.PostAsync("api/auth/reset-password", content);
                var body = await resp.Content.ReadAsStringAsync();
                var api = JsonSerializer.Deserialize<OtpResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (resp.IsSuccessStatusCode && api?.Success == true)
                {
                    TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập.";
                    return RedirectToAction("Login");
                }

                ModelState.AddModelError(string.Empty, api?.Message ?? "Đặt lại mật khẩu thất bại");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ResetPassword error for user {Username}", model.Username);
                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra. Vui lòng thử lại sau.");
                return View(model);
            }
        }
    }
}