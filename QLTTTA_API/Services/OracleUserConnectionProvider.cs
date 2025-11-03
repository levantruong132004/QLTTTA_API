using System.Collections.Concurrent;
using Oracle.ManagedDataAccess.Client;

namespace QLTTTA_API.Services
{
    /// <summary>
    /// Interface cache lưu tạm thông tin chứng thực người dùng (username + password) theo SessionId.
    /// Dùng để mở lại kết nối "per-user" cho các request sau đăng nhập mà không phải hỏi lại mật khẩu.
    /// </summary>
    public interface IUserCredentialCache
    {
        /// <summary>Thêm hoặc cập nhật credential cho một SessionId với thời gian hết hạn (TTL).</summary>
        void Set(string sessionId, string username, string password, TimeSpan ttl);
        /// <summary>Thử lấy credential theo SessionId. Trả về false nếu hết hạn hoặc không tồn tại.</summary>
        bool TryGet(string sessionId, out (string Username, string Password, DateTime Expiry) cred);
        /// <summary>Xóa credential theo SessionId (ví dụ khi logout).</summary>
        void Remove(string sessionId);
    }

    /// <summary>
    /// Triển khai cache đơn giản trong bộ nhớ bằng ConcurrentDictionary.
    /// Không dùng cho môi trường nhiều instance (cần cache phân tán nếu scale-out).
    /// </summary>
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

    /// <summary>
    /// Interface cung cấp kết nối Oracle ở ngữ cảnh người dùng đang đăng nhập (per-user connection).
    /// </summary>
    public interface IOracleConnectionProvider
    {
        /// <summary>
        /// Lấy kết nối Oracle theo tài khoản học viên (dựa trên SessionId trong header X-Session-Id).
        /// </summary>
        Task<OracleConnection> GetUserConnectionAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Provider mở kết nối Oracle dưới quyền của chính học viên thay vì tài khoản admin.
    /// Quy trình: đọc X-Session-Id -> tìm credential trong cache -> xác thực session với DB -> mở kết nối user.
    /// </summary>
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

        /// <summary>
        /// Trả về một OracleConnection mở bằng tài khoản user. Ném UnauthorizedAccessException nếu phiên không hợp lệ.
        /// </summary>
        public async Task<OracleConnection> GetUserConnectionAsync(CancellationToken ct = default)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                throw new InvalidOperationException("No HttpContext available for connection resolution");
            }

            var headers = httpContext.Request.Headers;
            var sessionId = headers["X-Session-Id"].FirstOrDefault(); // Header do Web tự gắn từ cookie SessionId
            var deviceType = headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
            if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                _logger.LogWarning("Missing X-Session-Id header in request to {Path}", httpContext.Request.Path);
                throw new UnauthorizedAccessException("Thiếu phiên đăng nhập");
            }

            if (!_credCache.TryGet(sessionId!, out var cred)) // Kiểm tra credential trong cache còn sống không
            {
                _logger.LogWarning("Session not found or expired for SessionId={SessionId}", sessionId);
                throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn hoặc không hợp lệ");
            }

            // Optional: verify session still matches DB before opening user connection
            try
            {
                var adminCs = _configuration.GetConnectionString("OracleDbConnection") ?? throw new Exception("Missing OracleDbConnection");
                using var adminConn = new OracleConnection(adminCs); // Mở kết nối admin để kiểm tra phiên trong DB
                await adminConn.OpenAsync(ct);
                int? userId = null;
                using (var findCmd = new OracleCommand("SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE TEN_DANG_NHAP = :u", adminConn) { BindByName = true }) // lấy ID người dùng
                {
                    findCmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = cred.Username;
                    var o = await findCmd.ExecuteScalarAsync(ct);
                    if (o != null && int.TryParse(o.ToString(), out var idVal)) userId = idVal;
                }
                if (!userId.HasValue)
                {
                    throw new UnauthorizedAccessException("Không tìm thấy tài khoản");
                }
                // Kiểm tra theo cột per-device (không fallback legacy)
                string columnName = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                using var checkCmdNew = new OracleCommand($"SELECT COUNT(*) FROM TAI_KHOAN WHERE ID_NGUOI_DUNG = :id AND {columnName} = :sid", adminConn) { BindByName = true };
                checkCmdNew.Parameters.Add(":id", OracleDbType.Int32).Value = userId.Value;
                checkCmdNew.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sessionId;
                var cntObjNew = await checkCmdNew.ExecuteScalarAsync(ct);
                var cnt = Convert.ToInt32(cntObjNew ?? 0);
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
            var baseCs = new OracleConnectionStringBuilder(_configuration.GetConnectionString("OracleDbConnection")); // Phân tích để lấy DataSource
            var userCs = new OracleConnectionStringBuilder
            {
                DataSource = baseCs.DataSource,
                UserID = cred.Username,
                Password = cred.Password
            };

            var userConn = new OracleConnection(userCs.ConnectionString); // Mở kết nối bằng credential học viên
            await userConn.OpenAsync(ct);
            return userConn;
        }
    }
}
