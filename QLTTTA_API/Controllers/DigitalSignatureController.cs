using Microsoft.AspNetCore.Mvc;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DigitalSignatureController : ControllerBase
    {
        private readonly IDigitalSignatureService _digitalSignatureService;
        private readonly IPdfSignatureService _pdfSignatureService;
        private readonly ILogger<DigitalSignatureController> _logger;
        private readonly IEmailService _emailService;

        public DigitalSignatureController(
            IDigitalSignatureService digitalSignatureService, 
            IPdfSignatureService pdfSignatureService,
            ILogger<DigitalSignatureController> logger,
            IEmailService emailService)
        {
            _digitalSignatureService = digitalSignatureService;
            _pdfSignatureService = pdfSignatureService;
            _logger = logger;
            _emailService = emailService;
        }

        /// <summary>
        /// Tạo cặp khóa RSA cho kế toán
        /// </summary>
        [HttpPost("generate-keypair")]
        public async Task<IActionResult> GenerateKeyPair()
        {
            var result = await _digitalSignatureService.GenerateKeyPairAsync();
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new 
            { 
                success = true, 
                message = result.Message,
                data = new 
                {
                    publicKey = result.PublicKeyPem,
                    privateKey = result.PrivateKeyPem,
                    instructions = new
                    {
                        publicKey = "Lưu public key này vào database (bảng KE_TOAN, cột KHOA_CONG_PEM)",
                        privateKey = "Lưu private key này vào file trên máy cá nhân (.pem hoặc .key) và bảo mật cẩn thận"
                    }
                }
            });
        }

        /// <summary>
        /// Admin: Tạo cặp khóa trung tâm và tự động lưu public key vào bảng TTTA
        /// Private key sẽ được trả về trong response để admin download về máy
        /// </summary>
        [HttpPost("generate-center-keypair/save")]
        public async Task<IActionResult> GenerateCenterKeyPairAndSave([FromBody] GenerateCenterKeyPairRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CenterName))
            {
                return BadRequest(new { success = false, message = "Vui lòng nhập tên trung tâm" });
            }
            
            // Lấy accountantId từ session (user đang đăng nhập)
            var accountantId = 1; // Tạm thời dùng 1, sau này lấy từ session
            
            var result = await _digitalSignatureService.GenerateCenterKeyPairAndSaveAsync(
                accountantId, 
                request.CenterName, 
                request.Address ?? string.Empty, 
                request.Phone ?? string.Empty
            );
            
            if (!result.Success)
            {
                return StatusCode(400, new { success = false, message = result.Message });
            }
            
            // Trả về cả private key và public key để frontend download
            return Ok(new 
            { 
                success = true, 
                message = result.Message,
                data = new 
                {
                    publicKey = result.PublicKeyPem,
                    privateKey = result.PrivateKeyPem, // Trả về để frontend download
                    centerName = request.CenterName
                }
            });
        }

        /// <summary>
        /// Ký số cho hóa đơn
        /// </summary>
        [HttpPost("sign-invoice")]
        public async Task<IActionResult> SignInvoice([FromBody] SignInvoiceRequest request)
        {
            if (request == null || request.InvoiceId <= 0 || string.IsNullOrEmpty(request.PrivateKeyPath))
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
            }

            var result = await _digitalSignatureService.SignInvoiceAsync(
                request.InvoiceId, 
                request.PrivateKeyPath, 
                request.AccountantId
            );

            return StatusCode(result.Success ? 200 : 400, new 
            { 
                success = result.Success, 
                message = result.Message,
                data = result.Success ? new 
                {
                    signature = result.SignatureBase64,
                    invoiceData = result.InvoiceData
                } : null
            });
        }

        /// <summary>
        /// Kế toán: Ký số hóa đơn bằng cách upload file private key (multipart/form-data)
        /// form fields: invoiceId (int), accountantId (int), privateKey (file .pem)
        /// </summary>
        [HttpPost("sign-invoice/upload")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> SignInvoiceWithUpload()
        {
            try
            {
                if (!Request.HasFormContentType)
                    return BadRequest(new { success = false, message = "Sai định dạng dữ liệu (cần multipart/form-data)" });

                var form = await Request.ReadFormAsync();
                if (!int.TryParse(form["invoiceId"], out var invoiceId) || !int.TryParse(form["accountantId"], out var accountantId))
                {
                    return BadRequest(new { success = false, message = "Thiếu hoặc sai invoiceId/accountantId" });
                }
                var file = form.Files["privateKey"];
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { success = false, message = "Chưa chọn file private key" });
                }
                string privateKeyPem;
                using (var stream = file.OpenReadStream())
                using (var reader = new StreamReader(stream))
                {
                    privateKeyPem = await reader.ReadToEndAsync();
                }

                var result = await _digitalSignatureService.SignInvoiceWithPrivateKeyContentAsync(invoiceId, privateKeyPem, accountantId);
                return StatusCode(result.Success ? 200 : 400, new { 
                    success = result.Success, 
                    message = result.Success ? "🔐 KÝ SỐ THÀNH CÔNG - Hóa đơn đã được ký. Nhấn 'In hóa đơn' để gửi PDF cho học viên." : result.Message,
                    data = result.Success ? new { 
                        signature = result.SignatureBase64, 
                        invoiceData = result.InvoiceData,
                        action = "DIGITAL_SIGN_ONLY" // Để frontend phân biệt
                    } : null 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignInvoiceWithUpload error");
                return StatusCode(500, new { success = false, message = "Lỗi xử lý ký số" });
            }
        }

        /// <summary>
        /// Xác thực chữ ký của hóa đơn
        /// </summary>
        [HttpGet("verify-invoice/{invoiceId:int}")]
        public async Task<IActionResult> VerifyInvoiceSignature([FromRoute] int invoiceId)
        {
            var result = await _digitalSignatureService.VerifyInvoiceSignatureAsync(invoiceId);
            
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new 
            { 
                success = true, 
                message = result.Message,
                data = new 
                {
                    isValid = result.IsValidSignature,
                    signedBy = result.SignedBy,
                    signedDate = result.SignedDate,
                    invoiceData = result.InvoiceData
                }
            });
        }

        /// <summary>
        /// Học viên: Upload file PDF hóa đơn để xác thực chữ ký (dùng public key trung tâm TTTA)
        /// form field: pdf (file application/pdf)
        /// </summary>
        [HttpPost("verify-pdf")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> VerifyPdf()
        {
            try
            {
                if (!Request.HasFormContentType)
                    return BadRequest(new { success = false, message = "Sai định dạng dữ liệu (cần multipart/form-data)" });

                var form = await Request.ReadFormAsync();
                var file = form.Files["pdf"];
                if (file == null || file.Length == 0)
                    return BadRequest(new { success = false, message = "Chưa chọn file PDF" });

                byte[] pdfBytes;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    pdfBytes = ms.ToArray();
                }

                var result = await _digitalSignatureService.VerifyPdfSignatureAsync(pdfBytes);
                return StatusCode(result.Success ? 200 : 400, new { success = result.Success, message = result.Message, data = result.Success ? new { isValid = result.IsValidSignature, invoiceData = result.InvoiceData } : null });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyPdf error");
                return StatusCode(500, new { success = false, message = "Lỗi xử lý xác thực PDF" });
            }
        }

        /// <summary>
        /// Đối chiếu file PDF bằng cách băm file và so sánh với hash lưu trong DB
        /// </summary>
        [HttpPost("verify-file-hash")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> VerifyFileHash()
        {
            try
            {
                if (!Request.HasFormContentType)
                    return BadRequest(new { success = false, message = "Sai định dạng dữ liệu (cần multipart/form-data)" });

                var form = await Request.ReadFormAsync();
                if (!int.TryParse(form["invoiceId"], out var invoiceId))
                {
                    return BadRequest(new { success = false, message = "Thiếu hoặc sai invoiceId" });
                }

                var file = form.Files["pdf"];
                if (file == null || file.Length == 0)
                    return BadRequest(new { success = false, message = "Chưa chọn file PDF" });

                byte[] pdfBytes;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    pdfBytes = ms.ToArray();
                }

                var result = await _digitalSignatureService.VerifyPdfIntegrityByHashAsync(invoiceId, pdfBytes);
                return StatusCode(result.Success ? 200 : 400, new
                {
                    success = result.Success,
                    message = result.Message,
                    data = result.Success ? new
                    {
                        isIntact = result.IsValidSignature,
                        details = result.InvoiceData
                    } : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyFileHash error");
                return StatusCode(500, new { success = false, message = "Lỗi xử lý đối chiếu file" });
            }
        }

        /// <summary>
        /// Xuất public key của kế toán
        /// </summary>
        [HttpGet("export-public-key/{accountantId:int}")]
        public async Task<IActionResult> ExportPublicKey([FromRoute] int accountantId)
        {
            var publicKey = await _digitalSignatureService.ExportPublicKeyAsync(accountantId);
            
            if (string.IsNullOrEmpty(publicKey))
            {
                return NotFound(new { success = false, message = "Không tìm thấy public key" });
            }

            return Ok(new 
            { 
                success = true, 
                data = new 
                {
                    accountantId = accountantId,
                    publicKey = publicKey
                }
            });
        }

        /// <summary>
        /// [NEW] Kế toán: Ký số và tạo PDF có chữ ký số tích hợp Version 3.0 - Một bước duy nhất
        /// form fields: invoiceId (int), accountantId (int), privateKey (file .pem), studentName (string), courseName (string), className (string), studentEmail (string optional - nếu gửi sẽ email PDF cho học viên)
        /// </summary>
        [HttpPost("sign-and-create-pdf-v3")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> SignAndCreatePdfV3()
        {
            try
            {
                if (!Request.HasFormContentType)
                    return BadRequest(new { success = false, message = "Sai định dạng dữ liệu (cần multipart/form-data)" });

                var form = await Request.ReadFormAsync();
                
                // Parse form data
                if (!int.TryParse(form["invoiceId"], out var invoiceId) || 
                    !int.TryParse(form["accountantId"], out var accountantId))
                {
                    return BadRequest(new { success = false, message = "Thiếu hoặc sai invoiceId/accountantId" });
                }

                var file = form.Files["privateKey"];
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { success = false, message = "Chưa chọn file private key" });
                }

                var studentName = form["studentName"].ToString();
                var courseName = form["courseName"].ToString(); 
                var className = form["className"].ToString();
                var studentEmail = form["studentEmail"].ToString(); // optional

                if (string.IsNullOrEmpty(studentName) || string.IsNullOrEmpty(courseName) || string.IsNullOrEmpty(className))
                {
                    return BadRequest(new { success = false, message = "Thiếu thông tin học viên/khóa học/lớp học" });
                }

                // Đọc private key content
                string privateKeyPem;
                using (var stream = file.OpenReadStream())
                using (var reader = new StreamReader(stream))
                {
                    privateKeyPem = await reader.ReadToEndAsync();
                }

                // Bước 1: Ký số hóa đơn
                var signResult = await _digitalSignatureService.SignInvoiceWithPrivateKeyContentAsync(
                    invoiceId, privateKeyPem, accountantId);
                
                if (!signResult.Success)
                {
                    return BadRequest(new { success = false, message = signResult.Message });
                }

                // Bước 2: Tạo PDF có chữ ký số tích hợp Version 3.0
                var pdfBytes = await _pdfSignatureService.CreateCryptographicallySignedPdfAsync(
                    invoiceId, 
                    signResult.InvoiceData ?? string.Empty, 
                    signResult.SignatureBase64 ?? string.Empty,
                    studentName,
                    courseName, 
                    className
                );

                var fileName = $"HoaDon_{invoiceId}_{DateTime.Now:yyyyMMdd_HHmmss}_Signed_v3.0.pdf";

                // Email PDF cho học viên nếu có email
                bool emailSent = false;
                if (!string.IsNullOrWhiteSpace(studentEmail))
                {
                    var subject = $"Hóa đơn đã ký số - {courseName}";
                    var body = $"<p>Chào {studentName},</p><p>Hóa đơn của bạn cho khóa học <strong>{courseName}</strong> lớp <strong>{className}</strong> đã được ký số và đính kèm trong email này.</p><p>Trân trọng,<br/>Trung tâm Tin học LDA</p>";
                    try
                    {
                        emailSent = await _emailService.SendEmailWithAttachmentAsync(studentEmail, subject, body, pdfBytes, fileName);
                        _logger.LogInformation("Đã gửi email hóa đơn ký số tới {StudentEmail} - Invoice {InvoiceId}", studentEmail, invoiceId);
                    }
                    catch (Exception exEmail)
                    {
                        _logger.LogError(exEmail, "Lỗi gửi email hóa đơn ký số tới {StudentEmail}", studentEmail);
                    }
                }

                _logger.LogInformation("✅ Đã tạo PDF có chữ ký số tích hợp Version 3.0 cho hóa đơn {InvoiceId}", invoiceId);

                // Thêm header để UI biết kết quả gửi mail (nếu có)
                if (!string.IsNullOrWhiteSpace(studentEmail))
                {
                    Response.Headers["X-Email-Sent"] = emailSent ? "true" : "false";
                }

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi tạo PDF có chữ ký số tích hợp");
                return StatusCode(500, new { success = false, message = "Lỗi xử lý tạo PDF có chữ ký số" });
            }
        }

        /// <summary>
        /// [NEW] Học viên: Upload và xác thực PDF có chữ ký số tích hợp Version 3.0
        /// form field: pdf (file application/pdf)
        /// </summary>
        [HttpPost("verify-pdf-v3")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> VerifyPdfV3()
        {
            try
            {
                if (!Request.HasFormContentType)
                    return BadRequest(new { success = false, message = "Sai định dạng dữ liệu (cần multipart/form-data)" });

                var form = await Request.ReadFormAsync();
                var file = form.Files["pdf"];
                
                if (file == null || file.Length == 0)
                    return BadRequest(new { success = false, message = "Chưa chọn file PDF để xác thực" });

                byte[] pdfBytes;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    pdfBytes = ms.ToArray();
                }

                _logger.LogInformation("🔍 Bắt đầu xác thực PDF có chữ ký số tích hợp Version 3.0");

                // Xác thực PDF integrity với PdfSignatureService mới
                var result = await _pdfSignatureService.VerifyPdfIntegrityAsync(pdfBytes);
                
                var responseData = new
                {
                    success = result.Success,
                    message = result.Message,
                    data = result.Success ? new 
                    {
                        isValid = result.IsValidSignature,
                        signedBy = result.SignedBy,
                        signedDate = result.SignedDate,
                        invoiceData = result.InvoiceData,
                        verificationMethod = "PDF_EMBEDDED_SIGNATURE_V3",
                        securityLevel = result.IsValidSignature.ToString()
                    } : null
                };

                _logger.LogInformation("✅ Hoàn thành xác thực PDF Version 3.0 - Kết quả: {IsValid}", 
                    result.IsValidSignature.ToString());

                return Ok(responseData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi xác thực PDF Version 3.0");
                return StatusCode(500, new { 
                    success = false, 
                    message = "Lỗi xử lý xác thực PDF có chữ ký số tích hợp" 
                });
            }
        }

        /// <summary>
        /// [NEW] Ký số và tạo PDF chuẩn PAdES (iText7) với chữ ký CMS/CAdES detached.
        /// form fields: invoiceId (int), accountantId (int), privateKey (file .pem), signerCert (file .pem), chainCerts (multiple .pem optional)
        /// </summary>
        [HttpPost("sign-and-create-pdf-pades")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> SignAndCreatePdfPades()
        {
            try
            {
                if (!Request.HasFormContentType)
                    return BadRequest(new { success = false, message = "Sai định dạng dữ liệu (cần multipart/form-data)" });

                var form = await Request.ReadFormAsync();
                if (!int.TryParse(form["invoiceId"], out var invoiceId) || !int.TryParse(form["accountantId"], out var accountantId))
                    return BadRequest(new { success = false, message = "Thiếu hoặc sai invoiceId/accountantId" });

                var pkFile = form.Files["privateKey"];
                if (pkFile == null || pkFile.Length == 0)
                    return BadRequest(new { success = false, message = "Chưa chọn file private key" });

                string privateKeyPem;
                using (var s = pkFile.OpenReadStream())
                using (var r = new StreamReader(s))
                    privateKeyPem = await r.ReadToEndAsync();

                // Optional signer certificate
                string? signerCertPem = null;
                var signerCertFile = form.Files["signerCert"];
                if (signerCertFile != null && signerCertFile.Length > 0)
                {
                    using var cs = signerCertFile.OpenReadStream();
                    using var cr = new StreamReader(cs);
                    signerCertPem = await cr.ReadToEndAsync();
                }

                // Optional chain certificates (multiple inputs named chainCerts)
                var chainPemList = new List<string>();
                foreach (var f in form.Files.Where(f => f.Name == "chainCerts"))
                {
                    if (f.Length == 0) continue;
                    using var fs = f.OpenReadStream();
                    using var fr = new StreamReader(fs);
                    chainPemList.Add(await fr.ReadToEndAsync());
                }

                // Re-sign invoice raw data into DB for traceability (optional)
                var signResult = await _digitalSignatureService.SignInvoiceWithPrivateKeyContentAsync(invoiceId, privateKeyPem, accountantId);
                if (!signResult.Success)
                    return BadRequest(new { success = false, message = signResult.Message });

                var invoiceDataJson = signResult.InvoiceData ?? "{}";

                // Create PAdES signed PDF
                var pdfBytes = await _pdfSignatureService.SignPdfAsync(invoiceId, invoiceDataJson, privateKeyPem, signerCertPem, chainPemList.Any() ? chainPemList : null);
                var fileName = $"HoaDon_{invoiceId}_{DateTime.Now:yyyyMMdd_HHmmss}_PAdES.pdf";
                _logger.LogInformation("[PAdES] Generated signed PDF for invoice {InvoiceId}", invoiceId);
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PAdES] Error creating signed PDF");
                return StatusCode(500, new { success = false, message = "Lỗi xử lý ký PAdES" });
            }
        }

        /// <summary>
        /// [NEW] Xác thực chữ ký PAdES trong PDF (Adobe-compatible). form field: pdf
        /// </summary>
        [HttpPost("verify-pdf-pades")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> VerifyPdfPades()
        {
            try
            {
                if (!Request.HasFormContentType)
                    return BadRequest(new { success = false, message = "Sai định dạng dữ liệu (cần multipart/form-data)" });
                var form = await Request.ReadFormAsync();
                var file = form.Files["pdf"];
                if (file == null || file.Length == 0)
                    return BadRequest(new { success = false, message = "Chưa chọn file PDF" });
                byte[] pdfBytes;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    pdfBytes = ms.ToArray();
                }
                var result = await _pdfSignatureService.VerifyPadesSignatureAsync(pdfBytes);
                return StatusCode(result.Success ? 200 : 400, new { success = result.Success, message = result.Message, data = result.Success ? new { isValid = result.IsValidSignature, signedBy = result.SignedBy, signedDate = result.SignedDate } : null });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PAdES] Verify error");
                return StatusCode(500, new { success = false, message = "Lỗi xác thực PAdES" });
            }
        }

        /// <summary>
        /// [NEW] Admin: Lấy thông tin public key fingerprint của trung tâm
        /// </summary>
        [HttpGet("public-key-info")]
        public async Task<IActionResult> GetPublicKeyInfo()
        {
            try
            {
                var fingerprint = await _pdfSignatureService.GetPublicKeyFingerprintAsync();
                
                return Ok(new {
                    success = true,
                    data = new {
                        publicKeyFingerprint = fingerprint,
                        signatureVersion = "3.0",
                        algorithm = "RSA-SHA256-PDF-EMBEDDED",
                        issuer = "Trung tâm Tin học LDA",
                        createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lấy thông tin public key");
                return StatusCode(500, new { 
                    success = false, 
                    message = "Lỗi lấy thông tin public key" 
                });
            }
        }

        public class SignInvoiceRequest
        {
            public int InvoiceId { get; set; }
            public string PrivateKeyPath { get; set; } = "";
            public int AccountantId { get; set; }
        }

        public class GenerateKeyPairSaveRequest
        {
            public int AccountantId { get; set; }
        }

        public class GenerateCenterKeyPairRequest
        {
            // Bỏ AccountantId - không cần chọn kế toán nữa
            public string CenterName { get; set; } = string.Empty;
            public string? Address { get; set; }
            public string? Phone { get; set; }
        }
    }
}