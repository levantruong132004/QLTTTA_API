using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace QLTTTA_WEB.Infrastructure
{
    // Delegating handler gắn X-Session-Id từ cookie vào mọi request tới API
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
                var cookies = _httpContextAccessor.HttpContext?.Request?.Cookies;
                if (cookies != null && cookies.TryGetValue("SessionId", out var sessionId) && !string.IsNullOrWhiteSpace(sessionId))
                {
                    request.Headers.Remove("X-Session-Id");
                    request.Headers.Add("X-Session-Id", sessionId);
                    // Gắn loại thiết bị cho Web: pc
                    request.Headers.Remove("X-Device-Type");
                    request.Headers.Add("X-Device-Type", "pc");
                }
            }
            catch
            {
                // ignore
            }
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
