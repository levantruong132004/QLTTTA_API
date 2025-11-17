using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Services;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegistrationsController : ControllerBase
    {
        private readonly IRegistrationService _service;
        private readonly ILogger<RegistrationsController> _logger;
        
        public RegistrationsController(IRegistrationService service, ILogger<RegistrationsController> logger)
        { 
            _service = service; 
            _logger = logger; 
        }

        // =============== STAFF OPERATIONS - Nhân viên quản lý đơn đăng ký ===============
        
        /// <summary>
        /// Danh sách đơn đăng ký cho Nhân viên học vụ (lọc theo trạng thái/lớp)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? status, [FromQuery] int? classId)
        {
            try
            {
                var list = await _service.GetRegistrationsAsync(status, classId);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting registrations");
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi lấy danh sách đơn đăng ký" });
            }
        }

        /// <summary>
        /// Duyệt đơn đăng ký - Chỉ dành cho Nhân viên học vụ
        /// Có thể truyền classId mới để gán lớp khác cho học viên
        /// </summary>
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
                _logger.LogWarning(oex, "Insufficient privileges approving registration {Id}", id);
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền duyệt đơn" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving registration {Id}", id);
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi duyệt đơn" });
            }
        }

        /// <summary>
        /// Từ chối đơn đăng ký - Chỉ dành cho Nhân viên học vụ
        /// </summary>
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
                _logger.LogWarning(oex, "Insufficient privileges rejecting registration {Id}", id);
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền từ chối đơn" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting registration {Id}", id);
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi từ chối đơn" });
            }
        }

        // =============== STUDENT OPERATIONS - Học viên xem đơn của mình ===============

        /// <summary>
        /// Học viên đăng ký lớp học mới
        /// </summary>
        [HttpPost("enroll")]
        public async Task<ActionResult<ApiResponse<bool>>> EnrollInClass([FromBody] EnrollClassRequest request)
        {
            try
            {
                var result = await _service.CreateRegistrationAsync(request.ClassId, request.Note);
                
                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<bool> 
                { 
                    Success = false, 
                    Message = ex.Message 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enrolling in class {ClassId}", request.ClassId);
                return StatusCode(500, new ApiResponse<bool> 
                { 
                    Success = false, 
                    Message = "Có lỗi xảy ra khi đăng ký lớp học" 
                });
            }
        }

        /// <summary>
        /// Học viên xem danh sách đơn đăng ký của chính mình
        /// Sử dụng session ID từ header để xác định học viên
        /// </summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMy()
        {
            try
            {
                var list = await _service.GetMyRegistrationsAsync();
                return Ok(list);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access to my registrations");
                return StatusCode(401, new { Success = false, Message = "Phiên đăng nhập không hợp lệ hoặc đã hết hạn" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting my registrations");
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi lấy danh sách đơn đăng ký" });
            }
        }

        // =============== ACCOUNTANT OPERATIONS - Kế toán xem đơn đã duyệt để tạo hóa đơn ===============

        /// <summary>
        /// Danh sách đơn đăng ký đã được phê duyệt - Dành cho Kế toán
        /// Kế toán sẽ dựa vào danh sách này để tạo hóa đơn và ký số
        /// </summary>
        [HttpGet("accountant")]
        public async Task<IActionResult> GetForAccountant([FromQuery] int? courseId, [FromQuery] int? classId)
        {
            try
            {
                var list = await _service.GetAccountantRegistrationsAsync(courseId, classId);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accountant registrations");
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi lấy danh sách đơn cho kế toán" });
            }
        }

        /// <summary>
        /// Chi tiết đơn đăng ký đã được phê duyệt - Dành cho Kế toán
        /// Phục vụ cho màn hình tạo hóa đơn và ký số
        /// </summary>
        [HttpGet("accountant/{id:int}")]
        public async Task<IActionResult> GetForAccountantById(int id)
        {
            try
            {
                var item = await _service.GetAccountantRegistrationByIdAsync(id);
                if (item == null) return NotFound(new { Success = false, Message = "Không tìm thấy đơn" });
                return Ok(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accountant registration by id {Id}", id);
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi lấy thông tin đơn" });
            }
        }
    }
}
