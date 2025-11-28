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

        private bool IsAccountant()
        {
            var role = HttpContext.Session.GetString("Role") ?? string.Empty;
            var roleIdStr = HttpContext.Session.GetString("RoleId");
            int.TryParse(roleIdStr, out var roleId);
            var username = HttpContext.Session.GetString("Username") ?? string.Empty;
            // Admins can also access accountant pages
            if (username.Equals("QLTTTA_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                username.Equals("QLTTA_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                role.Contains("QuanTri", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return role.Contains("KeToan", StringComparison.OrdinalIgnoreCase) || roleId == 6; // 6 if mapped
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? courseId, int? classId)
        {
            if (!IsAccountant())
                return RedirectToAction("Index", "Home");

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
            if (!IsAccountant())
                return RedirectToAction("Index", "Home");

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
            if (!IsAccountant())
                return RedirectToAction("Index", "Home");

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAndSignInvoice(CreateAndSignInvoiceViewModel model)
        {
            return NotFound();
        }

        // Ký hóa đơn đã có (đường dẫn file) - Vô hiệu hóa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SignInvoice(SignInvoiceViewModel model)
        {
            return NotFound();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignInvoiceUpload(int invoiceId, IFormFile privateKey)
        {
            if (!IsAccountant())
                return RedirectToAction("Index", "Home");

            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int accountantId))
                {
                    TempData["ErrorMessage"] = "Không xác định được kế toán. Vui lòng đăng nhập lại.";
                    return RedirectToAction("Index");
                }

                if (privateKey == null || privateKey.Length == 0)
                {
                    TempData["ErrorMessage"] = "Vui lòng chọn file private key.";
                    return RedirectToAction("Index");
                }

                using var content = new MultipartFormDataContent();
                content.Add(new StringContent(invoiceId.ToString()), "invoiceId");
                content.Add(new StringContent(accountantId.ToString()), "accountantId");
                using var stream = privateKey.OpenReadStream();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var fileContent = new ByteArrayContent(ms.ToArray());
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-pem-file");
                content.Add(fileContent, "privateKey", privateKey.FileName);

                var res = await _httpClient.PostAsync("api/digitalsignature/sign-invoice/upload", content);
                var body = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Ký hóa đơn thành công và đã gửi email cho học viên.";
                }
                else
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                        var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : body;
                        TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(msg) ? "Ký hóa đơn thất bại" : msg;
                    }
                    catch { TempData["ErrorMessage"] = body; }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignInvoiceUpload failed for invoice {InvoiceId}", invoiceId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi ký hóa đơn";
            }

            return RedirectToAction("Index");
        }

        // Trang quản lý chữ ký số (cũ) - Vô hiệu hóa
        [HttpGet]
        public IActionResult DigitalSignature()
        {
            return NotFound();
        }

        // Tạo cặp khóa RSA (cũ) - Vô hiệu hóa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GenerateKeyPair()
        {
            return NotFound();
        }

        // Trang quản lý thanh toán chờ xác nhận
        [HttpGet]
        public async Task<IActionResult> PendingPayments()
        {
            if (HttpContext?.Session?.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            var pendingList = new List<PendingPaymentViewModel>();
            try
            {
                var res = await _httpClient.GetAsync("api/payments/pending");
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var data))
                    {
                        pendingList = JsonSerializer.Deserialize<List<PendingPaymentViewModel>>(data.GetRawText(), 
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    }
                }
                else
                {
                    var body = await res.Content.ReadAsStringAsync();
                    TempData["ErrorMessage"] = "Không thể tải danh sách thanh toán chờ xác nhận";
                    _logger.LogWarning("GetPendingPayments failed: {Body}", body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PendingPayments failed");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải danh sách";
            }

            return View(pendingList);
        }

        // Xác nhận thanh toán
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int invoiceId)
        {
            if (HttpContext?.Session?.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            try
            {
                var userIdStr = HttpContext?.Session?.GetString("UserId");
                if (!int.TryParse(userIdStr, out int accountantId))
                {
                    TempData["ErrorMessage"] = "Không xác định được kế toán";
                    return RedirectToAction("PendingPayments");
                }

                var payload = new { InvoiceId = invoiceId, AccountantId = accountantId };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var res = await _httpClient.PostAsync("api/payments/confirm", content);
                var body = await res.Content.ReadAsStringAsync();

                if (res.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Đã xác nhận thanh toán thành công";
                }
                else
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "Xác nhận thanh toán thất bại";
                        TempData["ErrorMessage"] = msg;
                    }
                    catch
                    {
                        TempData["ErrorMessage"] = "Xác nhận thanh toán thất bại";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConfirmPayment failed for invoice {InvoiceId}", invoiceId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi xác nhận thanh toán";
            }

            return RedirectToAction("PendingPayments");
        }
        // In hóa đơn và gửi PDF qua email cho học viên
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrintAndEmailInvoice(int invoiceId)
        {
            if (!IsAccountant())
                return RedirectToAction("Index", "Home");

            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (!int.TryParse(userIdStr, out int accountantId))
                {
                    TempData["ErrorMessage"] = "Không xác định được kế toán";
                    return RedirectToAction("Index");
                }

                // Gọi API để tạo PDF và gửi email
                var payload = new { InvoiceId = invoiceId, AccountantId = accountantId };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var res = await _httpClient.PostAsync("api/invoices/print-and-email", content);
                var body = await res.Content.ReadAsStringAsync();

                if (res.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Đã in hóa đơn và gửi PDF qua email cho học viên thành công";
                }
                else
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "In hóa đơn thất bại";
                        TempData["ErrorMessage"] = msg;
                    }
                    catch
                    {
                        TempData["ErrorMessage"] = "In hóa đơn thất bại";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PrintAndEmailInvoice failed for invoice {InvoiceId}", invoiceId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi in hóa đơn";
            }

            return RedirectToAction("Index");
        }

        // Ký số và In hóa đơn (upload private key, ký số, tạo PDF đẹp, gửi email)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignAndPrintInvoice(int invoiceId, int accountantId, IFormFile privateKey)
        {
            if (!IsAccountant())
                return RedirectToAction("Index", "Home");

            try
            {
                if (privateKey == null || privateKey.Length == 0)
                {
                    TempData["ErrorMessage"] = "Vui lòng chọn file private key";
                    return RedirectToAction("Index");
                }

                // Gọi API print-sign-email (ký số + tạo PDF đẹp + gửi email)
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent(invoiceId.ToString()), "invoiceId");
                content.Add(new StringContent(accountantId.ToString()), "accountantId");
                
                using var stream = privateKey.OpenReadStream();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var fileContent = new ByteArrayContent(ms.ToArray());
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-pem-file");
                content.Add(fileContent, "privateKey", privateKey.FileName);

                var res = await _httpClient.PostAsync("api/invoices/print-sign-email", content);
                
                if (res.IsSuccessStatusCode)
                {
                    // Kiểm tra header để biết đã gửi email chưa
                    var emailSent = res.Headers.TryGetValues("X-Email-Sent", out var emailValues) && 
                                   emailValues.FirstOrDefault() == "true";
                    var alreadySigned = res.Headers.TryGetValues("X-Already-Signed", out var signedValues) && 
                                       signedValues.FirstOrDefault() == "true";
                    
                    if (alreadySigned)
                    {
                        TempData["SuccessMessage"] = "✅ Hóa đơn đã được ký số trước đó. Đã tạo PDF với giao diện đẹp" + 
                                                    (emailSent ? " và gửi email thành công!" : "!");
                    }
                    else
                    {
                        TempData["SuccessMessage"] = "✅ Đã ký số hóa đơn thành công! PDF với giao diện đẹp" + 
                                                    (emailSent ? " đã được gửi qua email cho học viên." : " đã được tạo.");
                    }
                }
                else
                {
                    var body = await res.Content.ReadAsStringAsync();
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "Ký số và in hóa đơn thất bại";
                        TempData["ErrorMessage"] = msg;
                    }
                    catch
                    {
                        TempData["ErrorMessage"] = "Ký số và in hóa đơn thất bại: " + body;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignAndPrintInvoice failed for invoice {InvoiceId}", invoiceId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi ký số và in hóa đơn: " + ex.Message;
            }

            return RedirectToAction("Index");
        }
    }
}
