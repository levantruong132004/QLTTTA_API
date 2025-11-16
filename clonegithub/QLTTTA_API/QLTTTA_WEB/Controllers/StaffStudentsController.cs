using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

namespace QLTTTA_WEB.Controllers
{
    public class StaffStudentsController : Controller
    {
        private readonly HttpClient _http;
        private readonly ILogger<StaffStudentsController> _logger;
        public StaffStudentsController(IHttpClientFactory factory, ILogger<StaffStudentsController> logger)
        { _http = factory.CreateClient("ApiClient"); _logger = logger; }

        private bool IsHocVuOrAdmin()
        {
            var roleIdStr = HttpContext.Session.GetString("RoleId");
            int.TryParse(roleIdStr, out var roleId);
            var roleName = HttpContext.Session.GetString("Role") ?? string.Empty;
            return roleId == 4
                || roleName.Contains("NhanVienHocVu", StringComparison.OrdinalIgnoreCase)
                || roleName.Contains("Nhân viên học vụ", StringComparison.OrdinalIgnoreCase)
                || roleId == 5
                || roleName.Contains("QuanTri", StringComparison.OrdinalIgnoreCase)
                || roleName.Contains("Quản trị", StringComparison.OrdinalIgnoreCase)
                || roleName.Contains("Admin", StringComparison.OrdinalIgnoreCase);
        }

        private static T? DeserializeEnvelope<T>(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("Data", out var dataEl) || root.TryGetProperty("data", out dataEl))
                {
                    var raw = dataEl.GetRawText();
                    return JsonSerializer.Deserialize<T>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                // fallback: try direct
                return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return default;
            }
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search)
        {
            if (!IsHocVuOrAdmin()) return RedirectToAction("Index", "Home");
            var url = "api/staff/students" + (string.IsNullOrWhiteSpace(search) ? string.Empty : $"?search={Uri.EscapeDataString(search)}");
            var res = await _http.GetAsync(url);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(body) ? $"Không thể tải danh sách (HTTP {(int)res.StatusCode})" : body;
                return View("~/Views/Staff/Students.cshtml", new List<QLTTTA_WEB.Models.StaffStudentListItem>());
            }

