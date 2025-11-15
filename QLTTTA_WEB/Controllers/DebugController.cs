using Microsoft.AspNetCore.Mvc;

namespace QLTTTA_WEB.Controllers
{
    public class DebugController : Controller
    {
        [HttpGet]
        public IActionResult Session()
        {
            var result = new
            {
                UserId = HttpContext.Session.GetString("UserId"),
                Username = HttpContext.Session.GetString("Username"),
                RoleId = HttpContext.Session.GetString("RoleId"),
                Role = HttpContext.Session.GetString("Role"),
                HasSessionIdCookie = Request.Cookies.ContainsKey("SessionId")
            };
            return Json(result);
        }
    }
}
