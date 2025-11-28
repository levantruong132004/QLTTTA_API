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
            var roleId = HttpContext.Session.GetString("RoleId");
            // Treat Admin (roleId==5) like Staff for the class section
            bool isStaff = roleStr.Contains("NhanVienHocVu", StringComparison.OrdinalIgnoreCase) ||
                           roleStr.Contains("QuanTriVien", StringComparison.OrdinalIgnoreCase) ||
                           (roleId == "4" || roleId == "5");

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

                // Helpers
                async Task<List<AdminClassItem>> LoadClassesByCourseAsync(int cid)
                {
                    var list = new List<AdminClassItem>();
                    try
                    {
                        var clsRes = await client.GetAsync($"api/classes?courseId={cid}");
                        if (clsRes.IsSuccessStatusCode)
                        {
                            var json = await clsRes.Content.ReadAsStringAsync();
                            list = JsonSerializer.Deserialize<List<AdminClassItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Load staff classes failed for course {CourseId}", cid);
                    }
                    return list;
                }

                async Task<List<AdminClassItem>> LoadAllClassesAsync()
                {
                    var list = new List<AdminClassItem>();
                    try
                    {
                        var res = await client.GetAsync("api/classes");
                        if (res.IsSuccessStatusCode)
                        {
                            var json = await res.Content.ReadAsStringAsync();
                            list = JsonSerializer.Deserialize<List<AdminClassItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Load all classes failed");
                    }
                    return list;
                }

                if (courseId.HasValue)
                {
                    if (courseId.Value == 0)
                    {
                        // Tất cả khóa học: không lọc theo courseId
                        dashboard.SelectedCourseId = 0;
                        dashboard.StaffClasses = await LoadAllClassesAsync();
                    }
                    else
                    {
                        // Load classes for selected or first course
                        var classes = await LoadClassesByCourseAsync(courseId.Value);
                        if (classes.Count == 0 && dashboard.StaffCourses.Any())
                        {
                            // Try to find the first course that actually has classes
                            foreach (var c in dashboard.StaffCourses.Where(c => c.CourseId != courseId.Value))
                            {
                                var tryClasses = await LoadClassesByCourseAsync(c.CourseId);
                                if (tryClasses.Count > 0)
                                {
                                    dashboard.SelectedCourseId = c.CourseId;
                                    dashboard.StaffClasses = tryClasses;
                                    break;
                                }
                            }
                        }

                        if (dashboard.StaffClasses == null || dashboard.StaffClasses.Count == 0)
                        {
                            // Either we didn't find any or selected course had some
                            dashboard.SelectedCourseId = courseId;
                            dashboard.StaffClasses = classes;
                        }
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
