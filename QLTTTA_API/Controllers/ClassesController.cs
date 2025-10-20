using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models.DTOs;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClassesController : ControllerBase
    {
        private readonly IClassService _service;
        private readonly ILogger<ClassesController> _logger;

        public ClassesController(IClassService service, ILogger<ClassesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int? courseId, [FromQuery] string? search)
        {
            var list = await _service.GetClassesAsync(courseId, search);
            return Ok(list);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _service.GetClassByIdAsync(id);
            if (data == null) return NotFound(new { Success = false, Message = "Không tìm thấy lớp" });
            return Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ClassCreateDto dto)
        {
            try
            {
                var result = await _service.CreateClassAsync(dto);
                if (result.Success) return Ok(result);
                return BadRequest(result);
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền tạo lớp học" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ClassUpdateDto dto)
        {
            if (id != dto.ClassId) return BadRequest(new { Success = false, Message = "ID không khớp" });
            try
            {
                var result = await _service.UpdateClassAsync(dto);
                if (result.Success) return Ok(result);
                return BadRequest(result);
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền sửa lớp học" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _service.DeleteClassAsync(id);
                if (result.Success) return Ok(result);
                return BadRequest(result);
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền xóa lớp học" });
            }
        }
    }
}
