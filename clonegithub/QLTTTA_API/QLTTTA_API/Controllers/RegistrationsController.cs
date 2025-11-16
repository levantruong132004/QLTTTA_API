using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegistrationsController : ControllerBase
    {
        private readonly IRegistrationService _service;
        private readonly ILogger<RegistrationsController> _logger;
        public RegistrationsController(IRegistrationService service, ILogger<RegistrationsController> logger)
        { _service = service; _logger = logger; }

        // Danh sách đơn cho NV học vụ (lọc trạng thái/lớp theo id hoặc mã)
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? status, [FromQuery] int? classId, [FromQuery] string? classCode)
        {
            var list = await _service.GetRegistrationsAsync(status, classId, classCode);
            return Ok(list);
        }

        // Học viên xem đơn của mình dưới quyền HV hiện tại
        [HttpGet("my")]
        public async Task<IActionResult> GetMy()
        {
            var list = await _service.GetMyRegistrationsAsync();
            return Ok(list);
        }

        // Tìm kiếm đơn đăng ký theo QR/code/id (cho tính năng quét QR)
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] bool? mine)
        {
            if (string.IsNullOrWhiteSpace(q)) return Ok(Array.Empty<object>());
            var list = await _service.SearchAsync(q);
            // Nếu mine=true: lọc theo học viên hiện tại dựa trên X-Session-Id và cột per-device
            if (mine == true)
            {
                try
                {
                    using var conn = await (_service as BaseService)!.GetAdminConnectionAsync();
                    var sid = HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
                    var deviceType = HttpContext?.Request?.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
                    if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
                    var columnName = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                    using var cmd = new OracleCommand($"SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE {columnName} = :sid", conn) { BindByName = true };
                    cmd.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sid ?? string.Empty;
                    var obj = await cmd.ExecuteScalarAsync();
                    if (obj != null && obj != DBNull.Value)
                    {
                        var hvId = Convert.ToInt32(obj);
                        list = list.Where(x => x.StudentId == hvId).ToList();
                    }
                    else
                    {
                        list = new List<QLTTTA_API.Models.Registration>();
                    }
                }
                catch
                {
                    list = new List<QLTTTA_API.Models.Registration>();
                }
            }
            return Ok(list);
        }

        // Danh sách dành cho Kế toán (lọc theo khóa/lớp)
        [HttpGet("accountant")]
        public async Task<IActionResult> GetForAccountant([FromQuery] int? courseId, [FromQuery] int? classId)
        {
            var list = await _service.GetAccountantRegistrationsAsync(courseId, classId);
            return Ok(list);
        }

        // Chi tiết đơn dành cho Kế toán (phục vụ trang hóa đơn)
        [HttpGet("accountant/{id:int}")]
        public async Task<IActionResult> GetForAccountantById(int id)
        {
            var item = await _service.GetAccountantRegistrationByIdAsync(id);
            if (item == null) return NotFound(new { Success = false, Message = "Không tìm thấy đơn" });
            return Ok(item);
        }

        // Duyệt đơn: có thể truyền classId mới để gán lớp
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
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền duyệt đơn" });
            }
        }

        // Từ chối đơn
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
                return StatusCode(403, new { Success = false, Message = "Bạn không có quyền từ chối đơn" });
            }
        }
    }
}
