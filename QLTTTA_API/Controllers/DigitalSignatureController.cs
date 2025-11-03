using Microsoft.AspNetCore.Mvc;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DigitalSignatureController : ControllerBase
    {
        private readonly IDigitalSignatureService _digitalSignatureService;
        private readonly ILogger<DigitalSignatureController> _logger;

        public DigitalSignatureController(IDigitalSignatureService digitalSignatureService, ILogger<DigitalSignatureController> logger)
        {
            _digitalSignatureService = digitalSignatureService;
            _logger = logger;
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
        /// Admin: Tạo cặp khóa trung tâm và tự động lưu public key vào bảng TTTA; gửi private key qua email kế toán.
        /// </summary>
        [HttpPost("generate-center-keypair/save")]
        public async Task<IActionResult> GenerateCenterKeyPairAndSave([FromBody] GenerateCenterKeyPairRequest request)
        {
            if (request == null || request.AccountantId <= 0 || string.IsNullOrWhiteSpace(request.CenterName))
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
            }
            var result = await _digitalSignatureService.GenerateCenterKeyPairAndSaveAsync(request.AccountantId, request.CenterName, request.Address ?? string.Empty, request.Phone ?? string.Empty);
            return StatusCode(result.Success ? 200 : 400, new { success = result.Success, message = result.Message, data = result.Success ? new { publicKey = result.PublicKeyPem } : null });
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
                return StatusCode(result.Success ? 200 : 400, new { success = result.Success, message = result.Message, data = result.Success ? new { signature = result.SignatureBase64, invoiceData = result.InvoiceData } : null });
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
            public int AccountantId { get; set; }
            public string CenterName { get; set; } = string.Empty;
            public string? Address { get; set; }
            public string? Phone { get; set; }
        }
    }
}