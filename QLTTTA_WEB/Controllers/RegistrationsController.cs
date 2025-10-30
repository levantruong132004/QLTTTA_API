using Microsoft.AspNetCore.Mvc;
using QLTTTA_WEB.Models;
using System.Text;
using System.Text.Json;

namespace QLTTTA_WEB.Controllers
{
    public class RegistrationsController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RegistrationsController> _logger;
        private readonly IConfiguration _configuration;

        public RegistrationsController(IHttpClientFactory httpClientFactory, ILogger<RegistrationsController> logger, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient("ApiClient");
            _logger = logger;
            _configuration = configuration;
        }

        private bool CheckAuthentication()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("UserId"));
        }

        private bool IsAccountant()
        {
            var roleIdStr = HttpContext.Session.GetString("RoleId");
            int.TryParse(roleIdStr, out var roleId);
            var roleName = HttpContext.Session.GetString("Role") ?? string.Empty;
            var acctIdCfg = _configuration["Roles:AccountantRoleId"];
            if (int.TryParse(acctIdCfg, out var acctId) && acctId > 0) return roleId == acctId;
            return roleName.Equals("KETOAN", StringComparison.OrdinalIgnoreCase) || roleName.Equals("Kế toán", StringComparison.OrdinalIgnoreCase) || roleName.Equals("Ke toan", StringComparison.OrdinalIgnoreCase);
        }

        // Role 4: nhân viên học vụ (duyệt đơn)
        private bool IsApprover()
        {
            var roleIdStr = HttpContext.Session.GetString("RoleId");
            int.TryParse(roleIdStr, out var roleId);
            if (roleId == 4) return true;
            var roleName = HttpContext.Session.GetString("Role") ?? string.Empty;
            // Fallback by role name keywords if available
            return roleName.Contains("HocVu", StringComparison.OrdinalIgnoreCase) || roleName.Contains("Học vụ", StringComparison.OrdinalIgnoreCase) || roleName.Contains("Duyet", StringComparison.OrdinalIgnoreCase);
        }

        [HttpGet]
        public async Task<IActionResult> Accountant(int? courseId, int? classId)
        {
            if (!CheckAuthentication()) return RedirectToAction("Login", "Account");
            if (!IsAccountant()) { TempData["ErrorMessage"] = "Bạn không có quyền truy cập mục này"; return RedirectToAction("Index", "Home"); }
            try
            {
                // Load filter options (courses and classes)
                var courses = new List<OptionItem>();
                try
                {
                    var cr = await _httpClient.GetAsync("api/courses");
                    if (cr.IsSuccessStatusCode)
                    {
                        var cjson = await cr.Content.ReadAsStringAsync();
                        _logger.LogInformation("/api/courses response: {Body}", cjson);
                        var arr = JsonDocument.Parse(cjson).RootElement;
                        foreach (var el in arr.EnumerateArray())
                        {
                            int id = 0;
                            string name = string.Empty;
                            if (el.TryGetProperty("courseId", out var idEl) || el.TryGetProperty("CourseId", out idEl))
                                id = idEl.GetInt32();
                            if (el.TryGetProperty("courseName", out var nEl) || el.TryGetProperty("CourseName", out nEl))
                                name = nEl.GetString() ?? string.Empty;
                            if (id > 0) courses.Add(new OptionItem { Id = id, Name = name });
                        }
                        _logger.LogInformation("Loaded {Count} courses for filter", courses.Count);
                    }
                    else
                    {
                        _logger.LogWarning("/api/courses returned status {Status}", cr.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Load course options failed");
                }

                var classes = new List<OptionItem>();
                if (courseId.HasValue)
                {
                    try
                    {
                        var clr = await _httpClient.GetAsync($"api/classes?courseId={courseId.Value}");
                        if (clr.IsSuccessStatusCode)
                        {
                            var cj = await clr.Content.ReadAsStringAsync();
                            var arr = JsonDocument.Parse(cj).RootElement;
                            foreach (var el in arr.EnumerateArray())
                            {
                                int id = 0; string name = string.Empty;
                                if (el.TryGetProperty("classId", out var idEl) || el.TryGetProperty("ClassId", out idEl))
                                    id = idEl.GetInt32();
                                if (el.TryGetProperty("className", out var nEl) || el.TryGetProperty("ClassName", out nEl))
                                    name = nEl.GetString() ?? string.Empty;
                                if (id > 0) classes.Add(new OptionItem { Id = id, Name = name });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Load class options failed for course {CourseId}", courseId);
                    }
                }

                // Load registrations with filters
                var url = "api/registrations/accountant";
                var qs = new List<string>();
                if (courseId.HasValue) qs.Add($"courseId={courseId.Value}");
                if (classId.HasValue) qs.Add($"classId={classId.Value}");
                if (qs.Count > 0) url += "?" + string.Join("&", qs);

                var res = await _httpClient.GetAsync(url);
                var model = new AccountantIndexViewModel
                {
                    SelectedCourseId = courseId,
                    SelectedClassId = classId,
                    Courses = courses,
                    Classes = classes
                };
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    model.Items = JsonSerializer.Deserialize<List<AccountantRegistrationItemViewModel>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                                 ?? new List<AccountantRegistrationItemViewModel>();
                }
                else
                {
                    TempData["ErrorMessage"] = $"Không thể tải danh sách đơn đăng ký (HTTP {(int)res.StatusCode})";
                }
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading accountant registrations");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải danh sách đơn đăng ký";
                return View(new AccountantIndexViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> AccountantDetail(int id)
        {
            if (!CheckAuthentication()) return RedirectToAction("Login", "Account");
            if (!IsAccountant()) { TempData["ErrorMessage"] = "Bạn không có quyền truy cập mục này"; return RedirectToAction("Index", "Home"); }
            try
            {
                var res = await _httpClient.GetAsync($"api/registrations/accountant/{id}");
                if (!res.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy đơn đăng ký";
                    return RedirectToAction(nameof(Accountant));
                }
                var json = await res.Content.ReadAsStringAsync();
                var detail = JsonSerializer.Deserialize<AccountantRegistrationDetailViewModel>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (detail == null)
                {
                    TempData["ErrorMessage"] = "Không đọc được dữ liệu đơn đăng ký";
                    return RedirectToAction(nameof(Accountant));
                }

                // Load invoice if any
                try
                {
                    var invRes = await _httpClient.GetAsync($"api/invoices/by-registration/{id}");
                    if (invRes.IsSuccessStatusCode)
                    {
                        var invJson = await invRes.Content.ReadAsStringAsync();
                        // API returns { success, data }
                        var doc = JsonDocument.Parse(invJson);
                        if (doc.RootElement.TryGetProperty("data", out var dataEl))
                        {
                            var inv = JsonSerializer.Deserialize<InvoiceSummaryViewModel>(dataEl.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            detail.Invoice = inv;
                            if (inv != null)
                            {
                                // Load payments for invoice
                                var payRes = await _httpClient.GetAsync($"api/payments/by-invoice/{inv.InvoiceId}");
                                if (payRes.IsSuccessStatusCode)
                                {
                                    var payJson = await payRes.Content.ReadAsStringAsync();
                                    var payDoc = JsonDocument.Parse(payJson);
                                    if (payDoc.RootElement.TryGetProperty("data", out var payArr))
                                    {
                                        var pays = JsonSerializer.Deserialize<List<PaymentSummaryViewModel>>(payArr.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                                                   ?? new List<PaymentSummaryViewModel>();
                                        detail.Payments = pays;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex2)
                {
                    _logger.LogWarning(ex2, "Load invoice/payments failed for reg {RegId}", id);
                }

                return View(detail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading accountant registration detail {Id}", id);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải chi tiết đơn đăng ký";
                return RedirectToAction(nameof(Accountant));
            }
        }

        // Trang duyệt đơn dành cho vai trò 4
        [HttpGet]
        public async Task<IActionResult> Approver(int? courseId, int? classId)
        {
            if (!CheckAuthentication()) return RedirectToAction("Login", "Account");
            if (!IsApprover()) { TempData["ErrorMessage"] = "Bạn không có quyền truy cập mục này"; return RedirectToAction("Index", "Home"); }
            try
            {
                // Tái sử dụng logic tải bộ lọc và danh sách như trang Kế toán
                var courses = new List<OptionItem>();
                try
                {
                    var cr = await _httpClient.GetAsync("api/courses");
                    if (cr.IsSuccessStatusCode)
                    {
                        var cjson = await cr.Content.ReadAsStringAsync();
                        var arr = JsonDocument.Parse(cjson).RootElement;
                        foreach (var el in arr.EnumerateArray())
                        {
                            int id = 0; string name = string.Empty;
                            if (el.TryGetProperty("courseId", out var idEl) || el.TryGetProperty("CourseId", out idEl)) id = idEl.GetInt32();
                            if (el.TryGetProperty("courseName", out var nEl) || el.TryGetProperty("CourseName", out nEl)) name = nEl.GetString() ?? string.Empty;
                            if (id > 0) courses.Add(new OptionItem { Id = id, Name = name });
                        }
                    }
                }
                catch { }

                var classes = new List<OptionItem>();
                if (courseId.HasValue)
                {
                    try
                    {
                        var clr = await _httpClient.GetAsync($"api/classes?courseId={courseId.Value}");
                        if (clr.IsSuccessStatusCode)
                        {
                            var cj = await clr.Content.ReadAsStringAsync();
                            var arr = JsonDocument.Parse(cj).RootElement;
                            foreach (var el in arr.EnumerateArray())
                            {
                                int id = 0; string name = string.Empty;
                                if (el.TryGetProperty("classId", out var idEl) || el.TryGetProperty("ClassId", out idEl)) id = idEl.GetInt32();
                                if (el.TryGetProperty("className", out var nEl) || el.TryGetProperty("ClassName", out nEl)) name = nEl.GetString() ?? string.Empty;
                                if (id > 0) classes.Add(new OptionItem { Id = id, Name = name });
                            }
                        }
                    }
                    catch { }
                }

                var url = "api/registrations/accountant"; // dùng cùng endpoint liệt kê
                var qs = new List<string>();
                if (courseId.HasValue) qs.Add($"courseId={courseId.Value}");
                if (classId.HasValue) qs.Add($"classId={classId.Value}");
                if (qs.Count > 0) url += "?" + string.Join("&", qs);

                var res = await _httpClient.GetAsync(url);
                var model = new AccountantIndexViewModel
                {
                    SelectedCourseId = courseId,
                    SelectedClassId = classId,
                    Courses = courses,
                    Classes = classes
                };
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    model.Items = JsonSerializer.Deserialize<List<AccountantRegistrationItemViewModel>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                                 ?? new List<AccountantRegistrationItemViewModel>();
                }
                else
                {
                    TempData["ErrorMessage"] = $"Không thể tải danh sách đơn đăng ký (HTTP {(int)res.StatusCode})";
                }
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading approver registrations");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải danh sách đơn đăng ký";
                return View(new AccountantIndexViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> ApproverDetail(int id)
        {
            if (!CheckAuthentication()) return RedirectToAction("Login", "Account");
            if (!IsApprover()) { TempData["ErrorMessage"] = "Bạn không có quyền truy cập mục này"; return RedirectToAction("Index", "Home"); }
            try
            {
                var res = await _httpClient.GetAsync($"api/registrations/accountant/{id}");
                if (!res.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy đơn đăng ký";
                    return RedirectToAction(nameof(Approver));
                }
                var json = await res.Content.ReadAsStringAsync();
                var detail = JsonSerializer.Deserialize<AccountantRegistrationDetailViewModel>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (detail == null)
                {
                    TempData["ErrorMessage"] = "Không đọc được dữ liệu đơn đăng ký";
                    return RedirectToAction(nameof(Approver));
                }
                return View(detail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading approver registration detail {Id}", id);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải chi tiết đơn đăng ký";
                return RedirectToAction(nameof(Approver));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            if (!CheckAuthentication()) return RedirectToAction("Login", "Account");
            if (!IsApprover()) { TempData["ErrorMessage"] = "Bạn không có quyền thực hiện thao tác này"; return RedirectToAction("Index", "Home"); }
            try
            {
                var res = await _httpClient.PostAsync($"api/registrations/{id}/approve", content: null);
                var body = await res.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(body);
                var ok = doc.RootElement.TryGetProperty("Success", out var s) ? s.GetBoolean() : (res.IsSuccessStatusCode);
                TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok ? "Đã duyệt đơn thành công" : (doc.RootElement.TryGetProperty("Message", out var m) ? m.GetString() : "Duyệt đơn thất bại");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Approve registration failed {Id}", id);
                TempData["ErrorMessage"] = "Có lỗi khi duyệt đơn";
            }
            // trở về trang chi tiết của người duyệt
            return RedirectToAction(nameof(ApproverDetail), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            if (!CheckAuthentication()) return RedirectToAction("Login", "Account");
            if (!IsApprover()) { TempData["ErrorMessage"] = "Bạn không có quyền thực hiện thao tác này"; return RedirectToAction("Index", "Home"); }
            try
            {
                var res = await _httpClient.PostAsync($"api/registrations/{id}/reject", content: null);
                var body = await res.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(body);
                var ok = doc.RootElement.TryGetProperty("Success", out var s) ? s.GetBoolean() : (res.IsSuccessStatusCode);
                TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok ? "Đã từ chối đơn" : (doc.RootElement.TryGetProperty("Message", out var m) ? m.GetString() : "Từ chối đơn thất bại");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reject registration failed {Id}", id);
                TempData["ErrorMessage"] = "Có lỗi khi từ chối đơn";
            }
            return RedirectToAction(nameof(ApproverDetail), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInvoice(int registrationId, DateTime dueDate, int amount)
        {
            if (!CheckAuthentication()) return RedirectToAction("Login", "Account");
            if (!IsAccountant()) { TempData["ErrorMessage"] = "Bạn không có quyền thực hiện thao tác này"; return RedirectToAction("Index", "Home"); }
            try
            {
                var payload = new { RegistrationId = registrationId, DueDate = dueDate, Amount = amount };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync("api/invoices", content);
                var body = await res.Content.ReadAsStringAsync();
                try
                {
                    var doc = JsonDocument.Parse(body);
                    var ok = doc.RootElement.TryGetProperty("success", out var s) ? s.GetBoolean() : res.IsSuccessStatusCode;
                    TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok ? "Tạo hóa đơn thành công" : (doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "Tạo hóa đơn thất bại");
                }
                catch
                {
                    TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = res.IsSuccessStatusCode ? "Tạo hóa đơn thành công" : $"Tạo hóa đơn thất bại (HTTP {(int)res.StatusCode})";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create invoice failed for reg {RegId}", registrationId);
                TempData["ErrorMessage"] = "Có lỗi khi tạo hóa đơn";
            }
            return RedirectToAction(nameof(AccountantDetail), new { id = registrationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePayment(int registrationId, int invoiceId, int amount, string paymentMethod)
        {
            if (!CheckAuthentication()) return RedirectToAction("Login", "Account");
            if (!IsAccountant()) { TempData["ErrorMessage"] = "Bạn không có quyền thực hiện thao tác này"; return RedirectToAction("Index", "Home"); }
            try
            {
                // AccountantId from current session user
                var userIdStr = HttpContext.Session.GetString("UserId");
                int.TryParse(userIdStr, out var accountantId);
                var payload = new { Amount = amount, PaymentMethod = paymentMethod, InvoiceId = invoiceId, AccountantId = accountantId };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync("api/payments", content);
                var body = await res.Content.ReadAsStringAsync();
                try
                {
                    var doc = JsonDocument.Parse(body);
                    var ok = doc.RootElement.TryGetProperty("success", out var s) ? s.GetBoolean() : res.IsSuccessStatusCode;
                    TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok ? "Ghi nhận thanh toán thành công" : (doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "Thanh toán thất bại");
                }
                catch
                {
                    TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = res.IsSuccessStatusCode ? "Ghi nhận thanh toán thành công" : $"Thanh toán thất bại (HTTP {(int)res.StatusCode})";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create payment failed for invoice {InvoiceId}", invoiceId);
                TempData["ErrorMessage"] = "Có lỗi khi ghi nhận thanh toán";
            }
            return RedirectToAction(nameof(AccountantDetail), new { id = registrationId });
        }
    }
}
