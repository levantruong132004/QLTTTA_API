using Microsoft.AspNetCore.Mvc;
using QLTTTA_WEB.Models;
using System.Diagnostics;
using System.Text.Json;
namespace QLTTTA_WEB.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public HomeController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> Index()
        {
            var client = _httpClientFactory.CreateClient("ApiClient");
            var response = await client.GetAsync("api/courses");
            List<CourseViewModel> courses = new();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                // Định nghĩa một class CourseViewModel để hứng dữ liệu
                courses = JsonSerializer.Deserialize<List<CourseViewModel>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return View(courses); // Truyền danh sách khóa học sang View
        }
    }
}
