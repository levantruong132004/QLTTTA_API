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
                
                // 1) Lấy hóa đơn theo đăng ký để biết invoiceId
                int? invoiceId = null;
                var invByReg = await _httpClient.GetAsync($"api/invoices/by-registration/{id}");
                var invByRegBody = await invByReg.Content.ReadAsStringAsync();
                if (invByReg.IsSuccessStatusCode)
                {
                    using var invDoc = JsonDocument.Parse(invByRegBody);
                    if (invDoc.RootElement.TryGetProperty("data", out var invData) && invData.TryGetProperty("invoiceId", out var iid))
                    {
                        invoiceId = iid.GetInt32();
                    }
                }
                else
                {
                    // Cho hiển thị lỗi cụ thể (kể cả lỗi Oracle)
                    try
                    {
                        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(invByRegBody) ? "{}" : invByRegBody);
                        if (doc.RootElement.TryGetProperty("message", out var m) && !string.IsNullOrWhiteSpace(m.GetString()))
                            TempData["ErrorMessage"] = m.GetString();
                    }
                    catch { TempData["ErrorMessage"] = invByRegBody; }
                }

                // 2) Nếu có invoiceId thì lấy chi tiết kèm trạng thái chữ ký
                if (invoiceId.HasValue)
                {
                    var inv = await _httpClient.GetAsync($"api/invoices/with-signature/{invoiceId.Value}");
                    var invBody = await inv.Content.ReadAsStringAsync();
                    if (inv.IsSuccessStatusCode)
                    {
                        var json = invBody;
                        using var doc = JsonDocument.Parse(json);
                        var data = doc.RootElement.GetProperty("data");
                        var invoiceData = data.GetProperty("invoice");
                        
                        page.Invoice = new InvoiceViewModel
                        {
                            InvoiceId = invoiceData.GetProperty("invoiceId").GetInt32(),
                            InvoiceCode = invoiceData.GetProperty("invoiceCode").GetString() ?? string.Empty,
                            CreatedDate = invoiceData.GetProperty("createdDate").GetDateTime(),
                            DueDate = invoiceData.GetProperty("dueDate").GetDateTime(),
                            Amount = invoiceData.GetProperty("amount").GetInt32(),
                            Status = invoiceData.GetProperty("status").GetString() ?? string.Empty,
                            RegistrationId = id
                        };

                        // Signature info
                        if (data.TryGetProperty("signature", out var sigData) && sigData.ValueKind != JsonValueKind.Null)
                        {
                            page.Signature = new DigitalSignatureViewModel
                            {
                                IsSigned = true,
                                IsValid = sigData.GetProperty("isValid").GetBoolean(),
                                SignedBy = sigData.TryGetProperty("signedBy", out var sb) ? sb.GetString() : null,
                                SignedDate = sigData.TryGetProperty("signedDate", out var sd) ? sd.GetDateTime() : null,
                                Algorithm = sigData.TryGetProperty("algorithm", out var alg) ? alg.GetString() : null
                            };
                        }
                        else
                        {
                            page.Signature = new DigitalSignatureViewModel { IsSigned = false };
                        }
                    }
                    else
                    {
                        // Hiển thị thông điệp lỗi cụ thể
                        try
                        {
                            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(invBody) ? "{}" : invBody);
                            if (doc.RootElement.TryGetProperty("message", out var m) && !string.IsNullOrWhiteSpace(m.GetString()))
                                TempData["ErrorMessage"] = m.GetString();
                            else TempData["ErrorMessage"] = invBody;
                        }
                        catch { TempData["ErrorMessage"] = invBody; }
                    }
                }
                else
                {
                    // Không có hóa đơn -> gợi ý thời hạn và số tiền
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

        // Tạo và ký hóa đơn cùng lúc
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAndSignInvoice(CreateAndSignInvoiceViewModel model)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            try
            {
                // Lấy AccountantId từ session thay vì hardcode
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int accountantId))
                {
                    TempData["ErrorMessage"] = "Không thể xác định thông tin kế toán. Vui lòng đăng nhập lại.";
                    return RedirectToAction("Login", "Account");
                }

                // Gán AccountantId từ session
                model.AccountantId = accountantId;

                var json = JsonSerializer.Serialize(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync("api/invoices/create-and-sign", content);
                var body = await res.Content.ReadAsStringAsync();
                
                if (res.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Tạo và ký hóa đơn thành công";
                }
                else
                {
                    try
                    {
                        var o = JsonDocument.Parse(body).RootElement;
                        var msg = o.TryGetProperty("message", out var m) ? m.GetString() : body;
                        TempData["ErrorMessage"] = msg ?? "Không thể tạo và ký hóa đơn";
                    }
                    catch { TempData["ErrorMessage"] = "Không thể tạo và ký hóa đơn"; }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateAndSignInvoice failed for registration {Id}", model.RegistrationId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tạo và ký hóa đơn";
            }
            return RedirectToAction("Invoice", new { id = model.RegistrationId });
        }

        // Ký hóa đơn đã có
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignInvoice(SignInvoiceViewModel model)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            try
            {
                // Lấy AccountantId từ session thay vì hardcode
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int accountantId))
                {
                    TempData["ErrorMessage"] = "Không thể xác định thông tin kế toán. Vui lòng đăng nhập lại.";
                    return RedirectToAction("Login", "Account");
                }

                // Gán AccountantId từ session
                model.AccountantId = accountantId;

                var json = JsonSerializer.Serialize(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync("api/digitalsignature/sign-invoice", content);
                var body = await res.Content.ReadAsStringAsync();
                
                if (res.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Ký hóa đơn thành công";
                }
                else
                {
                    try
                    {
                        var o = JsonDocument.Parse(body).RootElement;
                        var msg = o.TryGetProperty("message", out var m) ? m.GetString() : body;
                        TempData["ErrorMessage"] = msg ?? "Không thể ký hóa đơn";
                    }
                    catch { TempData["ErrorMessage"] = "Không thể ký hóa đơn"; }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignInvoice failed for invoice {Id}", model.InvoiceId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi ký hóa đơn";
            }
            
            // Redirect back to invoice page - need to get registration ID
            return RedirectToAction("Index");
        }

        // Trang quản lý chữ ký số
        [HttpGet]
        public IActionResult DigitalSignature()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            return View();
        }

        // Tạo cặp khóa RSA
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateKeyPair()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            try
            {
                var res = await _httpClient.PostAsync("api/digitalsignature/generate-keypair", null);
                var body = await res.Content.ReadAsStringAsync();
                
                if (res.IsSuccessStatusCode)
                {
                    var responseData = JsonDocument.Parse(body).RootElement;
                    var data = responseData.GetProperty("data");
                    
                    var model = new GenerateKeyPairViewModel
                    {
                        PublicKey = data.GetProperty("publicKey").GetString(),
                        PrivateKey = data.GetProperty("privateKey").GetString(),
                        Message = responseData.GetProperty("message").GetString()
                    };
                    
                    return View("KeyPairGenerated", model);
                }
                else
                {
                    TempData["ErrorMessage"] = "Không thể tạo cặp khóa";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateKeyPair failed");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tạo cặp khóa";
            }
            
            return RedirectToAction("DigitalSignature");
        }
    }
}
