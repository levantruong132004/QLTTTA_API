using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : ControllerBase
    {
        private readonly IStudentService _studentService;
        private readonly ILogger<StudentsController> _logger;
        private readonly IConfiguration _configuration;

        public StudentsController(IStudentService studentService, ILogger<StudentsController> logger, IConfiguration configuration)
        {
            _studentService = studentService;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet("count")]
        public async Task<ActionResult> GetStudentsCount()
        {
            try
            {
                var baseService = new BaseService(_configuration, _logger);
                using var connection = await baseService.GetConnectionAsync();
                using var command = new OracleCommand("SELECT COUNT(*) FROM QLTT_ADMIN.HOC_VIEN", connection);
                var count = Convert.ToInt32(await command.ExecuteScalarAsync());

                return Ok(new { Success = true, Count = count, Message = "HOC_VIEN table accessible" });
            }
            catch (Exception ex)
            {
                return Ok(new { Success = false, Error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<Student>>> GetStudents(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            try
            {
                var result = await _studentService.GetStudentsAsync(pageNumber, pageSize, search);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting students");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi lấy danh sách học viên",
                    Data = ex.Message
                });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<Student>>> GetStudent(int id)
        {
            try
            {
                var student = await _studentService.GetStudentByIdAsync(id);
                if (student == null)
                {
                    return NotFound(new ApiResponse<Student>
                    {
                        Success = false,
                        Message = "Không tìm thấy học viên"
                    });
                }

                return Ok(new ApiResponse<Student>
                {
                    Success = true,
                    Data = student
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting student {StudentId}", id);
                return StatusCode(500, new ApiResponse<Student>
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi lấy thông tin học viên"
                });
            }
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<Student>>> CreateStudent([FromBody] StudentCreateDto dto)
        {
            try
            {
                // Log received data for debugging
                _logger.LogInformation("Received student data: {@StudentDto}", dto);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value != null)
                        .SelectMany(x => x.Value!.Errors)
                        .Select(x => x.ErrorMessage)
                        .ToArray();

                    _logger.LogWarning("ModelState validation failed: {Errors}", string.Join(", ", errors));

                    return BadRequest(new ApiResponse<Student>
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ: " + string.Join(", ", errors),
                        Errors = ModelState
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateStudent validation");
                return StatusCode(500, new ApiResponse<Student>
                {
                    Success = false,
                    Message = "Lỗi server: " + ex.Message
                });
            }

            try
            {
                var result = await _studentService.CreateStudentAsync(dto);

                if (result.Success)
                {
                    return CreatedAtAction(nameof(GetStudent), new { id = result.Data?.StudentId }, result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating student");
                return StatusCode(500, new ApiResponse<Student>
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi tạo học viên"
                });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<Student>>> UpdateStudent(int id, [FromBody] StudentUpdateDto dto)
        {
            if (id != dto.StudentId)
            {
                return BadRequest(new ApiResponse<Student>
                {
                    Success = false,
                    Message = "ID không khớp"
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<Student>
                {
                    Success = false,
                    Message = "Dữ liệu không hợp lệ",
                    Errors = ModelState
                });
            }

            try
            {
                var result = await _studentService.UpdateStudentAsync(dto);

                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating student {StudentId}", id);
                return StatusCode(500, new ApiResponse<Student>
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi cập nhật học viên"
                });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteStudent(int id)
        {
            try
            {
                var result = await _studentService.DeleteStudentAsync(id);

                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting student {StudentId}", id);
                return StatusCode(500, new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi xóa học viên"
                });
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult<ApiResponse<List<Student>>>> SearchStudents([FromQuery] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return BadRequest(new ApiResponse<List<Student>>
                {
                    Success = false,
                    Message = "Từ khóa tìm kiếm là bắt buộc"
                });
            }

            try
            {
                var students = await _studentService.SearchStudentsAsync(keyword);
                return Ok(new ApiResponse<List<Student>>
                {
                    Success = true,
                    Data = students,
                    Message = $"Tìm thấy {students.Count} học viên"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching students with keyword {Keyword}", keyword);
                return StatusCode(500, new ApiResponse<List<Student>>
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi tìm kiếm học viên"
                });
            }
        }
    }
}