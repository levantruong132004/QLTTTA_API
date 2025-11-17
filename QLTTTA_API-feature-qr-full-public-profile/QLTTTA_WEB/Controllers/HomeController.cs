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
            var response = await client.GetAsync("api/courses");
            List<SimpleCourseViewModel> courses = new();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                courses = JsonSerializer.Deserialize<List<SimpleCourseViewModel>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<SimpleCourseViewModel>();
            }

            return View(courses);
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
