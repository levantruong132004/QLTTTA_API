using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using QRCoder;

namespace QLTTTA_WEB.Controllers
{
    public partial class PublicController : Controller
    {
        private readonly HttpClient _http;
        private readonly ILogger<PublicController> _logger;
        public PublicController(IHttpClientFactory factory, ILogger<PublicController> logger)
        { _http = factory.CreateClient("ApiClient"); _logger = logger; }

        [HttpGet]
    public async Task<IActionResult> QrSearch(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                TempData["ErrorMessage"] = "Không có dữ liệu QR hoặc mã để tra cứu.";
                return View("~/Views/Public/QrSearch.cshtml", new List<QLTTTA_WEB.Models.AdminRegistrationItem>());
            }
            try
            {
                // Nếu q là URL chứa tham số q bên trong thì trích ra
                if (Uri.TryCreate(q.Trim(), UriKind.Absolute, out var maybeUrl))
                {
                    var inner = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(maybeUrl.Query).TryGetValue("q", out var vals)
                        ? vals.ToString() : null;
                    if (!string.IsNullOrWhiteSpace(inner)) q = inner;
                }

                // Nếu nhập giống mã lớp (CLASS:... hoặc LH...), ưu tiên liệt kê lớp (chờ duyệt, nếu trống thì tất cả)
                var upper = q.ToUpperInvariant();
                bool looksLikeClass = upper.StartsWith("CLASS:") || upper.StartsWith("LOP:") || (!upper.StartsWith("REG:") && !upper.StartsWith("REGID:") && !q.Contains("{"));
                List<QLTTTA_WEB.Models.AdminRegistrationItem> data = new();
                if (looksLikeClass)
                {
                    var classCode = upper.StartsWith("CLASS:") ? q.Substring(6).Trim() : (upper.StartsWith("LOP:") ? q.Substring(4).Trim() : q.Trim());
                    if (!string.IsNullOrWhiteSpace(classCode))
                    {
                        var clsRes = await _http.GetAsync($"api/classes?search={Uri.EscapeDataString(classCode)}");
                        var clsBody = await clsRes.Content.ReadAsStringAsync();
                        if (clsRes.IsSuccessStatusCode)
                        {
                            var classes = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminClassItem>>(clsBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                            var match = classes.FirstOrDefault(c => string.Equals(c.ClassCode, classCode, StringComparison.OrdinalIgnoreCase));
                            if (match != null)
                            {
                                // pending trước
                                var regRes = await _http.GetAsync($"api/registrations?status={Uri.EscapeDataString("Chờ duyệt")}&classCode={Uri.EscapeDataString(match.ClassCode)}");
                                var regBody = await regRes.Content.ReadAsStringAsync();
                                if (regRes.IsSuccessStatusCode)
                                {
                                    data = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminRegistrationItem>>(regBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                                }
                                // fallback tất cả
                                if (data.Count == 0)
                                {
                                    var regResAll = await _http.GetAsync($"api/registrations?classCode={Uri.EscapeDataString(match.ClassCode)}");
                                    var regBodyAll = await regResAll.Content.ReadAsStringAsync();
                                    if (regResAll.IsSuccessStatusCode)
                                    {
                                        data = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminRegistrationItem>>(regBodyAll, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                                    }
                                }
                            }
                        }
                    }
                }

                // Nếu không phải mã lớp, dùng search theo QR/Code/ID
                if (data.Count == 0)
                {
                    var url = $"api/registrations/search?q={Uri.EscapeDataString(q)}";
                    var res = await _http.GetAsync(url);
                    var body = await res.Content.ReadAsStringAsync();
                    if (!res.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Public QR search failed: {Status} {Body}", res.StatusCode, body);
                        TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(body) ? "Tra cứu thất bại" : body;
                        return View("~/Views/Public/QrSearch.cshtml", new List<QLTTTA_WEB.Models.AdminRegistrationItem>());
                    }
                    data = JsonSerializer.Deserialize<List<QLTTTA_WEB.Models.AdminRegistrationItem>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                ViewBag.Query = q;
                return View("~/Views/Public/QrSearch.cshtml", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Public QR search error");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tra cứu. Vui lòng thử lại.";
                return View("~/Views/Public/QrSearch.cshtml", new List<QLTTTA_WEB.Models.AdminRegistrationItem>());
            }
        }
    }
}

namespace QLTTTA_WEB.Controllers
{
    public partial class PublicController
    {
        // Public QR image endpoint for generating QR images without staff login
        [HttpGet]
        public IActionResult QrImagePublic(string payload, int size = 400)
        {
            if (string.IsNullOrWhiteSpace(payload)) return BadRequest("Missing payload");
            try
            {
                if (size < 100) size = 100; if (size > 1024) size = 1024;
                using var qrGen = new QRCodeGenerator();
                using var data = qrGen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
                var png = new PngByteQRCode(data);
                int ppm = Math.Max(1, size / 21);
                var bytes = png.GetGraphic(ppm);
                return File(bytes, "image/png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QrImagePublic render failed");
                return BadRequest("Không thể tạo ảnh QR");
            }
        }
    }
}
