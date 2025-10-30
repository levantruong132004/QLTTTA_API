using Microsoft.AspNetCore.Mvc;
using QLTTTA_API.Models;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/profile")]
    public class ProfileController : ControllerBase
    {
        public class RegisterCourseRequest { public string? CourseCode { get; set; } }
        public class RegisterClassRequest { public int ClassId { get; set; } public int? StudentId { get; set; } }
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
            var shaped = list.Select(c => new { c.CourseCode, c.CourseName, c.Description, c.StandardFee });
            return Ok(shaped);
        }

        [HttpPost("register-course")]
        public async Task<IActionResult> RegisterCourse([FromBody] RegisterCourseRequest payload)
        {
            try
            {
                string courseCode = payload?.CourseCode ?? string.Empty;
                var (success, message) = await _profileService.SubmitCourseRegistrationRequestAsync(courseCode);
                if (success) return Ok(new { Success = true, Message = message });
                return BadRequest(new { Success = false, Message = message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("open-classes/{courseCode}")]
        public async Task<IActionResult> GetOpenClasses(string courseCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(courseCode)) return BadRequest(new { Success = false, Message = "Thiếu mã khóa" });
                var list = await (_profileService as IProfileService)!.GetOpenClassesByCourseAsync(courseCode);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetOpenClasses failed for {Course}", courseCode);
                return StatusCode(500, new { Success = false, Message = "Có lỗi xảy ra" });
            }
        }

        [HttpPost("register-class")]
        public async Task<IActionResult> RegisterClass([FromBody] RegisterClassRequest payload)
        {
            try
            {
                int classId = payload?.ClassId ?? 0;
                if (classId <= 0) return BadRequest(new { Success = false, Message = "Thiếu classId" });
                var (ok, msg) = await (_profileService as IProfileService)!.RegisterToClassAsync(classId, payload?.StudentId);
                return ok ? Ok(new { Success = true, Message = msg }) : BadRequest(new { Success = false, Message = msg });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RegisterClass failed");
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }
    }
}
