using Microsoft.AspNetCore.Mvc;
using QLTTTA_API.Models.DTOs;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentsController> _logger;
        public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
        { _paymentService = paymentService; _logger = logger; }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PaymentCreateDto dto)
        {
            if (dto == null || dto.Amount <= 0 || dto.InvoiceId <= 0 || dto.AccountantId <= 0 || string.IsNullOrWhiteSpace(dto.PaymentMethod))
                return BadRequest(new { success = false, message = "Dữ liệu thanh toán không hợp lệ" });
            var res = await _paymentService.CreateAsync(dto);
            return StatusCode(res.Success ? 200 : 400, new { success = res.Success, message = res.Message, data = res.Data });
        }

        [HttpGet("by-invoice/{invoiceId:int}")]
        public async Task<IActionResult> GetByInvoice([FromRoute] int invoiceId)
        {
            if (invoiceId <= 0) return BadRequest(new { success = false, message = "InvoiceId không hợp lệ" });
            var list = await _paymentService.GetByInvoiceAsync(invoiceId);
            return Ok(new { success = true, data = list });
        }
    }
}
