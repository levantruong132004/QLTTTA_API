using Microsoft.AspNetCore.Mvc;
using QLTTTA_API.Services;
using QLTTTA_API.Models;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StaffController : ControllerBase
    {
        private readonly IStaffService _staff;
        private readonly ILogger<StaffController> _logger;
        public StaffController(IStaffService staff, ILogger<StaffController> logger)
        {
            _staff = staff;
            _logger = logger;
        }

        // GET api/staff/students?search=&active=
        [HttpGet("students")]
        public async Task<IActionResult> SearchStudents([FromQuery] string? search, [FromQuery] int? active)
        {
            try
            {
                // Dịch vụ hiện tại chỉ hỗ trợ tìm theo từ khóa; bỏ qua "active"
                var list = await _staff.SearchStudentsAsync(search);
                return Ok(new { success = true, data = list });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchStudents error");
                return StatusCode(500, new { success = false, message = "Lỗi khi tìm kiếm học viên" });
            }
        }

        // POST api/staff/students/{id}/lock
        [HttpPost("students/{id:int}/lock")]
        public async Task<IActionResult> LockStudent([FromRoute] int id)
        {
            var resp = await _staff.LockStudentAsync(id);
            if (resp.Success) return Ok(new { success = true, message = resp.Message ?? "Đã khóa tài khoản" });
            return BadRequest(new { success = false, message = resp.Message ?? "Không khóa được tài khoản" });
        }

        // POST api/staff/students/{id:int}/unlock
        [HttpPost("students/{id:int}/unlock")]
        public async Task<IActionResult> UnlockStudent([FromRoute] int id)
        {
            var resp = await _staff.UnlockStudentAsync(id);
            if (resp.Success) return Ok(new { success = true, message = resp.Message ?? "Đã mở khóa tài khoản" });
            return BadRequest(new { success = false, message = resp.Message ?? "Không mở khóa được tài khoản" });
        }

        // GET api/staff/students/{id}/detail (header + regs + invoices)
        [HttpGet("students/{id:int}/detail")]
        public async Task<IActionResult> GetStudentDetail([FromRoute] int id)
        {
            try
            {
                var students = await _staff.SearchStudentsAsync(null);
                var stu = students.FirstOrDefault(s => s.StudentId == id);
                var regs = await _staff.GetStudentRegistrationsAsync(id);
                var invoices = await _staff.GetStudentInvoicesAsync(id);
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        student = stu,
                        registrations = regs,
                        invoices = invoices
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetStudentDetail error");
                return StatusCode(500, new { success = false, message = "Lỗi khi lấy chi tiết học viên" });
            }
        }

        // GET api/staff/students/{id}/registrations
        [HttpGet("students/{id:int}/registrations")]
        public async Task<IActionResult> GetRegistrations([FromRoute] int id)
        {
            try
            {
                var list = await _staff.GetStudentRegistrationsAsync(id);
                return Ok(new { success = true, data = list });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetStudentRegistrations error");
                return StatusCode(500, new { success = false, message = "Lỗi khi lấy đơn đăng ký" });
            }
        }

        // GET api/staff/students/{id}/invoices
        [HttpGet("students/{id:int}/invoices")]
        public async Task<IActionResult> GetInvoices([FromRoute] int id)
        {
            try
            {
                var list = await _staff.GetStudentInvoicesAsync(id);
                return Ok(new { success = true, data = list });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetStudentInvoices error");
                return StatusCode(500, new { success = false, message = "Lỗi khi lấy danh sách hóa đơn" });
            }
        }
    }
}
