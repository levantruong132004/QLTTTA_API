using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models.DTOs;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchedulesController : ControllerBase
    {
        private readonly IScheduleService _service;
        private readonly ILogger<SchedulesController> _logger;
        public SchedulesController(IScheduleService service, ILogger<SchedulesController> logger)
        {
            _service = service; _logger = logger;
        }

        [HttpGet("by-class/{classId}")]
        public async Task<IActionResult> GetByClass(int classId)
        {
            var list = await _service.GetByClassAsync(classId);
            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ScheduleCreateDto dto)
        {
            try
            {
                var result = await _service.CreateAsync(dto);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền thực hiện thao tác này" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ScheduleUpdateDto dto)
        {
            if (id != dto.ScheduleId) return BadRequest(new { Success = false, Message = "ID không khớp" });
            try
            {
                var result = await _service.UpdateAsync(dto);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền thực hiện thao tác này" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _service.DeleteAsync(id);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền thực hiện thao tác này" });
            }
        }
    }
}
