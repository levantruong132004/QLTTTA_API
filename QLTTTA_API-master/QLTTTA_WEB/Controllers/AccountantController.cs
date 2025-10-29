using Microsoft.AspNetCore.Mvc;
using QLTTTA_WEB.Models;
using System.Text;
using System.Text.Json;

namespace QLTTTA_WEB.Controllers
{
    public class AccountantController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AccountantController> _logger;

        public AccountantController(IHttpClientFactory httpClientFactory, ILogger<AccountantController> logger)
        {
            _httpClient = httpClientFactory.CreateClient("ApiClient");
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? courseId, int? classId)
        {
            // Require login
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            var vm = new AccountantHomeViewModel
            {
                SelectedCourseId = courseId,
                SelectedClassId = classId
            };

            try
            {
                // Load courses
                var crs = await _httpClient.GetAsync("api/courses");
                if (crs.IsSuccessStatusCode)
                {
                    var json = await crs.Content.ReadAsStringAsync();
                    vm.Courses = JsonSerializer.Deserialize<List<SimpleCourseViewModel>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                // Default select first course if none
                if (!vm.SelectedCourseId.HasValue && vm.Courses.Any())
                    vm.SelectedCourseId = vm.Courses.First().CourseId;

                // Load classes of selected course
                if (vm.SelectedCourseId.HasValue)
                {
                    var cls = await _httpClient.GetAsync($"api/classes?courseId={vm.SelectedCourseId.Value}");
                    if (cls.IsSuccessStatusCode)
                    {
                        var json = await cls.Content.ReadAsStringAsync();
                        vm.Classes = JsonSerializer.Deserialize<List<AdminClassItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    }
                }
                // Default select first class if none
                if (!vm.SelectedClassId.HasValue && vm.Classes.Any())
                    vm.SelectedClassId = vm.Classes.First().ClassId;

                // Load registrations for accountant
                var url = new StringBuilder("api/registrations/accountant");
                var hasQuery = false;
                if (vm.SelectedCourseId.HasValue)
                {
                    url.Append(hasQuery ? "&" : "?").Append("courseId=").Append(vm.SelectedCourseId.Value);
                    hasQuery = true;
                }
                if (vm.SelectedClassId.HasValue)
                {
                    url.Append(hasQuery ? "&" : "?").Append("classId=").Append(vm.SelectedClassId.Value);
                }
                var regs = await _httpClient.GetAsync(url.ToString());
                if (regs.IsSuccessStatusCode)
                {
                    var json = await regs.Content.ReadAsStringAsync();
                    vm.Registrations = JsonSerializer.Deserialize<List<AccountantRegItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Accountant Index failed");
                TempData["ErrorMessage"] = "Không tải được dữ liệu";
            }

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Invoice(int id)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            var page = new AccountantInvoicePageViewModel();
            try
            {
                // Registration detail
                var res = await _httpClient.GetAsync($"api/registrations/accountant/{id}");
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    page.Registration = JsonSerializer.Deserialize<AccountantRegItem>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                // Existing invoice
                var inv = await _httpClient.GetAsync($"api/invoices/by-registration/{id}");
                if (inv.IsSuccessStatusCode)
                {
                    var json = await inv.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var data = doc.RootElement.TryGetProperty("data", out var d) ? d : doc.RootElement;
                    page.Invoice = new InvoiceViewModel
                    {
                        InvoiceId = data.GetProperty("invoiceId").GetInt32(),
                        InvoiceCode = data.GetProperty("invoiceCode").GetString() ?? string.Empty,
                        CreatedDate = data.GetProperty("createdDate").GetDateTime(),
                        DueDate = data.GetProperty("dueDate").GetDateTime(),
                        Amount = data.GetProperty("amount").GetInt32(),
                        Status = data.GetProperty("status").GetString() ?? string.Empty,
                        RegistrationId = id
                    };
                }
                else
                {
                    // Suggest defaults when invoice doesn't exist
                    page.SuggestedDueDate = DateTime.Now.AddDays(7);
                    page.SuggestedAmount = page.Registration?.StandardFee ?? 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Load invoice page failed for registration {Id}", id);
                TempData["ErrorMessage"] = "Không tải được thông tin hóa đơn";
            }
            return View(page);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInvoice(int registrationId, DateTime dueDate, int amount)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            try
            {
                var payload = new { RegistrationId = registrationId, DueDate = dueDate, Amount = amount };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync("api/invoices", content);
                var body = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Tạo hóa đơn thành công";
                }
                else
                {
                    try
                    {
                        var o = JsonDocument.Parse(body).RootElement;
                        var msg = o.TryGetProperty("message", out var m) ? m.GetString() : body;
                        TempData["ErrorMessage"] = msg ?? "Không thể tạo hóa đơn";
                    }
                    catch { TempData["ErrorMessage"] = "Không thể tạo hóa đơn"; }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateInvoice failed for registration {Id}", registrationId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tạo hóa đơn";
            }
            return RedirectToAction("Invoice", new { id = registrationId });
        }
    }
}
