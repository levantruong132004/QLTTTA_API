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

        public async Task<IActionResult> Index()
        {
            // Kiểm tra đăng nhập
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.Username = HttpContext.Session.GetString("Username");
            ViewBag.Role = HttpContext.Session.GetString("Role");

            var client = _httpClientFactory.CreateClient("ApiClient");
            var dashboard = new HomeDashboardViewModel();

            // Lấy thông tin cá nhân từ view V_THONGTIN_CANHAN_HV qua API /api/profile
            var profileRes = await client.GetAsync("api/profile");
            if (profileRes.IsSuccessStatusCode)
            {
                var json = await profileRes.Content.ReadAsStringAsync();
                dashboard.Student = JsonSerializer.Deserialize<StudentProfileViewModel>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            // Lấy danh sách khoá học công khai từ API /api/profile/courses (dựa trên V_DANHSACH_KHOAHOC)
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
