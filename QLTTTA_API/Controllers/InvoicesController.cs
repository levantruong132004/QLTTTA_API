using Microsoft.AspNetCore.Mvc;
using QLTTTA_API.Models;
using QLTTTA_API.Services;
using Oracle.ManagedDataAccess.Client; // Added for OracleException handling
using QLTTTA_API.Models.DTOs; // for StudentPaymentDto
using Microsoft.AspNetCore.Http; // for IFormFile
using System.Text.Json; // added for invoiceData rebuild

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly IDigitalSignatureService _digitalSignatureService;
        private readonly IPdfSignatureService _pdfSignatureService; // add
        private readonly IEmailService _emailService; // add
        private readonly ILogger<InvoicesController> _logger;

        public InvoicesController(IInvoiceService invoiceService, IDigitalSignatureService digitalSignatureService, IPdfSignatureService pdfSignatureService, IEmailService emailService, ILogger<InvoicesController> logger)
        {
            _invoiceService = invoiceService;
            _digitalSignatureService = digitalSignatureService;
            _pdfSignatureService = pdfSignatureService; // assign
            _emailService = emailService; // assign
            _logger = logger;
        }

        public class CreateInvoiceRequest
        {
            public int RegistrationId { get; set; }
            public DateTime DueDate { get; set; }
            public int Amount { get; set; }
        }

        public class CreateAndSignInvoiceRequest
        {
            public int RegistrationId { get; set; }
            public DateTime DueDate { get; set; }
            public int Amount { get; set; }
            public string PrivateKeyPath { get; set; } = "";
            public int AccountantId { get; set; }
        }

        public class PrintAndEmailInvoiceRequest
        {
            public int InvoiceId { get; set; }
            public int AccountantId { get; set; }
        }

        public class PrintSignEmailForm
        {
            public int InvoiceId { get; set; }
            public int AccountantId { get; set; }
            public IFormFile? PrivateKey { get; set; }
        }

        // =============== ACCOUNTANT OPERATIONS - Kế toán tạo và ký số hóa đơn ===============

        /// <summary>
        /// Kế toán tạo hóa đơn cho đơn đăng ký đã được phê duyệt
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest req)
        {
            if (req == null || req.RegistrationId <= 0 || req.Amount <= 0)
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
            
            try
            {
                var res = await _invoiceService.CreateAsync(req.RegistrationId, req.DueDate, req.Amount);
                return StatusCode(res.Success ? 200 : 400, new { success = res.Success, message = res.Message, data = res.Data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice for registration {RegistrationId}", req.RegistrationId);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tạo hóa đơn" });
            }
        }

        /// <summary>
        /// Kế toán tạo và ký số hóa đơn cùng lúc
        /// Đây là chức năng chính cho kế toán: tạo hóa đơn từ đơn đã duyệt và ký số ngay
        /// </summary>
        [HttpPost("create-and-sign")]
        public async Task<IActionResult> CreateAndSign([FromBody] CreateAndSignInvoiceRequest req)
        {
            if (req == null || req.RegistrationId <= 0 || req.Amount <= 0 || string.IsNullOrEmpty(req.PrivateKeyPath))
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });

            try
            {
                // Tạo hóa đơn trước
                var createResult = await _invoiceService.CreateAsync(req.RegistrationId, req.DueDate, req.Amount);
                if (!createResult.Success)
                {
                    return StatusCode(400, new { success = false, message = createResult.Message });
                }

                // Ký số hóa đơn vừa tạo
                var signResult = await _digitalSignatureService.SignInvoiceAsync(
                    createResult.Data!.InvoiceId,
                    req.PrivateKeyPath,
                    req.AccountantId
                );

                if (!signResult.Success)
                {
                    return StatusCode(400, new
                    {
                        success = false,
                        message = $"Tạo hóa đơn thành công nhưng ký số thất bại: {signResult.Message}",
                        invoiceData = createResult.Data
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Tạo và ký hóa đơn thành công",
                    data = new
                    {
                        invoice = createResult.Data,
                        signature = signResult.SignatureBase64,
                        signedData = signResult.InvoiceData
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating and signing invoice for registration {RegistrationId}", req.RegistrationId);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tạo và ký hóa đơn" });
            }
        }

        /// <summary>
        /// In hóa đơn và gửi PDF qua email cho học viên
        /// </summary>
        [HttpPost("print-and-email")]
        public async Task<IActionResult> PrintAndEmailInvoice([FromBody] PrintAndEmailInvoiceRequest req)
        {
            if (req == null || req.InvoiceId <= 0 || req.AccountantId <= 0)
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });

            try
            {
                // 1. Lấy thông tin hóa đơn
                var invoice = await _invoiceService.GetByIdAsync(req.InvoiceId);
                if (invoice == null)
                    return NotFound(new { success = false, message = "Không tìm thấy hóa đơn" });

                // 2. Gọi service để tạo PDF và gửi email
                var result = await _invoiceService.GeneratePdfAndSendEmailAsync(req.InvoiceId, req.AccountantId);
                
                if (result.Success)
                {
                    return Ok(new 
                    { 
                        success = true, 
                        message = "✅ IN HÓA ĐƠN THÀNH CÔNG - Đã gửi PDF qua email cho học viên",
                        data = new 
                        {
                            invoiceCode = invoice.InvoiceCode,
                            emailSent = result.Data?.EmailAddress,
                            pdfGenerated = true,
                            action = "PRINT_AND_EMAIL" // Để frontend phân biệt
                        }
                    });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = result.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PrintAndEmailInvoice error for InvoiceId {InvoiceId}", req.InvoiceId);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi in hóa đơn và gửi email" });
            }
        }

        /// <summary>
        /// In hóa đơn: ký số ngay lúc in và gửi PDF qua email cho học viên (multipart/form-data)
        /// form fields: invoiceId (int), accountantId (int), privateKey (file .pem)
        /// </summary>
        [HttpPost("print-sign-email")]
        [Consumes("multipart/form-data")] // giúp Swagger hiển thị form upload
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> PrintSignAndEmail([FromForm] PrintSignEmailForm form)
        {
            try
            {
                if (form == null)
                    return BadRequest(new { success = false, message = "Thiếu dữ liệu form" });
                if (form.InvoiceId <= 0 || form.AccountantId <= 0)
                    return BadRequest(new { success = false, message = "Thiếu hoặc sai invoiceId/accountantId" });
                if (form.PrivateKey == null || form.PrivateKey.Length == 0)
                    return BadRequest(new { success = false, message = "Chưa chọn file private key" });

                var invoiceId = form.InvoiceId;
                var accountantId = form.AccountantId;

                // Lấy hóa đơn
                var invoice = await _invoiceService.GetByIdAsync(invoiceId);
                if (invoice == null)
                    return NotFound(new { success = false, message = "Không tìm thấy hóa đơn" });

                // Lấy thông tin học viên/khóa học qua service
                var (studentName, studentEmail, courseName, className) = await _invoiceService.GetStudentCourseInfoAsync(invoice.RegistrationId);
                if (string.IsNullOrWhiteSpace(studentEmail))
                    return BadRequest(new { success = false, message = "Học viên chưa có email để gửi" });

                string signatureBase64;
                string invoiceDataJson;
                bool alreadySigned = !string.IsNullOrEmpty(invoice.SignatureBase64);

                if (alreadySigned)
                {
                    // Hóa đơn đã ký trước đó: tái tạo dữ liệu để nhúng vào PDF v3
                    invoiceDataJson = JsonSerializer.Serialize(new {
                        invoice.InvoiceCode,
                        CreatedDate = invoice.CreatedDate?.ToString("yyyy-MM-dd HH:mm:ss"),
                        DueDate = invoice.DueDate?.ToString("yyyy-MM-dd HH:mm:ss"),
                        invoice.Amount,
                        invoice.RegistrationId
                    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    signatureBase64 = invoice.SignatureBase64!;
                }
                else
                {
                    // Đọc private key và ký mới
                    string privateKeyPem;
                    using (var s = form.PrivateKey.OpenReadStream())
                    using (var r = new StreamReader(s))
                        privateKeyPem = await r.ReadToEndAsync();

                    var signResult = await _digitalSignatureService.SignInvoiceWithPrivateKeyContentAsync(invoiceId, privateKeyPem, accountantId);
                    if (!signResult.Success)
                        return StatusCode(400, new { success = false, message = signResult.Message });

                    signatureBase64 = signResult.SignatureBase64 ?? string.Empty;
                    invoiceDataJson = signResult.InvoiceData ?? string.Empty;
                    // Refresh invoice info to capture signature just stored
                    invoice = await _invoiceService.GetByIdAsync(invoiceId) ?? invoice;
                }

                // Tạo PDF có chữ ký số tích hợp (v3)
                _logger.LogInformation("🔄 Bắt đầu tạo PDF có chữ ký cho hóa đơn {InvoiceId}", invoiceId);
                var pdfBytes = await _pdfSignatureService.CreateCryptographicallySignedPdfAsync(
                    invoiceId,
                    invoiceDataJson,
                    signatureBase64,
                    studentName,
                    courseName,
                    className
                );
                _logger.LogInformation("✅ Đã tạo PDF có chữ ký cho hóa đơn {InvoiceId}, size = {Size} bytes", invoiceId, pdfBytes.Length);

                var fileName = $"HoaDon_{invoice.InvoiceCode}_{DateTime.Now:yyyyMMdd_HHmmss}_Signed.pdf";

                // Gửi email cho học viên
                var subject = $"Hóa đơn đã ký số - {courseName}";
                var body = $"<p>Chào {studentName},</p><p>Hóa đơn {invoice.InvoiceCode} cho khóa <strong>{courseName}</strong> lớp <strong>{className}</strong> đã được ký số và đính kèm.</p><p>Trân trọng,<br/>Trung tâm Tin học LDA</p>";
                var emailSent = await _emailService.SendEmailWithAttachmentAsync(studentEmail, subject, body, pdfBytes, fileName);

                // Cập nhật DA_IN = 1 qua service (dù đã ký trước hay vừa ký)
                await _invoiceService.MarkPrintedAsync(invoiceId);

                Response.Headers["X-Email-Sent"] = emailSent ? "true" : "false";
                Response.Headers["X-Already-Signed"] = alreadySigned ? "true" : "false";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (OracleException oex)
            {
                _logger.LogError(oex, "Oracle error in PrintSignAndEmail for {InvoiceId}", form?.InvoiceId);
                return StatusCode(500, new { success = false, message = $"Lỗi Oracle ({oex.Number}): {oex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ PrintSignAndEmail error for InvoiceId={InvoiceId}: {Message}", form?.InvoiceId, ex.Message);
                _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
                return StatusCode(500, new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
            }
        }

        // =============== PUBLIC/STUDENT OPERATIONS - Học viên và kế toán xem hóa đơn ===============

        /// <summary>
        /// Lấy hóa đơn theo đơn đăng ký - Công khai cho học viên xem hóa đơn của mình
        /// Có kiểm tra và xác thực chữ ký số nếu có
        /// </summary>
        [HttpGet("by-registration/{registrationId:int}")]
        public async Task<IActionResult> GetByRegistration([FromRoute] int registrationId)
        {
            try
            {
                var inv = await _invoiceService.GetByRegistrationAsync(registrationId);
                if (inv == null) return NotFound(new { success = false, message = "Không tìm thấy hóa đơn" });
                
                // Kiểm tra và xác thực chữ ký nếu có
                object? signaturePayload = null;
                var hasStoredSignature = !string.IsNullOrEmpty(inv.SignatureBase64);
                if (hasStoredSignature)
                {
                    try
                    {
                        var verifyResult = await _digitalSignatureService.VerifyInvoiceSignatureAsync(inv.InvoiceId);
                        if (verifyResult.Success)
                        {
                            signaturePayload = new
                            {
                                isValid = verifyResult.IsValidSignature,
                                signedBy = verifyResult.SignedBy,
                                signedDate = verifyResult.SignedDate,
                                algorithm = string.IsNullOrEmpty(inv.Algorithm) ? "RSA-SHA256" : inv.Algorithm
                            };
                        }
                        else
                        {
                            signaturePayload = new
                            {
                                isValid = false,
                                signedBy = (string?)null,
                                signedDate = (DateTime?)null,
                                algorithm = string.IsNullOrEmpty(inv.Algorithm) ? "RSA-SHA256" : inv.Algorithm
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Verify invoice signature error for registration {RegistrationId}", registrationId);
                        signaturePayload = new
                        {
                            isValid = false,
                            signedBy = (string?)null,
                            signedDate = (DateTime?)null,
                            algorithm = string.IsNullOrEmpty(inv.Algorithm) ? "RSA-SHA256" : inv.Algorithm
                        };
                    }
                }

                return Ok(new 
                { 
                    success = true, 
                    data = new
                    {
                        invoice = inv,
                        signature = signaturePayload
                    }
                });
            }
            catch (OracleException oex)
            {
                _logger.LogError(oex, "Oracle error when GetByRegistration for {RegistrationId}", registrationId);
                return StatusCode(500, new { success = false, message = $"Lỗi Oracle ({oex.Number}): {oex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error GetByRegistration for {RegistrationId}", registrationId);
                return StatusCode(500, new { success = false, message = "Lỗi máy chủ khi lấy hóa đơn theo đơn đăng ký" });
            }
        }

        /// <summary>
        /// Lấy hóa đơn có thông tin chữ ký số - Dành cho kế toán và học viên xem chi tiết
        /// </summary>
        [HttpGet("with-signature/{invoiceId:int}")]
        public async Task<IActionResult> GetInvoiceWithSignature([FromRoute] int invoiceId)
        {
            try
            {
                var inv = await _invoiceService.GetByIdAsync(invoiceId);
                if (inv == null) return NotFound(new { success = false, message = "Không tìm thấy hóa đơn" });

                // Kiểm tra và xác thực chữ ký nếu có
                object? signaturePayload = null;
                var hasStoredSignature = !string.IsNullOrEmpty(inv.SignatureBase64);
                if (hasStoredSignature)
                {
                    try
                    {
                        var verifyResult = await _digitalSignatureService.VerifyInvoiceSignatureAsync(invoiceId);
                        if (verifyResult.Success)
                        {
                            signaturePayload = new
                            {
                                isValid = verifyResult.IsValidSignature,
                                signedBy = verifyResult.SignedBy,
                                signedDate = verifyResult.SignedDate,
                                algorithm = string.IsNullOrEmpty(inv.Algorithm) ? "RSA-SHA256" : inv.Algorithm
                            };
                        }
                        else
                        {
                            signaturePayload = new
                            {
                                isValid = false,
                                signedBy = (string?)null,
                                signedDate = (DateTime?)null,
                                algorithm = string.IsNullOrEmpty(inv.Algorithm) ? "RSA-SHA256" : inv.Algorithm
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Verify invoice signature error for {InvoiceId}", invoiceId);
                        signaturePayload = new
                        {
                            isValid = false,
                            signedBy = (string?)null,
                            signedDate = (DateTime?)null,
                            algorithm = string.IsNullOrEmpty(inv.Algorithm) ? "RSA-SHA256" : inv.Algorithm
                        };
                    }
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        invoice = inv,
                        signature = signaturePayload
                    }
                });
            }
            catch (OracleException oex)
            {
                _logger.LogError(oex, "Oracle error when GetInvoiceWithSignature for {InvoiceId}", invoiceId);
                return StatusCode(500, new { success = false, message = $"Lỗi Oracle ({oex.Number}): {oex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error GetInvoiceWithSignature for {InvoiceId}", invoiceId);
                return StatusCode(500, new { success = false, message = "Lỗi máy chủ khi lấy hóa đơn có chữ ký" });
            }
        }

        /// <summary>
        /// Học viên bắt đầu thanh toán - Chuyển trạng thái hóa đơn để đánh dấu đang xử lý
        /// </summary>
        [HttpPost("student-pay")]
        public async Task<IActionResult> StudentPay([FromBody] StudentPaymentDto dto)
        {
            if (dto == null || dto.InvoiceId <= 0 || string.IsNullOrWhiteSpace(dto.PaymentMethod))
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
            try
            {
                var inv = await _invoiceService.GetByIdAsync(dto.InvoiceId);
                if (inv == null) return NotFound(new { success = false, message = "Không tìm thấy hóa đơn" });
                if (string.Equals(inv.Status, "Đã thanh toán", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { success = false, message = "Hóa đơn đã thanh toán" });

                // Đặt trạng thái tạm: Chờ xác nhận thanh toán
                var ok = await _invoiceService.UpdateStatusAsync(dto.InvoiceId, "Chờ xác nhận thanh toán");
                if (!ok) return StatusCode(500, new { success = false, message = "Không cập nhật được trạng thái hóa đơn" });

                // Lấy lại thông tin chi tiết để trả về cho màn xác nhận
                inv = await _invoiceService.GetByIdAsync(dto.InvoiceId);
                return Ok(new { success = true, message = "Đã ghi nhận yêu cầu thanh toán", data = inv });
            }
            catch (OracleException oex)
            {
                _logger.LogError(oex, "Oracle error StudentPay for {InvoiceId}", dto.InvoiceId);
                return StatusCode(500, new { success = false, message = "Không thể xử lý thanh toán hiện tại" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StudentPay error for {InvoiceId}", dto.InvoiceId);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi xử lý thanh toán" });
            }
        }
    }
}
