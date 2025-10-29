using Microsoft.AspNetCore.Mvc;
using QLTTTA_API.Models;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly ILogger<InvoicesController> _logger;
        public InvoicesController(IInvoiceService invoiceService, ILogger<InvoicesController> logger)
        {
            _invoiceService = invoiceService; _logger = logger;
        }

        public class CreateInvoiceRequest
        {
            public int RegistrationId { get; set; }
            public DateTime DueDate { get; set; }
            public int Amount { get; set; }
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

        // Lấy hóa đơn theo đơn đăng ký
        [HttpGet("by-registration/{registrationId:int}")]
        public async Task<IActionResult> GetByRegistration([FromRoute] int registrationId)
        {
            var inv = await _invoiceService.GetByRegistrationAsync(registrationId);
            if (inv == null) return NotFound(new { success = false, message = "Không tìm thấy hóa đơn" });
            return Ok(new { success = true, data = inv });
        }
    }
}
