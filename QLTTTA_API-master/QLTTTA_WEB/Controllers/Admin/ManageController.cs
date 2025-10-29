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
                || role.Contains("QuanTri", StringComparison.OrdinalIgnoreCase);
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

        public async Task<IActionResult> Classes(int? courseId)
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home");
            var url = "api/classes" + (courseId.HasValue ? $"?courseId={courseId}" : "");
            var res = await _http.GetAsync(url);
            var body = await res.Content.ReadAsStringAsync();
            List<QLTTTA_WEB.Models.AdminClassItem> data;
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Classes list failed: {Status} {Body}", res.StatusCode, body);
                TempData["ErrorMessage"] = !string.IsNullOrWhiteSpace(body) ? body : $"Tải danh sách lớp thất bại (HTTP {(int)res.StatusCode})";
                data = new List<QLTTTA_WEB.Models.AdminClassItem>();
            }
            else
            {
                try
                {
                    data = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminClassItem>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Deserialize classes failed. Body: {Body}", body);
                    TempData["ErrorMessage"] = "Dữ liệu lớp học trả về không hợp lệ.";
                    data = new List<QLTTTA_WEB.Models.AdminClassItem>();
                }
            }
            ViewBag.CourseId = courseId;
            return View("~/Views/Admin/Classes.cshtml", data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClass(string classCode, string className, DateTime startDate, DateTime endDate, int maxSize, int courseId, int teacherId)
        {
            if (!IsStaff()) return RedirectToAction("Classes", new { courseId });
            var payload = new { ClassCode = classCode, ClassName = className, StartDate = startDate, EndDate = endDate, MaxSize = maxSize, CourseId = courseId, TeacherId = teacherId };
            var res = await _http.PostAsync("api/classes", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = await res.Content.ReadAsStringAsync();
            return RedirectToAction("Classes", new { courseId });
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteClass(int classId, int? courseId)
        {
            if (!IsStaff()) return RedirectToAction("Classes", new { courseId });
            var res = await _http.DeleteAsync($"api/classes/{classId}");
            TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = await res.Content.ReadAsStringAsync();
            return RedirectToAction("Classes", new { courseId });
        }

        [HttpGet]
        public async Task<IActionResult> EditClassPage(int id)
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home");
            var res = await _http.GetAsync($"api/classes/{id}");
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(body) ? "Không tải được lớp" : body;
                return RedirectToAction("Classes");
            }
            try
            {
                var item = JsonSerializer.Deserialize<QLTTTA_WEB.Models.AdminClassItem>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                return View("~/Views/Admin/EditClass.cshtml", item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialize class item failed. Body: {Body}", body);
                TempData["ErrorMessage"] = "Dữ liệu lớp học không hợp lệ.";
                return RedirectToAction("Classes");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditClass(int classId, string classCode, string className, DateTime startDate, DateTime endDate, int maxSize, int courseId, int teacherId, string? status)
        {
            if (!IsStaff()) return RedirectToAction("Classes");
            var payload = new { ClassId = classId, ClassCode = classCode, ClassName = className, StartDate = startDate, EndDate = endDate, MaxSize = maxSize, CourseId = courseId, TeacherId = teacherId, Status = status };
            var res = await _http.PutAsync($"api/classes/{classId}", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = await res.Content.ReadAsStringAsync();
            if (res.IsSuccessStatusCode) return RedirectToAction("Classes");
            return RedirectToAction("EditClassPage", new { id = classId });
        }

        public async Task<IActionResult> Schedules(int classId)
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home");
            var res = await _http.GetAsync($"api/schedules/by-class/{classId}");
            var body = await res.Content.ReadAsStringAsync();
            List<QLTTTA_WEB.Models.ScheduleItem> data;
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Schedules list failed: {Status} {Body}", res.StatusCode, body);
                TempData["ErrorMessage"] = !string.IsNullOrWhiteSpace(body) ? body : $"Tải lịch học thất bại (HTTP {(int)res.StatusCode})";
                data = new List<QLTTTA_WEB.Models.ScheduleItem>();
            }
            else
            {
                try
                {
                    data = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.ScheduleItem>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Deserialize schedules failed. Body: {Body}", body);
                    TempData["ErrorMessage"] = "Dữ liệu lịch học trả về không hợp lệ.";
                    data = new List<QLTTTA_WEB.Models.ScheduleItem>();
                }
            }
            ViewBag.ClassId = classId;
            return View("~/Views/Admin/Schedules.cshtml", data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSchedule(int classId, int dayOfWeek, string startTime, string endTime)
        {
            if (!IsStaff()) return RedirectToAction("Schedules", new { classId });
            // Ensure HH:mm format
            var payload = new { ClassId = classId, DayOfWeek = dayOfWeek, StartTime = startTime, EndTime = endTime };
            var res = await _http.PostAsync("api/schedules", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = await res.Content.ReadAsStringAsync();
            return RedirectToAction("Schedules", new { classId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSchedule(int scheduleId, int classId, int dayOfWeek, string startTime, string endTime)
        {
            if (!IsStaff()) return RedirectToAction("Schedules", new { classId });
            var payload = new { ScheduleId = scheduleId, ClassId = classId, DayOfWeek = dayOfWeek, StartTime = startTime, EndTime = endTime };
            var res = await _http.PutAsync($"api/schedules/{scheduleId}", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = await res.Content.ReadAsStringAsync();
            return RedirectToAction("Schedules", new { classId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSchedule(int scheduleId, int classId)
        {
            if (!IsStaff()) return RedirectToAction("Schedules", new { classId });
            var res = await _http.DeleteAsync($"api/schedules/{scheduleId}");
            TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = await res.Content.ReadAsStringAsync();
            return RedirectToAction("Schedules", new { classId });
        }

        public async Task<IActionResult> Registrations(string? status, int? classId)
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home");
            var url = "api/registrations" + (status != null || classId != null ? "?" : "") + (status != null ? $"status={Uri.EscapeDataString(status)}" : "") + (status != null && classId != null ? "&" : "") + (classId != null ? $"classId={classId}" : "");
            var res = await _http.GetAsync(url);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Registrations list failed: {Status} {Body}", res.StatusCode, body);
                TempData["ErrorMessage"] = !string.IsNullOrWhiteSpace(body) ? body : $"Tải danh sách đăng ký thất bại (HTTP {(int)res.StatusCode})";
                return View("~/Views/Admin/Registrations.cshtml", new List<QLTTTA_WEB.Models.AdminRegistrationItem>());
            }
            try
            {
                var data = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminRegistrationItem>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                return View("~/Views/Admin/Registrations.cshtml", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialize registrations failed. Body: {Body}", body);
                TempData["ErrorMessage"] = "Dữ liệu đăng ký trả về không hợp lệ.";
                return View("~/Views/Admin/Registrations.cshtml", new List<QLTTTA_WEB.Models.AdminRegistrationItem>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRegistration(int id, int? classId)
        {
            if (!IsStaff()) return RedirectToAction("Registrations");
            var url = $"api/registrations/{id}/approve" + (classId.HasValue ? $"?classId={classId}" : "");
            var res = await _http.PostAsync(url, new StringContent("", Encoding.UTF8, "application/json"));
            TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = await res.Content.ReadAsStringAsync();
            return RedirectToAction("Registrations");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRegistration(int id)
        {
            if (!IsStaff()) return RedirectToAction("Registrations");
            var url = $"api/registrations/{id}/reject";
            var res = await _http.PostAsync(url, new StringContent("", Encoding.UTF8, "application/json"));
            TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = await res.Content.ReadAsStringAsync();
            return RedirectToAction("Registrations");
        }
    }
}
