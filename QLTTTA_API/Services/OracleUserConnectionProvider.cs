using System.Collections.Concurrent;
using Oracle.ManagedDataAccess.Client;

namespace QLTTTA_API.Services
{
    public interface IUserCredentialCache
    {
        void Set(string sessionId, string username, string password, TimeSpan ttl);
        bool TryGet(string sessionId, out (string Username, string Password, DateTime Expiry) cred);
        void Remove(string sessionId);
    }

    public class InMemoryUserCredentialCache : IUserCredentialCache
    {
        private readonly ConcurrentDictionary<string, (string Username, string Password, DateTime Expiry)> _cache = new();

        public void Set(string sessionId, string username, string password, TimeSpan ttl)
        {
            var expiry = DateTime.UtcNow.Add(ttl);
            _cache[sessionId] = (username, password, expiry);
        }

        public bool TryGet(string sessionId, out (string Username, string Password, DateTime Expiry) cred)
        {
            if (_cache.TryGetValue(sessionId, out cred))
            {
                if (cred.Expiry > DateTime.UtcNow)
                {
                    return true;
                }
                // expired
                _cache.TryRemove(sessionId, out _);
            }
            cred = default;
            return false;
        }

        public void Remove(string sessionId)
        {
            _cache.TryRemove(sessionId, out _);
        }
    }

    public interface IOracleConnectionProvider
    {
        Task<OracleConnection> GetUserConnectionAsync(CancellationToken ct = default);
    }

    public class OracleUserConnectionProvider : IOracleConnectionProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly IUserCredentialCache _credCache;
        private readonly ILogger<OracleUserConnectionProvider> _logger;

        public OracleUserConnectionProvider(
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            IUserCredentialCache credCache,
            ILogger<OracleUserConnectionProvider> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _credCache = credCache;
            _logger = logger;
        }

        public async Task<OracleConnection> GetUserConnectionAsync(CancellationToken ct = default)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                throw new InvalidOperationException("No HttpContext available for connection resolution");
            }

            var headers = httpContext.Request.Headers;
            var sessionId = headers["X-Session-Id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                _logger.LogWarning("Missing X-Session-Id header in request to {Path}", httpContext.Request.Path);
                throw new UnauthorizedAccessException("Thiếu phiên đăng nhập");
            }

            if (!_credCache.TryGet(sessionId!, out var cred))
            {
                _logger.LogWarning("Session not found or expired for SessionId={SessionId}", sessionId);
                throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn hoặc không hợp lệ");
            }

            // Optional: verify session still matches DB before opening user connection
            try
            {
                var adminCs = _configuration.GetConnectionString("OracleDbConnection") ?? throw new Exception("Missing OracleDbConnection");
                using var adminConn = new OracleConnection(adminCs);
                await adminConn.OpenAsync(ct);
                int? userId = null;
                using (var findCmd = new OracleCommand("SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE TEN_DANG_NHAP = :u", adminConn) { BindByName = true })
                {
                    findCmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = cred.Username;
                    var o = await findCmd.ExecuteScalarAsync(ct);
                    if (o != null && int.TryParse(o.ToString(), out var idVal)) userId = idVal;
                }
                if (!userId.HasValue)
                {
                    throw new UnauthorizedAccessException("Không tìm thấy tài khoản");
                }
                using var checkCmd = new OracleCommand("SELECT COUNT(*) FROM TAI_KHOAN WHERE ID_NGUOI_DUNG = :id AND SESSION_ID_HIENTAI = :sid", adminConn)
                { BindByName = true };
                checkCmd.Parameters.Add(":id", OracleDbType.Int32).Value = userId.Value;
                checkCmd.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sessionId;
                var cntObj = await checkCmd.ExecuteScalarAsync(ct);
                var cnt = Convert.ToInt32(cntObj ?? 0);
                if (cnt == 0)
                {
                    _logger.LogWarning("Session mismatch in DB for user {User}", cred.Username);
                    throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ");
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating session against DB");
                throw new UnauthorizedAccessException("Không xác thực được phiên đăng nhập");
            }

            // Build user connection using same DataSource as admin connection string
            var baseCs = new OracleConnectionStringBuilder(_configuration.GetConnectionString("OracleDbConnection"));
            var userCs = new OracleConnectionStringBuilder
            {
                DataSource = baseCs.DataSource,
                UserID = cred.Username,
                Password = cred.Password
            };

            var userConn = new OracleConnection(userCs.ConnectionString);
            await userConn.OpenAsync(ct);
            return userConn;
        }
    }
}
