using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CoursesController : Controller
    {
        private readonly ICourseService _courseService;
        private readonly ILogger<CoursesController> _logger;

        public CoursesController(ICourseService courseService, ILogger<CoursesController> logger)
        {
            _courseService = courseService;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách tất cả khóa học - Công khai cho tất cả người dùng (học viên, nhân viên, kế toán)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCourses()
        {
            try
            {
                var list = await _courseService.GetAllCoursesAsync();
                // Trả đầy đủ field để phía quản trị sử dụng
                var shaped = list.Select(c => new
                {
                    c.CourseId,
                    c.CourseCode,
                    c.CourseName,
                    c.Description,
                    c.StandardFee
                });
                return Ok(shaped);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading courses");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        /// <summary>
        /// Lấy chi tiết khóa học theo ID - Công khai cho tất cả người dùng
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCourseById(int id)
        {
            try
            {
                var course = await _courseService.GetCourseByIdAsync(id);
                if (course == null) return NotFound(new { Success = false, Message = "Không tìm thấy khóa học" });
                return Ok(course);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting course by id {Id}", id);
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi lấy khóa học" });
            }
        }

        // =============== STAFF-ONLY OPERATIONS (enforced by DB privileges) ===============
        
        /// <summary>
        /// Tạo khóa học mới - Chỉ dành cho nhân viên (DB sẽ kiểm tra quyền)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateCourse([FromBody] Models.DTOs.CourseCreateDto dto)
        {
            try
            {
                var result = await _courseService.CreateCourseAsync(dto);
                if (result.Success) return Ok(result);
                return BadRequest(result);
            }
            catch (OracleException oex) when (oex.Number == 1031) // ORA-01031: insufficient privileges
            {
                _logger.LogWarning(oex, "Insufficient privileges creating course");
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền thực hiện thao tác này" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating course");
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi tạo khóa học" });
            }
        }

        /// <summary>
        /// Cập nhật khóa học - Chỉ dành cho nhân viên
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCourse(int id, [FromBody] Models.DTOs.CourseUpdateDto dto)
        {
            if (id != dto.CourseId)
                return BadRequest(new { Success = false, Message = "ID không khớp" });

            try
            {
                var result = await _courseService.UpdateCourseAsync(dto);
                if (result.Success) return Ok(result);
                return BadRequest(result);
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                _logger.LogWarning(oex, "Insufficient privileges updating course {CourseId}", id);
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền thực hiện thao tác này" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating course {CourseId}", id);
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi cập nhật khóa học" });
            }
        }

        /// <summary>
        /// Xóa khóa học - Chỉ dành cho nhân viên
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            try
            {
                var result = await _courseService.DeleteCourseAsync(id);
                if (result.Success) return Ok(result);
                return BadRequest(result);
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                _logger.LogWarning(oex, "Insufficient privileges deleting course {CourseId}", id);
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền thực hiện thao tác này" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting course {CourseId}", id);
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra khi xóa khóa học" });
            }
        }
    }
}
