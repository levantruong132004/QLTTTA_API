using Microsoft.AspNetCore.Mvc;
using QLTTTA_WEB.Models;
using System.Text.Json;

namespace QLTTTA_WEB.Controllers.StudentArea
{
    public class MyRegistrationsController : Controller
    {
        private readonly HttpClient _http;
        private readonly ILogger<MyRegistrationsController> _logger;
        public MyRegistrationsController(IHttpClientFactory factory, ILogger<MyRegistrationsController> logger)
        { _http = factory.CreateClient("ApiClient"); _logger = logger; }

        private bool IsStudent() => (HttpContext.Session.GetString("Role") ?? "").Contains("HocVien", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(HttpContext.Session.GetString("Role"));

        public async Task<IActionResult> Index()
        {
            if (!IsStudent()) return RedirectToAction("Index", "Home");
            var res = await _http.GetAsync("api/registrations/my");
            var json = await res.Content.ReadAsStringAsync();
            var data = new List<StudentMyRegistrationItem>();
            try
            {
                data = JsonSerializer.Deserialize<List<StudentMyRegistrationItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialize my registrations failed. Body: {Body}", json);
            }
            return View("~/Views/StudentArea/Index.cshtml", data);
        }
    }
}
