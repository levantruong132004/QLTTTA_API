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
    }
}