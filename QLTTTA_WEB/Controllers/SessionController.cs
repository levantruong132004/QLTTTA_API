using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace QLTTTA_WEB.Controllers
{
    [Route("Session")]
    public class SessionController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SessionController> _logger;

        public SessionController(IHttpClientFactory httpClientFactory, ILogger<SessionController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet("Check")]
        public async Task<IActionResult> CheckAsync()
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new { status = "invalid" });
            }

            if (!Request.Cookies.TryGetValue("SessionId", out var sessionId) || string.IsNullOrWhiteSpace(sessionId))
            {
                return Unauthorized(new { status = "invalid" });
            }

            try
            {
                var client = _httpClientFactory.CreateClient("ApiClient");
                var url = $"api/auth/check-session?username={Uri.EscapeDataString(username)}&sessionId={Uri.EscapeDataString(sessionId)}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Remove("X-Device-Type");
                request.Headers.Add("X-Device-Type", "pc");
                var response = await client.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return Unauthorized(new { status = "invalid" });
                }

                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new { status = "error", message = body });
                }

                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("status", out var statusEl) && statusEl.GetString() == "valid")
                    {
                        return Ok(new { status = "valid" });
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning(jsonEx, "Unable to parse session check response");
                }

                return Ok(new { status = "invalid" });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Session check failed");
                return StatusCode(503, new { status = "error" });
            }
        }
    }
}
