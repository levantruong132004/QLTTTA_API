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
                await _httpClient.PostAsync("api/auth/logout", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đăng xuất");
            }

            // Xóa session
            HttpContext.Session.Clear();

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
                        TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Vui lòng đăng nhập.";
                        return RedirectToAction("Login");
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
    }
}