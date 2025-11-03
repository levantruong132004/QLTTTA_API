using Microsoft.AspNetCore.Mvc;
using QLTTTA_WEB.Models;
using System.Diagnostics;
using System.Text.Json;

namespace QLTTTA_WEB.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IHttpClientFactory httpClientFactory, ILogger<HomeController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int? courseId)
        {
            // Kiểm tra đăng nhập
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.Username = HttpContext.Session.GetString("Username");
            ViewBag.Role = HttpContext.Session.GetString("Role") ?? string.Empty;
            var roleStr = (string)ViewBag.Role;
            bool isStaff = roleStr.Contains("NhanVienHocVu", StringComparison.OrdinalIgnoreCase) ||
                           (HttpContext.Session.GetString("RoleId") == "4");

            var client = _httpClientFactory.CreateClient("ApiClient");
            var dashboard = new HomeDashboardViewModel();

            if (isStaff)
            {
                // STAFF: hiển thị danh sách lớp theo khóa (combobox)
                try
                {
                    var crsRes = await client.GetAsync("api/courses");
                    if (crsRes.IsSuccessStatusCode)
                    {
                        var json = await crsRes.Content.ReadAsStringAsync();
                        dashboard.StaffCourses = JsonSerializer.Deserialize<List<SimpleCourseViewModel>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Load staff courses failed");
                    dashboard.StaffCourses = new();
                }

                // Chọn courseId hiện tại (query) hoặc default là course đầu tiên
                if (!courseId.HasValue && dashboard.StaffCourses.Any())
                    courseId = dashboard.StaffCourses.First().CourseId;
                dashboard.SelectedCourseId = courseId;

                if (dashboard.SelectedCourseId.HasValue)
                {
                    try
                    {
                        var clsRes = await client.GetAsync($"api/classes?courseId={dashboard.SelectedCourseId.Value}");
                        if (clsRes.IsSuccessStatusCode)
                        {
                            var json = await clsRes.Content.ReadAsStringAsync();
                            dashboard.StaffClasses = JsonSerializer.Deserialize<List<AdminClassItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Load staff classes failed for course {CourseId}", dashboard.SelectedCourseId);
                        dashboard.StaffClasses = new();
                    }
                }

                // Vẫn hiển thị số lượng khóa ở thẻ metrics trên cùng nếu muốn
                dashboard.Courses = dashboard.StaffCourses
                    .Select(c => new PublicCourseItem { CourseName = c.CourseName, Description = c.Description })
                    .ToList();

                return View(dashboard);
            }

            // HỌC VIÊN: giữ nguyên luồng cũ
            // Lấy thông tin cá nhân từ view V_THONGTIN_CANHAN_HV qua API /api/profile
            var profileRes = await client.GetAsync("api/profile");
            if (profileRes.IsSuccessStatusCode)
            {
                var json = await profileRes.Content.ReadAsStringAsync();
                dashboard.Student = JsonSerializer.Deserialize<StudentProfileViewModel>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            // Lấy danh sách khoá học công khai từ API /api/profile/courses
            var coursesRes = await client.GetAsync("api/profile/courses");
            if (coursesRes.IsSuccessStatusCode)
            {
                var json = await coursesRes.Content.ReadAsStringAsync();
                dashboard.Courses = JsonSerializer.Deserialize<List<PublicCourseItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }

            return View(dashboard);
        }

        public IActionResult Privacy()
        {
            // Kiểm tra đăng nhập
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
