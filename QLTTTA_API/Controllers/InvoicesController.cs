using Microsoft.AspNetCore.Mvc;
using QLTTTA_API.Models;
using QLTTTA_API.Services;
using Oracle.ManagedDataAccess.Client; // Added for OracleException handling
using QLTTTA_API.Models.DTOs; // for StudentPaymentDto

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly IDigitalSignatureService _digitalSignatureService;
        private readonly ILogger<InvoicesController> _logger;

        public InvoicesController(IInvoiceService invoiceService, IDigitalSignatureService digitalSignatureService, ILogger<InvoicesController> logger)
        {
            _invoiceService = invoiceService;
            _digitalSignatureService = digitalSignatureService;
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

        // Kế toán tạo hóa đơn cho đơn đã duyệt
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest req)
        {
            if (req == null || req.RegistrationId <= 0 || req.Amount <= 0)
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
            var res = await _invoiceService.CreateAsync(req.RegistrationId, req.DueDate, req.Amount);
            return StatusCode(res.Success ? 200 : 400, new { success = res.Success, message = res.Message, data = res.Data });
        }

        // Kế toán tạo và ký số hóa đơn cùng lúc
        [HttpPost("create-and-sign")]
        public async Task<IActionResult> CreateAndSign([FromBody] CreateAndSignInvoiceRequest req)
        {
            if (req == null || req.RegistrationId <= 0 || req.Amount <= 0 || string.IsNullOrEmpty(req.PrivateKeyPath))
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });

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

        // Lấy hóa đơn theo đơn đăng ký
        [HttpGet("by-registration/{registrationId:int}")]
        public async Task<IActionResult> GetByRegistration([FromRoute] int registrationId)
        {
            try
            {
                var inv = await _invoiceService.GetByRegistrationAsync(registrationId);
                if (inv == null) return NotFound(new { success = false, message = "Không tìm thấy hóa đơn" });
                
                // Kiểm tra và xác thực chữ ký nếu có (giống như API with-signature)
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
                // Phản hồi chi tiết lỗi Oracle để UI hiển thị và hướng dẫn cập nhật DB
                _logger.LogError(oex, "Oracle error when GetByRegistration for {RegistrationId}", registrationId);
                return StatusCode(500, new { success = false, message = $"Lỗi Oracle ({oex.Number}): {oex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error GetByRegistration for {RegistrationId}", registrationId);
                return StatusCode(500, new { success = false, message = "Lỗi máy chủ khi lấy hóa đơn theo đơn đăng ký" });
            }
        }

        // Lấy hóa đơn có thông tin chữ ký số
        [HttpGet("with-signature/{invoiceId:int}")]
        public async Task<IActionResult> GetInvoiceWithSignature([FromRoute] int invoiceId)
        {
            try
            {
                var inv = await _invoiceService.GetByIdAsync(invoiceId); // Sử dụng GetByIdAsync
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
                            // Có chữ ký lưu trong DB nhưng xác thực thất bại -> vẫn trả về chữ ký với isValid=false để UI hiển thị "Đã ký số (Không hợp lệ)"
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

        // Học viên bắt đầu thanh toán: chuyển trạng thái hóa đơn để đánh dấu đang xử lý
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

        public class PrintAndEmailInvoiceRequest
        {
            public int InvoiceId { get; set; }
            public int AccountantId { get; set; }
        }

        // In hóa đơn và gửi PDF qua email cho học viên
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
                        message = "Đã tạo PDF hóa đơn và gửi email thành công",
                        data = new 
                        {
                            invoiceCode = invoice.InvoiceCode,
                            emailSent = result.Data?.EmailAddress,
                            pdfGenerated = true
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
    }
}
