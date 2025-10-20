using Microsoft.AspNetCore.Mvc;
using QLTTTA_WEB.Models;
using System.Text;
using System.Text.Json;

namespace QLTTTA_WEB.Controllers
{
    public class StudentController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<StudentController> _logger;

        public StudentController(IHttpClientFactory httpClientFactory, ILogger<StudentController> logger)
        {
            _httpClient = httpClientFactory.CreateClient("ApiClient");
            _logger = logger;
        }

        // Quyền xem/chỉnh sửa sẽ do database (VIEW + quyền UPDATE) kiểm soát theo user đang kết nối.
        // Chỉ cần đảm bảo đã đăng nhập (có session UserId) ở tầng web.

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            var res = await _httpClient.GetAsync("api/profile");
            if (!res.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = "Không tải được thông tin cá nhân";
                return View(new StudentProfileViewModel());
            }
            var json = await res.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<StudentProfileViewModel>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new StudentProfileViewModel();
            return View(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(StudentProfileViewModel model)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            var req = new StudentProfileUpdateRequest
            {
                // Chỉ gửi các trường được phép chỉnh sửa
                SoDienThoai = model.SoDienThoai,
                DiaChi = model.DiaChi
            };
            var body = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");
            var res = await _httpClient.PutAsync("api/profile", body);
            if (res.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Cập nhật thông tin thành công";
            }
            else
            {
                TempData["ErrorMessage"] = "Cập nhật thất bại";
            }
            return RedirectToAction("Profile");
        }

        [HttpGet]
        public async Task<IActionResult> Courses()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            var res = await _httpClient.GetAsync("api/profile/courses");
            var list = new List<PublicCourseItem>();
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                list = JsonSerializer.Deserialize<List<PublicCourseItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> OpenClasses(string courseCode)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");
            if (string.IsNullOrWhiteSpace(courseCode)) return RedirectToAction("Courses");

            var res = await _httpClient.GetAsync($"api/profile/open-classes/{Uri.EscapeDataString(courseCode)}");
            var json = await res.Content.ReadAsStringAsync();
            List<QLTTTA_WEB.Models.OpenClassItem> data;
            if (!res.IsSuccessStatusCode)
            {
                string msg = "Không tải được danh sách lớp";
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("message", out var m))
                        {
                            var s = m.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) msg = s!;
                        }
                        else
                        {
                            msg = json;
                        }
                    }
                    catch
                    {
                        msg = json;
                    }
                }
                TempData["ErrorMessage"] = msg;
                data = new List<QLTTTA_WEB.Models.OpenClassItem>();
            }
            else
            {
                try
                {
                    data = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.OpenClassItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Deserialize OpenClasses failed. Body: {Body}", json);
                    TempData["ErrorMessage"] = "Dữ liệu lớp mở trả về không hợp lệ.";
                    data = new List<QLTTTA_WEB.Models.OpenClassItem>();
                }
            }
            ViewBag.CourseCode = courseCode;
            return View("OpenClasses", data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterClass(int classId, string? courseCode)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");
            try
            {
                var payload = new { ClassId = classId };
                var res = await _httpClient.PostAsync("api/profile/register-class", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
                var body = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                var success = doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
                var message = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
                TempData[success ? "SuccessMessage" : "ErrorMessage"] = string.IsNullOrWhiteSpace(message) ? (success ? "Đăng ký thành công" : "Đăng ký thất bại") : message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RegisterClass error {ClassId}", classId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi đăng ký lớp";
            }
            if (!string.IsNullOrWhiteSpace(courseCode))
                return RedirectToAction("OpenClasses", new { courseCode });
            return RedirectToAction("Courses");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterCourse(string courseCode)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");
            try
            {
                var payload = new { courseCode };
                var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync("api/profile/register-course", body);
                var txt = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(txt);
                var success = doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
                var message = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
                if (success)
                {
                    TempData["SuccessMessage"] = string.IsNullOrWhiteSpace(message) ? $"Đã gửi yêu cầu đăng ký khóa {courseCode}." : message;
                }
                else
                {
                    TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(message) ? "Gửi yêu cầu đăng ký thất bại" : message;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RegisterCourse error for {CourseCode}", courseCode);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi gửi yêu cầu đăng ký";
            }
            return RedirectToAction("Courses");
        }
    }
}
