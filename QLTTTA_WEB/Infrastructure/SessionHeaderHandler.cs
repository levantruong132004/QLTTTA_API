using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace QLTTTA_WEB.Infrastructure
{
    /// <summary>
    /// Delegating handler gắn X-Session-Id và X-Device-Type từ cookie/session vào mọi request tới API
    /// </summary>
    public class SessionHeaderHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SessionHeaderHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                var context = _httpContextAccessor.HttpContext;
                if (context != null)
                {
                    // Thêm X-Session-Id từ cookie
                    var cookies = context.Request?.Cookies;
                    if (cookies != null && cookies.TryGetValue("SessionId", out var sessionId) && !string.IsNullOrWhiteSpace(sessionId))
                    {
                        request.Headers.Remove("X-Session-Id");
                        request.Headers.Add("X-Session-Id", sessionId);
                    }

                    // Thêm X-Device-Type (mặc định là "pc" cho web)
                    request.Headers.Remove("X-Device-Type");
                    request.Headers.Add("X-Device-Type", "pc");
                }
            }
            catch
            {
                // ignore - không chặn request nếu không lấy được headers
            }
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
