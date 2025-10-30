using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace QLTTTA_WEB.Controllers.Admin
{
    public class ManageController : Controller
    {
        private readonly HttpClient _http;
        private readonly ILogger<ManageController> _logger;
        public ManageController(IHttpClientFactory factory, ILogger<ManageController> logger)
        { _http = factory.CreateClient("ApiClient"); _logger = logger; }

        private bool IsStaff()
        {
            var role = HttpContext.Session.GetString("Role") ?? string.Empty;
            var roleIdStr = HttpContext.Session.GetString("RoleId");
            int.TryParse(roleIdStr, out var roleId);
            return roleId == 4
                || role.Contains("NhanVienHocVu", StringComparison.OrdinalIgnoreCase)
                || role.Contains("QuanTri", StringComparison.OrdinalIgnoreCase)
                || role.Contains("Admin", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<IActionResult> Courses()
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home");
            var res = await _http.GetAsync("api/courses");
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Courses list failed: {Status} {Body}", res.StatusCode, body);
                TempData["ErrorMessage"] = !string.IsNullOrWhiteSpace(body) ? body : $"Tải danh sách khóa học thất bại (HTTP {(int)res.StatusCode})";
                return View("~/Views/Admin/Courses.cshtml", new List<QLTTTA_WEB.Models.CourseViewModel>());
            }
            try
            {
                var data = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.CourseViewModel>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                return View("~/Views/Admin/Courses.cshtml", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialize courses failed. Body: {Body}", body);
                TempData["ErrorMessage"] = "Dữ liệu trả về không hợp lệ.";
                return View("~/Views/Admin/Courses.cshtml", new List<QLTTTA_WEB.Models.CourseViewModel>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCourse(int courseId, string courseCode, string courseName, string? description, int standardFee)
        {
            if (!IsStaff()) return RedirectToAction("Courses");
            var payload = new { CourseId = courseId, CourseCode = courseCode, CourseName = courseName, Description = description, StandardFee = standardFee };
            var res = await _http.PutAsync($"api/courses/{courseId}", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = await res.Content.ReadAsStringAsync();
            return RedirectToAction("Courses");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCourse(int courseId)
        {
            if (!IsStaff()) return RedirectToAction("Courses");
            var res = await _http.DeleteAsync($"api/courses/{courseId}");
            TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = await res.Content.ReadAsStringAsync();
            return RedirectToAction("Courses");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCourse(string courseCode, string courseName, string description, int standardFee)
        {
            if (!IsStaff()) return RedirectToAction("Courses");
            var payload = new { CourseCode = courseCode, CourseName = courseName, Description = description, StandardFee = standardFee };
            var res = await _http.PostAsync("api/courses", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = await res.Content.ReadAsStringAsync();
            return RedirectToAction("Courses");
        }
    }
}
