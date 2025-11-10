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
            var username = HttpContext.Session.GetString("Username") ?? string.Empty;
            if (username.Equals("QLTTTA_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                username.Equals("QLTTA_ADMIN", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
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

        public async Task<IActionResult> Classes(int? courseId, string? search)
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home");
            var qs = new List<string>();
            if (courseId.HasValue) qs.Add($"courseId={courseId}");
            if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
            var url = "api/classes" + (qs.Count>0? ("?"+string.Join("&", qs)) : "");
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
            ViewBag.Search = search;
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
            
            // MẶC ĐỊNH: Nếu không truyền status, tự động lọc "Chờ duyệt"
            if (string.IsNullOrWhiteSpace(status))
            {
                status = "Chờ duyệt";
            }
            
            try
            {
                var url = "api/registrations" + (status != null || classId != null ? "?" : "") + (status != null ? $"status={Uri.EscapeDataString(status)}" : "") + (status != null && classId != null ? "&" : "") + (classId != null ? $"classId={classId}" : "");
                var res = await _http.GetAsync(url);
                var body = await res.Content.ReadAsStringAsync();
                
                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Registrations list failed: {Status} {Body}", res.StatusCode, body);
                    
                    // Trích xuất message lỗi gọn gàng
                    string errorMsg = "Không thể tải danh sách đăng ký";
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("message", out var msgProp))
                        {
                            errorMsg = msgProp.GetString() ?? errorMsg;
                        }
                    }
                    catch
                    {
                        // Nếu không parse được JSON, cắt message ngắn lại
                        if (body.Length > 200)
                        {
                            errorMsg = body.Substring(0, 200) + "...";
                        }
                        else
                        {
                            errorMsg = body;
                        }
                    }
                    
                    TempData["ErrorMessage"] = errorMsg;
                    ViewBag.CurrentStatus = status;
                    ViewBag.CurrentClassId = classId;
                    return View("~/Views/Admin/Registrations.cshtml", new List<QLTTTA_WEB.Models.AdminRegistrationItem>());
                }
                
                var data = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminRegistrationItem>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                
                // Truyền status hiện tại vào ViewBag để form lọc biết
                ViewBag.CurrentStatus = status;
                ViewBag.CurrentClassId = classId;
                
                return View("~/Views/Admin/Registrations.cshtml", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registrations page error");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải danh sách đăng ký. Vui lòng thử lại.";
                ViewBag.CurrentStatus = status;
                ViewBag.CurrentClassId = classId;
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
            
            // Redirect về trang Registrations với filter "Chờ duyệt"
            return RedirectToAction("Registrations", new { status = "Chờ duyệt" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRegistration(int id)
        {
            if (!IsStaff()) return RedirectToAction("Registrations");
            var url = $"api/registrations/{id}/reject";
            var res = await _http.PostAsync(url, new StringContent("", Encoding.UTF8, "application/json"));
            TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = await res.Content.ReadAsStringAsync();
            
            // Redirect về trang Registrations với filter "Chờ duyệt"
            return RedirectToAction("Registrations", new { status = "Chờ duyệt" });
        }

        [HttpGet]
        public IActionResult Invoices()
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home");
            return View("~/Views/Admin/Invoices.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateCenterKeyPair(string centerName, string? address, string? phone, int accountantId)
        {
            if (!IsStaff()) return RedirectToAction("Invoices");
            try
            {
                var payload = new { AccountantId = accountantId, CenterName = centerName, Address = address, Phone = phone };
                var res = await _http.PostAsync("api/digitalsignature/generate-center-keypair/save",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
                var body = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Đã tạo và lưu public key trung tâm. Private key đã được gửi email cho kế toán.";
                }
                else
                {
                    string msg = "Không thể tạo khóa trung tâm";
                    try
                    {
                        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                        if (doc.RootElement.TryGetProperty("message", out var m) && !string.IsNullOrWhiteSpace(m.GetString()))
                            msg = m.GetString()!;
                    } catch { msg = body; }
                    TempData["ErrorMessage"] = msg;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateCenterKeyPair failed");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tạo khóa trung tâm";
            }
            return RedirectToAction("Invoices");
        }

        [HttpGet]
        public IActionResult Staff()
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home");
            return View("~/Views/Admin/Staff.cshtml");
        }
    }
}
