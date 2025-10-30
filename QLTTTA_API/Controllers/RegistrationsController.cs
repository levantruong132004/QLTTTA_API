using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegistrationsController : ControllerBase
    {
        private readonly IRegistrationService _service;
        private readonly ILogger<RegistrationsController> _logger;
        public RegistrationsController(IRegistrationService service, ILogger<RegistrationsController> logger)
        { _service = service; _logger = logger; }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? status, [FromQuery] int? classId)
        {
            var list = await _service.GetRegistrationsAsync(status, classId);
            return Ok(list);
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMy()
        {
            var list = await _service.GetMyRegistrationsAsync();
            return Ok(list);
        }

        [HttpGet("accountant")]
        public async Task<IActionResult> GetForAccountant([FromQuery] int? courseId, [FromQuery] int? classId)
        {
            var list = await _service.GetAccountantRegistrationsAsync(courseId, classId);
            return Ok(list);
        }

        [HttpGet("accountant/{id:int}")]
        public async Task<IActionResult> GetForAccountantById(int id)
        {
            var item = await _service.GetAccountantRegistrationByIdAsync(id);
            if (item == null) return NotFound(new { Success = false, Message = "Không tìm thấy đơn" });
            return Ok(item);
        }

        [HttpPost("{id}/approve")]
        public async Task<IActionResult> Approve(int id, [FromQuery] int? classId)
        {
            try
            {
                var result = await _service.ApproveAsync(id, classId);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền duyệt đơn" });
            }
        }

        [HttpPost("{id}/reject")]
        public async Task<IActionResult> Reject(int id)
        {
            try
            {
                var result = await _service.RejectAsync(id);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền từ chối đơn" });
            }
        }
    }
}
