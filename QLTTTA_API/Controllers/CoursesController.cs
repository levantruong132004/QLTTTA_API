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
        public async Task<IActionResult> GetCourses()
        {
            try
            {
                // Dùng service để hưởng per-user connection
                var list = await _courseService.GetAllCoursesAsync();
                var shaped = list.Select(c => new
                {
                    CourseId = c.CourseId,
                    CourseName = c.CourseName,
                    Description = c.Description
                });
                return Ok(shaped);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading courses");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }
    }
}
