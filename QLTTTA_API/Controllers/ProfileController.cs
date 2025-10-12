using Microsoft.AspNetCore.Mvc;
using QLTTTA_API.Models;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/profile")]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(IProfileService profileService, ILogger<ProfileController> logger)
        {
            _profileService = profileService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<StudentProfileDto>> Get()
        {
            var data = await _profileService.GetMyProfileAsync();
            if (data == null) return NotFound();
            return Ok(data);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] StudentProfileUpdateDto dto)
        {
            var ok = await _profileService.UpdateMyProfileAsync(dto);
            if (!ok) return BadRequest(new { Success = false, Message = "Cập nhật không thành công" });
            return Ok(new { Success = true });
        }

        [HttpGet("courses")]
        public async Task<IActionResult> Courses()
        {
            var list = await _profileService.GetAllCoursesAsync();
            // Không trả ID ra nếu cần ẩn ID trên web có thể filter tại web
            var shaped = list.Select(c => new { c.CourseCode, c.CourseName, c.Description, c.StandardFee });
            return Ok(shaped);
        }
    }
}
