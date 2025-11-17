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

        /// <summary>
        /// Lấy danh sách lớp học - Công khai cho tất cả người dùng (học viên, nhân viên, kế toán)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int? courseId, [FromQuery] string? search)
        {
            try
            {
                var list = await _service.GetClassesAsync(courseId, search);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting classes");
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi lấy danh sách lớp học" });
            }
        }

        /// <summary>
        /// Lấy chi tiết lớp học theo ID - Công khai cho tất cả người dùng
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var data = await _service.GetClassByIdAsync(id);
                if (data == null) return NotFound(new { Success = false, Message = "Không tìm thấy lớp" });
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting class by id {Id}", id);
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi lấy thông tin lớp học" });
            }
        }

        /// <summary>
        /// Lấy danh sách học viên trong lớp (roster) - Công khai cho tất cả người dùng
        /// </summary>
        [HttpGet("{id}/roster")]
        public async Task<IActionResult> GetRoster(int id)
        {
            try
            {
                var list = await _service.GetRosterAsync(id);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting roster for class {Id}", id);
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi lấy danh sách học viên" });
            }
        }

        // =============== STAFF-ONLY OPERATIONS (enforced by DB privileges) ===============

        /// <summary>
        /// Tạo lớp học mới - Chỉ dành cho nhân viên
        /// </summary>
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
                _logger.LogWarning(oex, "Insufficient privileges creating class");
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền tạo lớp học" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating class");
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi tạo lớp học" });
            }
        }

        /// <summary>
        /// Cập nhật lớp học - Chỉ dành cho nhân viên
        /// </summary>
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
                _logger.LogWarning(oex, "Insufficient privileges updating class {ClassId}", id);
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền sửa lớp học" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating class {ClassId}", id);
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi cập nhật lớp học" });
            }
        }

        /// <summary>
        /// Xóa lớp học - Chỉ dành cho nhân viên
        /// </summary>
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
                _logger.LogWarning(oex, "Insufficient privileges deleting class {ClassId}", id);
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền xóa lớp học" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting class {ClassId}", id);
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi xóa lớp học" });
            }
        }
    }
}
