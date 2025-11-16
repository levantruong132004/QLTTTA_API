using Microsoft.AspNetCore.Mvc;
using QLTTTA_WEB.Models;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization; // for AllowAnonymous on CourseDetails

namespace QLTTTA_WEB.Controllers
{
    public class StudentController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<StudentController> _logger;

        public StudentController(IHttpClientFactory httpClientFactory, ILogger<StudentController> logger)
        {
            _httpClient = httpClientFactory.CreateClient("ApiClient");
            _logger = logger;
        }
        // Staff/admin checker aligned with Admin.ManageController
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

        // Quyền xem/chỉnh sửa sẽ do database (VIEW + quyền UPDATE) kiểm soát theo user đang kết nối.
        // Chỉ cần đảm bảo đã đăng nhập (có session UserId) ở tầng web.

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            var res = await _httpClient.GetAsync("api/profile");
            if (!res.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = "Không tải được thông tin cá nhân";
                return View(new StudentProfileViewModel());
            }
            var json = await res.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<StudentProfileViewModel>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new StudentProfileViewModel();
            return View(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(StudentProfileViewModel model)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            var req = new StudentProfileUpdateRequest
            {
                // Chỉ gửi các trường được phép chỉnh sửa
                SoDienThoai = model.SoDienThoai,
                DiaChi = model.DiaChi
            };
            var body = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");
            var res = await _httpClient.PutAsync("api/profile", body);
            if (res.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Cập nhật thông tin thành công";
            }
            else
            {
                TempData["ErrorMessage"] = "Cập nhật thất bại";
            }
            return RedirectToAction("Profile");
        }

        [HttpGet]
        public async Task<IActionResult> Courses()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            var res = await _httpClient.GetAsync("api/profile/courses");
            var list = new List<PublicCourseItem>();
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                list = JsonSerializer.Deserialize<List<PublicCourseItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> OpenClasses(string courseCode)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");
            if (string.IsNullOrWhiteSpace(courseCode)) return RedirectToAction("Courses");

