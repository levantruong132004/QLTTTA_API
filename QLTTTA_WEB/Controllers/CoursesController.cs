using Microsoft.AspNetCore.Mvc;
using QLTTTA_WEB.Models;
using System.Text.Json;
using System.Text;

namespace QLTTTA_WEB.Controllers
{
    public class CoursesController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CoursesController> _logger;
        public CoursesController(IHttpClientFactory httpClientFactory, ILogger<CoursesController> logger)
        {
            _httpClient = httpClientFactory.CreateClient("ApiClient");
            _logger = logger;
        }

        private bool CheckAuthentication() => !string.IsNullOrEmpty(HttpContext.Session.GetString("UserId"));

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!CheckAuthentication()) return RedirectToAction("Login", "Account");
            List<CourseViewModel> courses = new();
            try
            {
                var res = await _httpClient.GetAsync("api/courses");
                var body = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                {
                    courses = JsonSerializer.Deserialize<List<CourseViewModel>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                else
                {
                    _logger.LogWarning("Load courses failed: {Status} {Body}", res.StatusCode, body);
                    TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(body) ? $"Không tải được danh sách khóa học (HTTP {(int)res.StatusCode})" : body;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Load courses error");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải danh sách khóa học";
            }
            return View(courses);
        }

        // AJAX: lấy danh sách lớp đang mở theo mã khóa học
        [HttpGet]
        public async Task<IActionResult> OpenClasses([FromQuery] string courseCode)
        {
            if (!CheckAuthentication()) return Unauthorized();
            if (string.IsNullOrWhiteSpace(courseCode)) return BadRequest(new { success = false, message = "Thiếu mã khóa học" });
            try
            {
                var res = await _httpClient.GetAsync($"api/profile/open-classes/{Uri.EscapeDataString(courseCode)}");
                var body = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                {
                    return StatusCode((int)res.StatusCode, new { success = false, message = body });
                }
                // Trả nguyên dữ liệu từ API (list OpenClassItem)
                return Content(body, "application/json", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenClasses error for {CourseCode}", courseCode);
                return StatusCode(500, new { success = false, message = "Có lỗi khi tải lớp mở tuyển" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterClass([FromForm] int classId, [FromForm] string returnCourseCode)
        {
            if (!CheckAuthentication()) return RedirectToAction("Login", "Account");
            if (classId <= 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn lớp để đăng ký";
                return RedirectToAction(nameof(Index));
            }
            try
            {
                var payload = JsonSerializer.Serialize(new { ClassId = classId });
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync("api/profile/register-class", content);
                var body = await res.Content.ReadAsStringAsync();
                bool ok = res.IsSuccessStatusCode;
                try
                {
                    var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("Success", out var s)) ok = s.GetBoolean();
                    var msg = doc.RootElement.TryGetProperty("Message", out var m) ? (m.GetString() ?? "") : (ok ? "Đăng ký thành công" : "Đăng ký thất bại");
                    TempData[ok ? "SuccessMessage" : "ErrorMessage"] = msg;
                }
                catch
                {
                    TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok ? "Đăng ký thành công" : $"Đăng ký thất bại (HTTP {(int)res.StatusCode})";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RegisterClass error for class {ClassId}", classId);
                TempData["ErrorMessage"] = "Có lỗi khi gửi đăng ký";
            }
            // Sau đăng ký, quay về danh sách khóa học; có thể cuộn/đánh dấu lại bằng mã khóa
            return RedirectToAction(nameof(Index), new { code = returnCourseCode });
        }
    }
}