            var data = DeserializeEnvelope<List<QLTTTA_WEB.Models.StaffStudentListItem>>(body) ?? new();
            ViewBag.Search = search;
            return View("~/Views/Staff/Students.cshtml", data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(int id)
        {
            if (!IsHocVuOrAdmin()) return RedirectToAction("Index");
            var res = await _http.PostAsync($"api/staff/students/{id}/lock", new StringContent("", Encoding.UTF8, "application/json"));
            var body = await res.Content.ReadAsStringAsync();
            // Try to unwrap message if provided
            TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = string.IsNullOrWhiteSpace(body) ? (res.IsSuccessStatusCode ? "Đã khóa tài khoản học viên." : "Không thể khóa tài khoản.") : body;
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(int id)
        {
            if (!IsHocVuOrAdmin()) return RedirectToAction("Index");
            var res = await _http.PostAsync($"api/staff/students/{id}/unlock", new StringContent("", Encoding.UTF8, "application/json"));
            var body = await res.Content.ReadAsStringAsync();
            TempData[res.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = string.IsNullOrWhiteSpace(body) ? (res.IsSuccessStatusCode ? "Đã mở khóa tài khoản học viên." : "Không thể mở khóa tài khoản.") : body;
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            if (!IsHocVuOrAdmin()) return RedirectToAction("Index", "Home");
            var vm = new QLTTTA_WEB.Models.StaffStudentDetailViewModel();

            try
            {
                // Gọi API staff detail theo cơ chế cũ: trả về { success, data: { student, registrations, invoices } }
                var res = await _http.GetAsync($"api/staff/students/{id}/detail");
                var body = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode || string.IsNullOrWhiteSpace(body))
                {
                    vm.Student = new QLTTTA_WEB.Models.StaffStudentListItem { StudentId = id };
                    vm.Registrations = new();
                    vm.Invoices = new();
                }
                else
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("Data", out var dataEl) || root.TryGetProperty("data", out dataEl))
                    {
                        // student
                        if (dataEl.TryGetProperty("student", out var stuEl))
                        {
                            vm.Student = new QLTTTA_WEB.Models.StaffStudentListItem
                            {
                                StudentId = stuEl.TryGetProperty("studentId", out var p0) ? p0.GetInt32() : id,
                                StudentCode = stuEl.TryGetProperty("studentCode", out var p1) ? p1.GetString() : null,
                                FullName = stuEl.TryGetProperty("fullName", out var p2) ? p2.GetString() : null,
                                Email = stuEl.TryGetProperty("email", out var p3) ? p3.GetString() : null,
                                PhoneNumber = stuEl.TryGetProperty("phoneNumber", out var p4) ? p4.GetString() : null,
                                IsActive = stuEl.TryGetProperty("isActive", out var p5) && p5.ValueKind == JsonValueKind.True
                            };
                        }
                        else
                        {
                            vm.Student = new QLTTTA_WEB.Models.StaffStudentListItem { StudentId = id };
                        }

                        // registrations
                        if (dataEl.TryGetProperty("registrations", out var regsEl) && regsEl.ValueKind == JsonValueKind.Array)
                        {
                            var regs = new List<QLTTTA_WEB.Models.StaffRegistrationItemVM>();
                            foreach (var r in regsEl.EnumerateArray())
                            {
                                regs.Add(new QLTTTA_WEB.Models.StaffRegistrationItemVM
                                {
                                    RegistrationId = r.TryGetProperty("registrationId", out var r0) ? r0.GetInt32() : 0,
                                    CourseName = r.TryGetProperty("courseName", out var r1) ? r1.GetString() ?? string.Empty : string.Empty,
                                    ClassName = r.TryGetProperty("className", out var r2) ? r2.GetString() ?? string.Empty : string.Empty,
                                    RegistrationDate = r.TryGetProperty("registrationDate", out var r3) && r3.ValueKind == JsonValueKind.String && DateTime.TryParse(r3.GetString(), out var dt) ? dt : (DateTime?)null,
                                    Status = r.TryGetProperty("status", out var r4) ? r4.GetString() ?? string.Empty : string.Empty
                                });
                            }
                            vm.Registrations = regs;
                        }
                        else
                        {
                            vm.Registrations = new();
                        }

                        // invoices
                        if (dataEl.TryGetProperty("invoices", out var invEl) && invEl.ValueKind == JsonValueKind.Array)
                        {
                            var invs = new List<QLTTTA_WEB.Models.StaffInvoiceItemVM>();
                            foreach (var inv in invEl.EnumerateArray())
                            {
                                invs.Add(new QLTTTA_WEB.Models.StaffInvoiceItemVM
                                {
                                    InvoiceId = inv.TryGetProperty("invoiceId", out var i0) ? i0.GetInt32() : 0,
                                    InvoiceCode = inv.TryGetProperty("invoiceCode", out var i1) ? i1.GetString() ?? string.Empty : string.Empty,
                                    Amount = inv.TryGetProperty("amount", out var i2) && i2.TryGetDecimal(out var dec) ? Convert.ToInt32(dec) : (inv.TryGetProperty("amount", out var i2b) && i2b.ValueKind == JsonValueKind.Number && i2b.TryGetInt32(out var iv) ? iv : 0),
                                    // Map paidDate (nếu có) vào CreatedDate để hiển thị
                                    CreatedDate = inv.TryGetProperty("paidDate", out var i3) && i3.ValueKind == JsonValueKind.String && DateTime.TryParse(i3.GetString(), out var dt2) ? dt2 : (DateTime?)null,
                                    Status = inv.TryGetProperty("status", out var i4) ? i4.GetString() ?? string.Empty : string.Empty
                                });
                            }
                            vm.Invoices = invs;
                        }
                        else
                        {
                            vm.Invoices = new();
                        }
                    }
                    else
                    {
                        vm.Student = new QLTTTA_WEB.Models.StaffStudentListItem { StudentId = id };
                        vm.Registrations = new();
                        vm.Invoices = new();
                    }
                }
            }
            catch
            {
                vm.Student = new QLTTTA_WEB.Models.StaffStudentListItem { StudentId = id };
                vm.Registrations = new();
                vm.Invoices = new();
            }

            return View("~/Views/Staff/StudentDetail.cshtml", vm);
        }
    }
}