            var res = await _httpClient.GetAsync($"api/profile/open-classes/{Uri.EscapeDataString(courseCode)}");
            var json = await res.Content.ReadAsStringAsync();
            List<QLTTTA_WEB.Models.OpenClassItem> data;
            if (!res.IsSuccessStatusCode)
            {
                string msg = "Không tải được danh sách lớp";
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("message", out var m))
                        {
                            var s = m.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) msg = s!;
                        }
                        else
                        {
                            msg = json;
                        }
                    }
                    catch
                    {
                        msg = json;
                    }
                }
                TempData["ErrorMessage"] = msg;
                data = new List<QLTTTA_WEB.Models.OpenClassItem>();
            }
            else
            {
                try
                {
                    data = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.OpenClassItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Deserialize OpenClasses failed. Body: {Body}", json);
                    TempData["ErrorMessage"] = "Dữ liệu lớp mở trả về không hợp lệ.";
                    data = new List<QLTTTA_WEB.Models.OpenClassItem>();
                }
            }
            ViewBag.CourseCode = courseCode;
            return View("OpenClasses", data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterClass(int classId, string? courseCode)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");
            try
            {
                var payload = new { ClassId = classId };
                var res = await _httpClient.PostAsync("api/profile/register-class", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
                var body = await res.Content.ReadAsStringAsync();

                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var apiRes = JsonSerializer.Deserialize<BasicApiResponse>(body, opts) ?? new BasicApiResponse { Success = res.IsSuccessStatusCode };

                if (apiRes.Success)
                    TempData["SuccessMessage"] = string.IsNullOrWhiteSpace(apiRes.Message) ? "Đăng ký thành công" : apiRes.Message;
                else
                    TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(apiRes.Message) ? "Có lỗi xảy ra khi đăng ký lớp" : apiRes.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RegisterClass error {ClassId}", classId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi đăng ký lớp";
            }
            if (!string.IsNullOrWhiteSpace(courseCode))
                return RedirectToAction("OpenClasses", new { courseCode });
            return RedirectToAction("Courses");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterCourse(string courseCode)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");
            try
            {
                var payload = new { courseCode };
                var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync("api/profile/register-course", body);
                var txt = await res.Content.ReadAsStringAsync();

                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var apiRes = JsonSerializer.Deserialize<BasicApiResponse>(txt, opts) ?? new BasicApiResponse { Success = res.IsSuccessStatusCode };

                if (apiRes.Success)
                    TempData["SuccessMessage"] = string.IsNullOrWhiteSpace(apiRes.Message) ? $"Đã gửi yêu cầu đăng ký khóa {courseCode}." : apiRes.Message;
                else
                    TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(apiRes.Message) ? "Gửi yêu cầu đăng ký thất bại" : apiRes.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RegisterCourse error for {CourseCode}", courseCode);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi gửi yêu cầu đăng ký";
            }
            return RedirectToAction("Courses");
        }

        // Xem danh sách đơn đăng ký và hóa đơn của học viên
        [HttpGet]
        public async Task<IActionResult> MyRegistrations()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            try
            {
                var res = await _httpClient.GetAsync("api/profile/registrations");
                var registrations = new List<StudentRegistrationViewModel>();
                
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    registrations = JsonSerializer.Deserialize<List<StudentRegistrationViewModel>>(json, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                else
                {
                    TempData["ErrorMessage"] = "Không thể tải danh sách đăng ký";
                }
                
                return View(registrations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load student registrations");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải danh sách đăng ký";
                return View(new List<StudentRegistrationViewModel>());
            }
        }

        // Xem chi tiết hóa đơn với chữ ký số
        [HttpGet]
        public async Task<IActionResult> ViewInvoice(int registrationId)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            try
            {
                StudentRegistrationViewModel? registration = null;

                // Lấy thông tin đăng ký (nhưng không redirect nếu lỗi)
                var regRes = await _httpClient.GetAsync($"api/profile/registration/{registrationId}");
                var regJson = await regRes.Content.ReadAsStringAsync();
                if (regRes.IsSuccessStatusCode)
                {
                    var regDetail = JsonSerializer.Deserialize<StudentRegistrationDetailDto>(regJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    registration = new StudentRegistrationViewModel
                    {
                        RegistrationId = regDetail?.RegistrationId ?? registrationId,
                        RegistrationCode = regDetail?.RegistrationCode,
                        RegistrationDate = regDetail?.RegistrationDate,
                        Status = regDetail?.Status,
                        CourseName = regDetail?.CourseName,
                        ClassName = regDetail?.ClassName,
                        StudyDate = regDetail?.StudyDate,
                        Email = regDetail?.Email,
                        PhoneNumber = regDetail?.PhoneNumber
                    };
                }
                else
                {
                    string msg = "Không tìm thấy thông tin đăng ký";
                    try
                    {
                        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(regJson) ? "{}" : regJson);
                        if (doc.RootElement.TryGetProperty("message", out var m) && !string.IsNullOrWhiteSpace(m.GetString()))
                            msg = m.GetString()!;
                    }
                    catch { /* ignore */ }
                    TempData["ErrorMessage"] = msg;
                }

                // Lấy hóa đơn theo đăng ký trước để có invoiceId
                StudentInvoiceViewModel? invoice = null;
                DigitalSignatureViewModel? signature = null;

                var invByReg = await _httpClient.GetAsync($"api/invoices/by-registration/{registrationId}");
                var invByRegJson = await invByReg.Content.ReadAsStringAsync();
                if (invByReg.IsSuccessStatusCode)
                {
                    try
                    {
                        using var invDoc = JsonDocument.Parse(invByRegJson);
                        JsonElement invData;
                        if (TryGetPropertyCI(invDoc.RootElement, "data", out var tmpData))
                        {
                            invData = tmpData;
                        }
                        else
                        {
                            // Fallback: API có thể trả trực tiếp object hóa đơn
                            invData = invDoc.RootElement;
                        }

                        int invoiceId;
                        if (TryGetPropertyCI(invData, "invoiceId", out var iidEl))
                        {
                            if (iidEl.ValueKind == JsonValueKind.Number && iidEl.TryGetInt32(out var tmp)) invoiceId = tmp;
                            else if (iidEl.ValueKind == JsonValueKind.String && int.TryParse(iidEl.GetString(), out tmp)) invoiceId = tmp;
                            else throw new Exception("invoiceId invalid");
                        }
                        else if (TryGetPropertyCI(invData, "idHoaDon", out var iidEl2) && iidEl2.ValueKind == JsonValueKind.Number && iidEl2.TryGetInt32(out var tmp2))
                        {
                            invoiceId = tmp2;
                        }
                        else
                        {
                            throw new Exception("invoiceId not found");
                        }

                        invoice = new StudentInvoiceViewModel
                        {
                            InvoiceId = invoiceId,
                            InvoiceCode = TryGetPropertyCI(invData, "invoiceCode", out var ic) ? ic.GetString() ?? string.Empty : string.Empty,
                            CreatedDate = GetDateTimeFlexible(invData, "createdDate"),
                            DueDate = GetDateTimeFlexible(invData, "dueDate"),
                            Amount = GetIntFlexible(invData, "amount", 0),
                            Status = TryGetPropertyCI(invData, "status", out var st) ? (st.GetString() ?? string.Empty) : string.Empty,
                            RegistrationId = registrationId
                        };

                        // Sau khi có invoiceId, lấy thông tin chữ ký số
                        var invSigRes = await _httpClient.GetAsync($"api/invoices/with-signature/{invoiceId}");
                        var invSigBody = await invSigRes.Content.ReadAsStringAsync();
                        if (invSigRes.IsSuccessStatusCode)
                        {
                            using var sigDoc = JsonDocument.Parse(invSigBody);
                            if (TryGetPropertyCI(sigDoc.RootElement, "data", out var data))
                            {
                                if (TryGetPropertyCI(data, "invoice", out var invFull))
                                {
                                    invoice.InvoiceCode = TryGetPropertyCI(invFull, "invoiceCode", out var ic2) ? ic2.GetString() ?? invoice.InvoiceCode : invoice.InvoiceCode;
                                    invoice.CreatedDate = GetDateTimeFlexible(invFull, "createdDate") ?? invoice.CreatedDate;
                                    invoice.DueDate = GetDateTimeFlexible(invFull, "dueDate") ?? invoice.DueDate;
                                    invoice.Amount = GetIntFlexible(invFull, "amount", invoice.Amount);
                                    invoice.Status = TryGetPropertyCI(invFull, "status", out var stf) ? stf.GetString() ?? invoice.Status : invoice.Status;
                                }

                                if (TryGetPropertyCI(data, "signature", out var sigData) && sigData.ValueKind != JsonValueKind.Null)
                                {
                                    signature = new DigitalSignatureViewModel
                                    {
                                        IsSigned = true,
                                        IsValid = TryGetPropertyCI(sigData, "isValid", out var iv) && (iv.ValueKind == JsonValueKind.True || (iv.ValueKind == JsonValueKind.String && bool.TryParse(iv.GetString(), out var boolVal) && boolVal)),
                                        SignedBy = TryGetPropertyCI(sigData, "signedBy", out var sb) ? sb.GetString() : null,
                                        SignedDate = TryGetPropertyCI(sigData, "signedDate", out var sd) && sd.ValueKind != JsonValueKind.Null ? sd.GetDateTime() : (DateTime?)null,
                                        Algorithm = TryGetPropertyCI(sigData, "algorithm", out var alg) ? alg.GetString() : null
                                    };
                                }
                                else
                                {
                                    if (TryGetPropertyCI(data, "invoice", out var invObj) &&
                                        (TryGetPropertyCI(invObj, "signatureBase64", out var sigBase) && sigBase.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(sigBase.GetString())
                                         || TryGetPropertyCI(invObj, "signedDate", out var sdt) && sdt.ValueKind != JsonValueKind.Null))
                                    {
                                        signature = new DigitalSignatureViewModel
                                        {
                                            IsSigned = true,
                                            IsValid = false,
                                            SignedBy = null,
                                            SignedDate = TryGetPropertyCI(invObj, "signedDate", out var sd2) && sd2.ValueKind != JsonValueKind.Null ? sd2.GetDateTime() : (DateTime?)null,
                                            Algorithm = TryGetPropertyCI(invObj, "algorithm", out var alg2) ? alg2.GetString() : null
                                        };
                                    }
                                    else
                                    {
                                        signature = new DigitalSignatureViewModel { IsSigned = false };
                                    }
                                }
                            }
                        }
                        else
                        {
                            string msg = "Không thể tải thông tin chữ ký";
                            try
                            {
                                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(invSigBody) ? "{}" : invSigBody);
                                if (doc.RootElement.TryGetProperty("message", out var m) && !string.IsNullOrWhiteSpace(m.GetString()))
                                    msg = m.GetString()!;
                            }
                            catch { msg = string.IsNullOrWhiteSpace(invSigBody) ? msg : invSigBody; }
                            TempData["ErrorMessage"] = msg;

                            if (TryGetPropertyCI(invData, "signatureBase64", out var sigBase2) && sigBase2.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(sigBase2.GetString()))
                            {
                                signature = new DigitalSignatureViewModel { IsSigned = true, IsValid = false };
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Deserialize invoice by-registration failed. Body: {Body}", invByRegJson);
                        TempData["ErrorMessage"] = "Dữ liệu hóa đơn trả về không hợp lệ.";
                    }
                }
                else
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(invByRegJson) ? "{}" : invByRegJson);
                        var msg = doc.RootElement.TryGetProperty("message", out var m) ? (m.GetString() ?? "Không tìm thấy hóa đơn cho đăng ký này") : "Không tìm thấy hóa đơn cho đăng ký này";
                        TempData["ErrorMessage"] = msg;
                    }
                    catch
                    {
                        TempData["ErrorMessage"] = "Không tìm thấy hóa đơn cho đăng ký này";
                    }
                }

                var viewModel = new StudentInvoiceDetailViewModel
                {
                    Registration = registration,
                    Invoice = invoice,
                    Signature = signature
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load invoice for registration {RegistrationId}", registrationId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải thông tin hóa đơn";
                return RedirectToAction("MyRegistrations");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Pay(int registrationId)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            // Reuse logic from ViewInvoice to fetch registration + invoice
            var vmResult = await ViewInvoice(registrationId) as ViewResult;
            if (vmResult?.Model is StudentInvoiceDetailViewModel detail && detail.Invoice != null)
            {
                // Hide backend error on this page
                TempData.Remove("ErrorMessage");
                return View("Pay", detail);
            }
            TempData["ErrorMessage"] = "Không tìm thấy hóa đơn để thanh toán";
            return RedirectToAction("MyRegistrations");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPay(int registrationId, int invoiceId)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            try
            {
                // Gọi API mới: học viên gửi yêu cầu xác nhận thanh toán
                var res = await _httpClient.PostAsync($"api/payments/student-request/{invoiceId}", null);
                var body = await res.Content.ReadAsStringAsync();
                
                using var doc = JsonDocument.Parse(body);
                var success = doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
                var message = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;

                if (success)
                {
                    TempData["SuccessMessage"] = message ?? "Đã gửi yêu cầu xác nhận thanh toán. Vui lòng chờ kế toán xác nhận.";
                    // Chuyển sang trang xem hóa đơn
                    return RedirectToAction("ViewInvoice", new { registrationId });
                }
                else
                {
                    TempData["ErrorMessage"] = message ?? "Không thể gửi yêu cầu thanh toán";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConfirmPay error for invoice {InvoiceId}", invoiceId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi xử lý thanh toán";
            }
            return RedirectToAction("Pay", new { registrationId });
        }

        // ==== Student QR lookup for registrations (scan/search only, no approve/reject) ====
        [HttpGet]
        public IActionResult QrLookupRegistrations(string? returnUrl)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("QrLookupRegistrations", "Student") });

            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? Url.Action("MyRegistrations", "Student") : returnUrl;
            return View("~/Views/Student/QrLookupRegistrations.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> QrSearchRegistrations(string q, string? returnUrl)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("QrLookupRegistrations", "Student") });

            if (string.IsNullOrWhiteSpace(q))
            {
                TempData["ErrorMessage"] = "Không có dữ liệu QR hoặc mã để tra cứu.";
                return RedirectToAction("QrLookupRegistrations", new { returnUrl });
            }

            try
            {
                // Trường hợp q là URL có chứa tham số q bên trong
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

                var text = q.Trim();
                var upper = text.ToUpperInvariant();
                bool tryClass = upper.StartsWith("CLASS:") || upper.StartsWith("LOP:") || (!upper.StartsWith("REG:") && !upper.StartsWith("REGID:") && !text.Contains("{"));

                if (tryClass)
                {
                    var classCode = upper.StartsWith("CLASS:") ? text.Substring(6).Trim() : (upper.StartsWith("LOP:") ? text.Substring(4).Trim() : text);
                    if (!string.IsNullOrWhiteSpace(classCode))
                    {
                        var clsRes = await _httpClient.GetAsync($"api/classes?search={Uri.EscapeDataString(classCode)}");
                        var clsBody = await clsRes.Content.ReadAsStringAsync();
                        if (clsRes.IsSuccessStatusCode)
                        {
                            var classes = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminClassItem>>(clsBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                            var match = classes.FirstOrDefault(c => string.Equals(c.ClassCode, classCode, StringComparison.OrdinalIgnoreCase));
                            if (match != null)
                            {
                                var regRes = await _httpClient.GetAsync($"api/registrations?status={Uri.EscapeDataString("Chờ duyệt")}&classCode={Uri.EscapeDataString(match.ClassCode)}");
                                var regBody = await regRes.Content.ReadAsStringAsync();
                                List<QLTTTA_WEB.Models.AdminRegistrationItem> regData = new();
                                if (regRes.IsSuccessStatusCode)
                                {
                                    regData = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminRegistrationItem>>(regBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                                }

                                if (regData.Count == 0)
                                {
                                    var regResAll = await _httpClient.GetAsync($"api/registrations?classCode={Uri.EscapeDataString(match.ClassCode)}");
                                    var regBodyAll = await regResAll.Content.ReadAsStringAsync();
                                    if (regResAll.IsSuccessStatusCode)
                                    {
                                        regData = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminRegistrationItem>>(regBodyAll, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                                    }
                                }
                                // Mine-only filter
                                var uidStr = HttpContext.Session.GetString("UserId");
                                int.TryParse(uidStr, out var sid);
                                if (sid > 0) regData = regData.Where(r => r.StudentId == sid).ToList(); else regData = new();

                                ViewBag.Query = q;
                                ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? Url.Action("MyRegistrations", "Student") : returnUrl;
                                return View("~/Views/Student/QrSearchResults.cshtml", regData);
                            }
                        }
                    }
                }

                // Mặc định: tra cứu theo mã/QR/ID đơn đăng ký
                var url = $"api/registrations/search?q={Uri.EscapeDataString(q)}";
                var res = await _httpClient.GetAsync(url);
                var body = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(body) ? "Tra cứu thất bại" : body;
                    return RedirectToAction("QrLookupRegistrations", new { returnUrl });
                }

                var data = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminRegistrationItem>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                // Mine-only filter
                var uidStr2 = HttpContext.Session.GetString("UserId");
                int.TryParse(uidStr2, out var sid2);
                if (sid2 > 0) data = data.Where(r => r.StudentId == sid2).ToList(); else data = new();
                ViewBag.Query = q;
                ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? Url.Action("MyRegistrations", "Student") : returnUrl;
                return View("~/Views/Student/QrSearchResults.cshtml", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Student QR search error");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tra cứu. Vui lòng thử lại.";
                return RedirectToAction("QrLookupRegistrations", new { returnUrl });
            }
        }
        // ==== End Student QR lookup ====

        // Xác thực chữ ký số (để học viên kiểm tra)
        [HttpGet]
        public async Task<IActionResult> VerifySignature(int invoiceId)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            try
            {
                var res = await _httpClient.GetAsync($"api/digitalsignature/verify-invoice/{invoiceId}");
                var json = await res.Content.ReadAsStringAsync();

                if (res.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(json);
                    var data = doc.RootElement.GetProperty("data");
                    
                    var result = new SignatureVerificationResult
                    {
                        Success = true,
                        IsValid = data.GetProperty("isValid").GetBoolean(),
                        SignedBy = data.TryGetProperty("signedBy", out var sb) ? sb.GetString() : null,
                        SignedDate = data.TryGetProperty("signedDate", out var sd) ? sd.GetDateTime() : null,
                        Message = doc.RootElement.GetProperty("message").GetString() ?? "",
                        InvoiceData = data.TryGetProperty("invoiceData", out var id) ? id.GetString() : null
                    };

                    return Json(result);
                }
                else
                {
                    return Json(new SignatureVerificationResult 
                    { 
                        Success = false, 
                        Message = "Không thể xác thực chữ ký" 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify signature for invoice {InvoiceId}", invoiceId);
                return Json(new SignatureVerificationResult 
                { 
                    Success = false, 
                    Message = "Có lỗi xảy ra khi xác thực chữ ký" 
                });
            }
        }

        // NEW: Trang upload PDF để xác thực chữ ký số (dùng public key TTTA)
        [HttpGet]
        public IActionResult VerifyPdf()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");
            return View();
        }

        // NEW: Xử lý upload PDF và gọi API xác thực
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPdfUpload(IFormFile pdf)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Account");

            if (pdf == null || pdf.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn file PDF";
                return RedirectToAction("VerifyPdf");
            }

            try
            {
                using var content = new MultipartFormDataContent();
                using var ms = new MemoryStream();
                await pdf.CopyToAsync(ms);
                var fileContent = new ByteArrayContent(ms.ToArray());
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                content.Add(fileContent, "pdf", pdf.FileName);

                var res = await _httpClient.PostAsync("api/digitalsignature/verify-pdf", content);
                var body = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(body);
                    var data = doc.RootElement.GetProperty("data");
                    var isValid = data.GetProperty("isValid").GetBoolean();
                    TempData["SuccessMessage"] = isValid ? "Xác thực hợp lệ" : "Xác thực không hợp lệ";
                    TempData["VerifyDetail"] = data.TryGetProperty("invoiceData", out var id) ? id.GetString() : null;
                }
                else
                {
                    string msg = "Không thể xác thực chữ ký";
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
                _logger.LogError(ex, "VerifyPdfUpload failed");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi xác thực PDF";
            }
            return RedirectToAction("VerifyPdf");
        }

        private class BasicApiResponse
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
        }

        private static bool TryGetPropertyCI(JsonElement obj, string name, out JsonElement value)
        {
            if (obj.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in obj.EnumerateObject())
                {
                    if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = p.Value;
                        return true;
                    }
                }
            }
            value = default;
            return false;
        }

        private static DateTime? GetDateTimeFlexible(JsonElement parent, string name)
        {
            if (TryGetPropertyCI(parent, name, out var el))
            {
                try
                {
                    if (el.ValueKind == JsonValueKind.Null) return null;
                    if (el.ValueKind == JsonValueKind.String)
                    {
                        var s = el.GetString();
                        if (DateTime.TryParse(s, out var dt)) return dt;
                        return null;
                    }
                    if (el.ValueKind == JsonValueKind.Number)
                    {
                        // epoch milliseconds/seconds fallback if used
                        if (el.TryGetInt64(out var num))
                        {
                            // heuristic: if too large, treat as ms
                            if (num > 10_000_000_000) // > ~Sat Nov 20 2286 for seconds; assume ms
                                return DateTimeOffset.FromUnixTimeMilliseconds(num).LocalDateTime;
                            return DateTimeOffset.FromUnixTimeSeconds(num).LocalDateTime;
                        }
                    }
                    // Try as ISO via GetDateTime if native
                    if (el.ValueKind == JsonValueKind.String || el.ValueKind == JsonValueKind.Object)
                    {
                        return el.GetDateTime();
                    }
                }
                catch { }
            }
            return null;
        }

        private static int GetIntFlexible(JsonElement parent, string name, int defaultValue)
        {
            if (TryGetPropertyCI(parent, name, out var el))
            {
                try
                {
                    if (el.ValueKind == JsonValueKind.Number)
                    {
                        if (el.TryGetInt32(out var i)) return i;
                        if (el.TryGetInt64(out var l)) return unchecked((int)l);
                    }
                    if (el.ValueKind == JsonValueKind.String)
                    {
                        var s = el.GetString();
                        if (int.TryParse(s, out var i)) return i;
                        if (long.TryParse(s, out var l)) return unchecked((int)l);
                    }
                }
                catch { }
            }
            return defaultValue;
        }

        // ================= Admin: Quản lý học viên (gom từ StudentsController) =================
        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 10, string? search = null)
        {
            if (!IsStaff())
                return RedirectToAction("Index", "Home");

            try
            {
                var queryString = $"?pageNumber={pageNumber}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(search))
                    queryString += $"&search={Uri.EscapeDataString(search)}";

                var response = await _httpClient.GetAsync($"api/students{queryString}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<PaginatedApiResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    var viewModel = new PaginatedViewModel<StudentViewModel>
                    {
                        Items = apiResponse?.Data?.Select(MapToStudentViewModel).ToList() ?? new(),
                        TotalRecords = apiResponse?.TotalRecords ?? 0,
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        SearchTerm = search
                    };

                    return View(viewModel);
                }

                TempData["ErrorMessage"] = "Không thể tải danh sách học viên";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading students");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải danh sách học viên";
            }

            return View(new PaginatedViewModel<StudentViewModel>());
        }

        public async Task<IActionResult> Details(int id)
        {
            if (!IsStaff())
                return RedirectToAction("Index", "Home");

            try
            {
                var response = await _httpClient.GetAsync($"api/students/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<StudentApiResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (apiResponse?.Success == true && apiResponse.Data != null)
                    {
                        return View(MapToStudentViewModel(apiResponse.Data));
                    }
                }

                TempData["ErrorMessage"] = "Không tìm thấy học viên";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student details");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải thông tin học viên";
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Create()
        {
            if (!IsStaff())
                return RedirectToAction("Index", "Home");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StudentCreateViewModel model)
        {
            if (!IsStaff())
                return RedirectToAction("Index", "Home");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var createDto = new
                {
                    FullName = model.FullName,
                    Sex = model.Sex,
                    DateOfBirth = model.DateOfBirth,
                    PhoneNumber = model.PhoneNumber,
                    Address = model.Address,
                    Username = model.Username,
                    Password = model.Password,
                    Email = model.Email
                };

                var json = JsonSerializer.Serialize(createDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("api/students", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                var apiResponse = JsonSerializer.Deserialize<StudentApiResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true)
                {
                    TempData["SuccessMessage"] = "Tạo học viên thành công!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    ModelState.AddModelError("", apiResponse?.Message ?? "Có lỗi xảy ra khi tạo học viên");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating student");
                ModelState.AddModelError("", "Có lỗi xảy ra khi tạo học viên");
            }

            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!IsStaff())
                return RedirectToAction("Index", "Home");

            try
            {
                var response = await _httpClient.GetAsync($"api/students/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<StudentApiResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (apiResponse?.Success == true && apiResponse.Data != null)
                    {
                        var student = apiResponse.Data;
                        var viewModel = new StudentEditViewModel
                        {
                            StudentId = student.StudentId,
                            FullName = student.FullName ?? "",
                            StudentCode = student.StudentCode ?? "",
                            Sex = student.Sex,
                            DateOfBirth = student.DateOfBirth ?? DateTime.Now,
                            PhoneNumber = student.PhoneNumber ?? "",
                            Address = student.Address
                        };

                        return View(viewModel);
                    }
                }

                TempData["ErrorMessage"] = "Không tìm thấy học viên";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student for edit");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải thông tin học viên";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StudentEditViewModel model)
        {
            if (!IsStaff())
                return RedirectToAction("Index", "Home");

            if (id != model.StudentId)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var updateDto = new
                {
                    StudentId = model.StudentId,
                    FullName = model.FullName,
                    StudentCode = model.StudentCode,
                    Sex = model.Sex,
                    DateOfBirth = model.DateOfBirth,
                    PhoneNumber = model.PhoneNumber,
                    Address = model.Address
                };

                var json = JsonSerializer.Serialize(updateDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"api/students/{id}", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                var apiResponse = JsonSerializer.Deserialize<StudentApiResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true)
                {
                    TempData["SuccessMessage"] = "Cập nhật học viên thành công!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    ModelState.AddModelError("", apiResponse?.Message ?? "Có lỗi xảy ra khi cập nhật học viên");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating student");
                ModelState.AddModelError("", "Có lỗi xảy ra khi cập nhật học viên");
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsStaff())
                return RedirectToAction("Index", "Home");

            try
            {
                var response = await _httpClient.DeleteAsync($"api/students/{id}");
                var responseContent = await response.Content.ReadAsStringAsync();

                var apiResponse = JsonSerializer.Deserialize<ApiResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true)
                {
                    TempData["SuccessMessage"] = "Xóa học viên thành công!";
                }
                else
                {
                    TempData["ErrorMessage"] = apiResponse?.Message ?? "Có lỗi xảy ra khi xóa học viên";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting student");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi xóa học viên";
            }

            return RedirectToAction(nameof(Index));
        }

        // Public course details for QR landing
        [HttpGet("Students/CourseDetails/{courseId}")]
        [AllowAnonymous]
        public async Task<IActionResult> CourseDetails(int courseId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/courses/{courseId}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var course = JsonSerializer.Deserialize<CourseViewModel>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (course != null)
                        return View("PublicCourseDetails", course);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading public course details {CourseId}", courseId);
            }
            return View("PublicNotFound");
        }

        private bool IsAuthenticated() => !string.IsNullOrEmpty(HttpContext.Session.GetString("UserId"));

        private StudentViewModel MapToStudentViewModel(StudentData student)
        {
            return new StudentViewModel
            {
                StudentId = student.StudentId,
                FullName = student.FullName ?? "",
                StudentCode = student.StudentCode ?? "",
                Sex = student.Sex,
                DateOfBirth = student.DateOfBirth,
                PhoneNumber = student.PhoneNumber ?? "",
                Address = student.Address
            };
        }

        private class PaginatedApiResponse
        {
            public List<StudentData> Data { get; set; } = new();
            public int TotalRecords { get; set; }
            public int PageNumber { get; set; }
            public int PageSize { get; set; }
        }

        private class StudentApiResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
            public StudentData? Data { get; set; }
        }

        private class ApiResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
        }

        private class StudentData
        {
            public int StudentId { get; set; }
            public string? FullName { get; set; }
            public string? StudentCode { get; set; }
            public string? Sex { get; set; }
            public DateTime? DateOfBirth { get; set; }
            public string? PhoneNumber { get; set; }
            public string? Address { get; set; }
        }
        // ================= End Admin: Quản lý học viên =================
    }
}
