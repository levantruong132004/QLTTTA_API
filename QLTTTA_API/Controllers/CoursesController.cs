using Microsoft.AspNetCore.Mvc;
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

        [HttpGet]
        public IActionResult GetCourses()
        {
            try
            {
                // Dùng service để hưởng per-user connection
                var list = _courseService.GetAllCoursesAsync().GetAwaiter().GetResult();
                _logger.LogInformation("Loaded {Count} courses from DB", list.Count);
                // Trả về đầy đủ thuộc tính để UI quản trị sử dụng
                return Ok(list.Select(c => new
                {
                    c.CourseId,
                    c.CourseCode,
                    c.CourseName,
                    c.Description,
                    c.StandardFee
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading courses");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost]
        public IActionResult CreateCourse([FromBody] QLTTTA_API.Models.DTOs.CourseCreateDto dto)
        {
            try
            {
                var result = _courseService.CreateCourseAsync(dto).GetAwaiter().GetResult();
                if (result.Success) return Ok(result.Message);
                return BadRequest(result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create course failed");
                return StatusCode(500, "Có lỗi xảy ra khi tạo khóa học");
            }
        }

        [HttpPut("{id}")]
        public IActionResult UpdateCourse(int id, [FromBody] QLTTTA_API.Models.DTOs.CourseUpdateDto dto)
        {
            try
            {
                if (dto.CourseId != id) return BadRequest("ID không khớp");
                var result = _courseService.UpdateCourseAsync(dto).GetAwaiter().GetResult();
                if (result.Success) return Ok(result.Message);
                return BadRequest(result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update course failed {Id}", id);
                return StatusCode(500, "Có lỗi xảy ra khi cập nhật khóa học");
            }
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteCourse(int id)
        {
            try
            {
                var result = _courseService.DeleteCourseAsync(id).GetAwaiter().GetResult();
                if (result.Success) return Ok(result.Message);
                return BadRequest(result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete course failed {Id}", id);
                return StatusCode(500, "Có lỗi xảy ra khi xóa khóa học");
            }
        }
    }
}
