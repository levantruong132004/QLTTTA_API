using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using QRCoder;

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
                username.Equals("QLTTA_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                username.Equals("QLTT_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                username.Equals("qltt_admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return roleId == 4
                || role.Contains("NhanVienHocVu", StringComparison.OrdinalIgnoreCase)
                || role.Contains("QuanTri", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsAdmin()
        {
            var role = HttpContext.Session.GetString("Role") ?? string.Empty;
            var roleIdStr = HttpContext.Session.GetString("RoleId");
            int.TryParse(roleIdStr, out var roleId);
            var username = HttpContext.Session.GetString("Username") ?? string.Empty;
            if (username.Equals("QLTTTA_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                username.Equals("QLTTA_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                username.Equals("QLTT_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                username.Equals("qltt_admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return roleId == 5
                || role.Contains("QuanTri", StringComparison.OrdinalIgnoreCase)
                || role.Contains("Admin", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<IActionResult> Courses()
        {
            if (!IsStaff())
            {
                var ru = Url.Content("~" + Request.Path + Request.QueryString);
                return RedirectToAction("Login", "Account", new { returnUrl = ru });
            }
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

        [HttpGet]
        public IActionResult CreateCourse()
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home");
            return View("~/Views/Admin/CreateCourse.cshtml");
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

        [HttpGet]
        public async Task<IActionResult> EditCoursePage(int id)
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home");
            var res = await _http.GetAsync($"api/courses/{id}");
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(body) ? "Không tải được khóa học" : body;
                return RedirectToAction("Courses");
            }
            try
            {
                var item = JsonSerializer.Deserialize<QLTTTA_WEB.Models.CourseViewModel>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                return View("~/Views/Admin/EditCourse.cshtml", item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialize course item failed. Body: {Body}", body);
                TempData["ErrorMessage"] = "Dữ liệu khóa học không hợp lệ.";
                return RedirectToAction("Courses");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ClassRoster(int id)
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home");
            
            // Get Class Info for Title
            string className = "Lớp học";
            var clsRes = await _http.GetAsync($"api/classes/{id}");
            if (clsRes.IsSuccessStatusCode)
            {
                var clsBody = await clsRes.Content.ReadAsStringAsync();
                try {
                    var cls = JsonSerializer.Deserialize<QLTTTA_WEB.Models.AdminClassItem>(clsBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (cls != null) className = cls.ClassName;
                } catch { }
            }
            ViewBag.ClassName = className;

            var res = await _http.GetAsync($"api/classes/{id}/paid-roster");
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(body) ? "Không tải được danh sách học viên" : body;
                return View("~/Views/Admin/ClassRoster.cshtml", new List<QLTTTA_WEB.Models.RosterStudentItem>());
            }
            try
            {
                var list = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.RosterStudentItem>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                return View("~/Views/Admin/ClassRoster.cshtml", list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialize roster failed. Body: {Body}", body);
                TempData["ErrorMessage"] = "Dữ liệu danh sách học viên không hợp lệ.";
                return View("~/Views/Admin/ClassRoster.cshtml", new List<QLTTTA_WEB.Models.RosterStudentItem>());
            }
        }

        public async Task<IActionResult> Classes(int? courseId, string? search)
        {
            if (!IsStaff())
            {
                var ru = Url.Content("~" + Request.Path + Request.QueryString);
                return RedirectToAction("Login", "Account", new { returnUrl = ru });
            }
            var qs = new List<string>();
            if (courseId.HasValue) qs.Add($"courseId={courseId}");
            if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
            var url = "api/classes" + (qs.Count > 0 ? ("?" + string.Join("&", qs)) : "");
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

        [HttpGet]
        public async Task<IActionResult> CreateClass(int? courseId)
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home");
            ViewBag.CourseId = courseId;

            // Load Courses
            var coursesRes = await _http.GetAsync("api/courses");
            if (coursesRes.IsSuccessStatusCode)
            {
                var body = await coursesRes.Content.ReadAsStringAsync();
                var courses = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.CourseViewModel>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                ViewBag.Courses = courses;
            }

            // Load Teachers
            var teachersRes = await _http.GetAsync("api/admin/staff/teachers");
            if (teachersRes.IsSuccessStatusCode)
            {
                var body = await teachersRes.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("data", out var dataEl))
                {
                    var teachers = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.TeacherViewModel>>(dataEl.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    ViewBag.Teachers = teachers;
                }
            }

            return View("~/Views/Admin/CreateClass.cshtml");
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
            var body = await res.Content.ReadAsStringAsync();
            string msg = res.IsSuccessStatusCode ? "Xóa lớp thành công" : "Xóa lớp thất bại";
            try { using var doc = JsonDocument.Parse(body); if (doc.RootElement.TryGetProperty("message", out var m)) msg = m.GetString() ?? msg; } catch { }
            TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = msg;
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
            if (!IsStaff())
            {
                var ru = Url.Content("~" + Request.Path + Request.QueryString);
                return RedirectToAction("Login", "Account", new { returnUrl = ru });
            }
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

        public async Task<IActionResult> Registrations(string? status, string? classCode)
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home");

            // MẶC ĐỊNH: Nếu không truyền status, tự động lọc "Chờ duyệt"
            if (string.IsNullOrWhiteSpace(status))
            {
                status = "Chờ duyệt";
            }

            try
            {
                var hasAny = !string.IsNullOrWhiteSpace(status) || !string.IsNullOrWhiteSpace(classCode);
                var url = "api/registrations" + (hasAny ? "?" : "")
                          + (!string.IsNullOrWhiteSpace(status) ? $"status={Uri.EscapeDataString(status!)}" : "")
                          + (!string.IsNullOrWhiteSpace(status) && !string.IsNullOrWhiteSpace(classCode) ? "&" : "")
                          + (!string.IsNullOrWhiteSpace(classCode) ? $"classCode={Uri.EscapeDataString(classCode!)}" : "");
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
                    ViewBag.CurrentClassCode = classCode;
                    return View("~/Views/Admin/Registrations.cshtml", new List<QLTTTA_WEB.Models.AdminRegistrationItem>());
                }

                var data = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminRegistrationItem>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

                // Truyền status hiện tại vào ViewBag để form lọc biết
                ViewBag.CurrentStatus = status;
                ViewBag.CurrentClassCode = classCode;

                return View("~/Views/Admin/Registrations.cshtml", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registrations page error");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải danh sách đăng ký. Vui lòng thử lại.";
                ViewBag.CurrentStatus = status;
                ViewBag.CurrentClassCode = classCode;
                return View("~/Views/Admin/Registrations.cshtml", new List<QLTTTA_WEB.Models.AdminRegistrationItem>());
            }
        }

        [HttpGet]
        public IActionResult QrLookup(string? returnUrl)
        {
            if (!IsStaff())
            {
                var ru = Url.Content("~" + Request.Path + Request.QueryString);
                return RedirectToAction("Login", "Account", new { returnUrl = ru });
            }
            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? null : returnUrl;
            return View("~/Views/Admin/QrLookup.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> QrSearch(string q, string? returnUrl)
        {
            if (!IsStaff())
            {
                var ru = Url.Content("~" + Request.Path + Request.QueryString);
                return RedirectToAction("Login", "Account", new { returnUrl = ru });
            }
            if (string.IsNullOrWhiteSpace(q))
            {
                TempData["ErrorMessage"] = "Không có dữ liệu QR hoặc mã để tra cứu.";
                return RedirectToAction("QrLookup", new { returnUrl });
            }

            try
            {
                // Nếu q là 1 URL và có tham số q bên trong (từ việc quét QR là link), tách nội tham số ra
                if (Uri.TryCreate(q.Trim(), UriKind.Absolute, out var maybeUrl))
                {
                    var innerQ = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(maybeUrl.Query).TryGetValue("q", out var vals)
                        ? vals.ToString()
                        : null;
                    if (!string.IsNullOrWhiteSpace(innerQ))
                    {
                        q = innerQ;
                    }
                }
                // Heuristics: if input looks like CLASS:<code> or plain class code, resolve class and load its pending registrations
                var text = q.Trim();
                var upper = text.ToUpperInvariant();
                bool tryClass = upper.StartsWith("CLASS:") || upper.StartsWith("LOP:") || (!upper.StartsWith("REG:") && !upper.StartsWith("REGID:") && !text.Contains("{"));

                if (tryClass)
                {
                    var classCode = upper.StartsWith("CLASS:") ? text.Substring(6).Trim() : (upper.StartsWith("LOP:") ? text.Substring(4).Trim() : text);
                    if (!string.IsNullOrWhiteSpace(classCode))
                    {
                        var clsRes = await _http.GetAsync($"api/classes?search={Uri.EscapeDataString(classCode)}");
                        var clsBody = await clsRes.Content.ReadAsStringAsync();
                        if (clsRes.IsSuccessStatusCode)
                        {
                            var classes = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminClassItem>>(clsBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                            var match = classes.FirstOrDefault(c => string.Equals(c.ClassCode, classCode, StringComparison.OrdinalIgnoreCase));
                            if (match != null)
                            {
                                // Try pending first
                                var regRes = await _http.GetAsync($"api/registrations?status={Uri.EscapeDataString("Chờ duyệt")}&classCode={Uri.EscapeDataString(match.ClassCode)}");
                                var regBody = await regRes.Content.ReadAsStringAsync();
                                List<QLTTTA_WEB.Models.AdminRegistrationItem> regData = new();
                                if (regRes.IsSuccessStatusCode)
                                {
                                    regData = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminRegistrationItem>>(regBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                                }
                                else
                                {
                                    _logger.LogWarning("Registrations by class failed: {Status} {Body}", regRes.StatusCode, regBody);
                                }

                                // Fallback: if pending is empty, try without status (all statuses)
                                if (regData.Count == 0)
                                {
                                    var regResAll = await _http.GetAsync($"api/registrations?classCode={Uri.EscapeDataString(match.ClassCode)}");
                                    var regBodyAll = await regResAll.Content.ReadAsStringAsync();
                                    if (regResAll.IsSuccessStatusCode)
                                    {
                                        regData = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminRegistrationItem>>(regBodyAll, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Registrations by class (all) failed: {Status} {Body}", regResAll.StatusCode, regBodyAll);
                                    }
                                }

                                ViewBag.Query = q;
                                ViewBag.ReturnUrl = returnUrl;
                                return View("~/Views/Admin/QrSearchResults.cshtml", regData);
                            }
                        }
                    }
                }

                // Default: search by registration QR/code/id
                var url = $"api/registrations/search?q={Uri.EscapeDataString(q)}";
                var res = await _http.GetAsync(url);
                var body = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("QR search failed: {Status} {Body}", res.StatusCode, body);
                    TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(body) ? "Tra cứu thất bại" : body;
                    return RedirectToAction("QrLookup", new { returnUrl });
                }

                var data = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminRegistrationItem>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                ViewBag.Query = q;
                ViewBag.ReturnUrl = returnUrl;
                return View("~/Views/Admin/QrSearchResults.cshtml", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QR search error");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tra cứu. Vui lòng thử lại.";
                return RedirectToAction("QrLookup", new { returnUrl });
            }
        }

        [HttpGet]
        public IActionResult QrImage(string payload, int size = 400)
        {
            if (!IsStaff())
            {
                var ru = Url.Content("~" + Request.Path + Request.QueryString);
                return RedirectToAction("Login", "Account", new { returnUrl = ru });
            }
            if (string.IsNullOrWhiteSpace(payload)) return BadRequest("Missing payload");
            try
            {
                // Clamp size for safety
                if (size < 100) size = 100; if (size > 1024) size = 1024;
                using var qrGen = new QRCodeGenerator();
                using var data = qrGen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
                var png = new PngByteQRCode(data);
                // pixelsPerModule approx controls size: size / 21 baseline
                int ppm = Math.Max(1, size / 21);
                var bytes = png.GetGraphic(ppm);
                return File(bytes, "image/png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QrImage render failed");
                return BadRequest("Không thể tạo ảnh QR");
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
                    }
                    catch { msg = body; }
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
        public IActionResult CreateStaffPage()
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");
            return View("~/Views/Admin/CreateStaff.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> EditStaffPage(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");
            var res = await _http.GetAsync($"api/admin/staff/{id}");
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = "Không tìm thấy nhân viên";
                return RedirectToAction("Staff");
            }
            using var doc = JsonDocument.Parse(body);
            var dataEl = doc.RootElement.TryGetProperty("data", out var d) ? d : doc.RootElement;
            var item = JsonSerializer.Deserialize<QLTTTA_WEB.Models.StaffAdminItem>(dataEl.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return View("~/Views/Admin/EditStaff.cshtml", item);
        }

        [HttpGet]
        public async Task<IActionResult> Staff()
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");
            try
            {
                var res = await _http.GetAsync("api/admin/staff");
                var body = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Load staff failed: {Status} {Body}", res.StatusCode, body);
                    TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(body) ? "Không thể tải danh sách nhân viên" : body;
                    return View("~/Views/Admin/Staff.cshtml", new List<QLTTTA_WEB.Models.StaffAdminItem>());
                }
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                var dataEl = doc.RootElement.TryGetProperty("data", out var d) ? d : doc.RootElement;
                var list = System.Text.Json.JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.StaffAdminItem>>(dataEl.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                return View("~/Views/Admin/Staff.cshtml", list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Staff page load error");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải danh sách nhân viên";
                return View("~/Views/Admin/Staff.cshtml", new List<QLTTTA_WEB.Models.StaffAdminItem>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(QLTTTA_WEB.Models.StaffCreateModel model)
        {
            if (!IsAdmin()) return RedirectToAction("Staff");
            try
            {
                var payload = new
                {
                    Username = model.Username,
                    Password = model.Password,
                    Email = model.Email,
                    RoleId = model.RoleId,
                    FullName = model.FullName,
                    EmployeeCode = model.EmployeeCode,
                    Sex = model.Sex,
                    Phone = model.Phone
                };
                var res = await _http.PostAsync("api/admin/staff", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
                var body = await res.Content.ReadAsStringAsync();
                
                string msg = "Không thể tạo nhân viên";
                try { using var doc = JsonDocument.Parse(body); if (doc.RootElement.TryGetProperty("message", out var m)) msg = m.GetString() ?? msg; } catch { }

                if (res.IsSuccessStatusCode) TempData["SuccessMessage"] = msg;
                else TempData["ErrorMessage"] = msg;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateStaff error");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tạo nhân viên";
            }
            return RedirectToAction("Staff");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStaff(QLTTTA_WEB.Models.StaffUpdateModel model)
        {
            if (!IsAdmin()) return RedirectToAction("Staff");
            try
            {
                var payload = new
                {
                    Email = model.Email,
                    RoleId = model.RoleId,
                    FullName = model.FullName,
                    Sex = model.Sex,
                    Phone = model.Phone
                };
                var res = await _http.PutAsync($"api/admin/staff/{model.UserId}", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
                var body = await res.Content.ReadAsStringAsync();
                
                string msg = "Cập nhật thất bại";
                try { using var doc = JsonDocument.Parse(body); if (doc.RootElement.TryGetProperty("message", out var m)) msg = m.GetString() ?? msg; } catch { }

                if (res.IsSuccessStatusCode) TempData["SuccessMessage"] = msg;
                else TempData["ErrorMessage"] = msg;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateStaff error");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật nhân viên";
            }
            return RedirectToAction("Staff");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LockStaff(int userId)
        {
            if (!IsAdmin()) return RedirectToAction("Staff");
            var res = await _http.PostAsync($"api/admin/staff/{userId}/lock", new StringContent("", Encoding.UTF8, "application/json"));
            var body = await res.Content.ReadAsStringAsync();
            
            string msg = "Khóa tài khoản thất bại";
            try { using var doc = JsonDocument.Parse(body); if (doc.RootElement.TryGetProperty("message", out var m)) msg = m.GetString() ?? msg; } catch { }

            if (res.IsSuccessStatusCode) TempData["SuccessMessage"] = msg;
            else TempData["ErrorMessage"] = msg;
            
            return RedirectToAction("Staff");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlockStaff(int userId)
        {
            if (!IsAdmin()) return RedirectToAction("Staff");
            var res = await _http.PostAsync($"api/admin/staff/{userId}/unlock", new StringContent("", Encoding.UTF8, "application/json"));
            var body = await res.Content.ReadAsStringAsync();

            string msg = "Mở khóa tài khoản thất bại";
            try { using var doc = JsonDocument.Parse(body); if (doc.RootElement.TryGetProperty("message", out var m)) msg = m.GetString() ?? msg; } catch { }

            if (res.IsSuccessStatusCode) TempData["SuccessMessage"] = msg;
            else TempData["ErrorMessage"] = msg;

            return RedirectToAction("Staff");
        }
    }
}
