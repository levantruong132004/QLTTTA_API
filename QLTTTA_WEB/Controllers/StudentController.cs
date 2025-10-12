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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegisterCourse(string courseCode)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");
            // Placeholder: gọi SP/endpoint đăng ký ở API khi có
            TempData["SuccessMessage"] = $"Đã gửi yêu cầu đăng ký khóa {courseCode}.";
            return RedirectToAction("Courses");
        }
    }
}
