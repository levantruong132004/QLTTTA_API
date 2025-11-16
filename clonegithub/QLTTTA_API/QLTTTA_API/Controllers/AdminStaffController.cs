using Microsoft.AspNetCore.Mvc;
using QLTTTA_API.Models.DTOs;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/admin/staff")]
    public class AdminStaffController : ControllerBase
    {
        private readonly IAdminStaffService _service;
        private readonly ILogger<AdminStaffController> _logger;

        public AdminStaffController(IAdminStaffService service, ILogger<AdminStaffController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var list = await _service.GetStaffAsync();
            return Ok(new { Success = true, Data = list });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] StaffCreateDto dto)
        {
            var res = await _service.CreateStaffAsync(dto);
            return Ok(res);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] StaffUpdateDto dto)
        {
            var res = await _service.UpdateStaffAsync(id, dto);
            return Ok(res);
        }

        [HttpPost("{id}/lock")]
        public async Task<IActionResult> Lock([FromRoute] int id)
        {
            var res = await _service.LockStaffAsync(id);
            return Ok(res);
        }

        [HttpPost("{id}/unlock")]
        public async Task<IActionResult> Unlock([FromRoute] int id)
        {
            var res = await _service.UnlockStaffAsync(id);
            return Ok(res);
        }
    }
}
