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
        public IActionResult Login(string? returnUrl)
        {
            // Kiểm tra nếu đã đăng nhập rồi thì redirect về trang chính
            if (HttpContext.Session.GetString("UserId") != null)
            {
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Home");
            }

            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? null : returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl)
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
                        HttpContext.Session.SetString("RoleId", loginResponse.User.RoleId.ToString());
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
                        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }
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

            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? null : returnUrl;
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            try
            {
                // Gọi API logout nếu cần
                var username = HttpContext.Session.GetString("Username") ?? string.Empty;
                var sid = Request.Cookies["SessionId"];
                var url = $"api/auth/logout?username={Uri.EscapeDataString(username)}";
                var req = new HttpRequestMessage(HttpMethod.Post, url);
                if (!string.IsNullOrEmpty(sid))
                {
                    req.Headers.Add("X-Session-Id", sid);
                }
                await _httpClient.SendAsync(req);
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

                _logger.LogInformation("Gọi API initiate register OTP với data: {Json}", json);

                var response = await _httpClient.PostAsync("api/auth/register/initiate-otp", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("API response status: {StatusCode}, content: {Content}", response.StatusCode, responseContent);

                var otpRes = JsonSerializer.Deserialize<OtpInitiateResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (response.IsSuccessStatusCode && otpRes?.Success == true && !string.IsNullOrEmpty(otpRes.CorrelationId))
                {
                    TempData["InfoMessage"] = "Mã OTP đã được gửi tới email. Vui lòng kiểm tra và nhập OTP.";
                    return RedirectToAction("RegisterVerify", new { correlationId = otpRes.CorrelationId });
                }

                ModelState.AddModelError("", otpRes?.Message ?? "Không thể gửi OTP đăng ký");
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
        public IActionResult RegisterVerify(string correlationId)
        {
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                TempData["ErrorMessage"] = "Thiếu thông tin xác thực";
                return RedirectToAction("Register");
            }
            var vm = new RegisterVerifyViewModel { CorrelationId = correlationId };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterVerify(RegisterVerifyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            try
            {
                var payload = new
                {
                    correlationId = model.CorrelationId,
                    otp = model.Otp
                };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/auth/register/verify-otp", content);
                var body = await response.Content.ReadAsStringAsync();
                var res = JsonSerializer.Deserialize<RegisterApiResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (response.IsSuccessStatusCode && res?.Success == true)
                {
                    TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng đăng nhập.";
                    return RedirectToAction("Login");
                }
                ModelState.AddModelError("", res?.Message ?? "Xác thực OTP thất bại");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RegisterVerify error");
                ModelState.AddModelError("", "Có lỗi xảy ra khi xác thực OTP");
            }
            return View(model);
        }

        // ===== Forgot password =====
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
            {
                return View(model);
            }

            try
            {
                var payload = new { username = model.Username, email = model.Email };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/auth/forgot/initiate", content);
                var body = await response.Content.ReadAsStringAsync();
                var res = JsonSerializer.Deserialize<OtpInitiateResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (response.IsSuccessStatusCode && res != null && res.Success)
                {
                    TempData["InfoMessage"] = "Đã gửi OTP đến email. Vui lòng kiểm tra và nhập OTP để đặt lại mật khẩu.";
                    return RedirectToAction("ForgotVerify", new { correlationId = res.CorrelationId });
                }

                if (res != null && res.ShouldRegister)
                {
                    TempData["ErrorMessage"] = res.Message;
                    return RedirectToAction("Register");
                }

                ModelState.AddModelError("", res?.Message ?? "Không thể gửi OTP đặt lại mật khẩu");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ForgotPassword error");
                ModelState.AddModelError("", "Có lỗi xảy ra khi gửi yêu cầu");
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotVerify(string correlationId)
        {
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                TempData["ErrorMessage"] = "Thiếu thông tin xác thực";
                return RedirectToAction("ForgotPassword");
            }
            return View(new ForgotVerifyViewModel { CorrelationId = correlationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotVerify(ForgotVerifyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            try
            {
                var payload = new
                {
                    correlationId = model.CorrelationId,
                    otp = model.Otp,
                    newPassword = model.NewPassword,
                    confirmNewPassword = model.ConfirmNewPassword
                };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/auth/forgot/verify", content);
                var body = await response.Content.ReadAsStringAsync();
                var res = JsonSerializer.Deserialize<BasicResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (response.IsSuccessStatusCode && res?.Success == true)
                {
                    TempData["SuccessMessage"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập!";
                    return RedirectToAction("Login");
                }
                ModelState.AddModelError("", res?.Message ?? "Xác thực OTP thất bại");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ForgotVerify error");
                ModelState.AddModelError("", "Có lỗi xảy ra khi xác thực OTP");
            }
            return View(model);
        }
    }
}