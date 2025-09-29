using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace QLTTTA_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CoursesController : Controller
    {
        private readonly IConfiguration _configuration;

        public CoursesController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult GetCourses()
        {
            var courses = new List<object>();
            string connectionString = _configuration.GetConnectionString("OracleDbConnection");

            using (OracleConnection con = new OracleConnection(connectionString))
            {
                try
                {
                    con.Open();
                    // Chú ý: Luôn dùng schema name (QLTT_ADMIN) nếu user kết nối không phải là chủ sở hữu table
                    using (OracleCommand cmd = new OracleCommand("SELECT COURSE_ID, COURSE_NAME, DESCRIPTION FROM QLTT_ADMIN.COURSES", con))
                    {
                        using (OracleDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                courses.Add(new
                                {
                                    CourseId = reader["COURSE_ID"],
                                    CourseName = reader["COURSE_NAME"],
                                    Description = reader["DESCRIPTION"]
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log lỗi ở đây
                    return StatusCode(500, "Internal server error: " + ex.Message);
                }
            }

            return Ok(courses);
        }
    }
}
