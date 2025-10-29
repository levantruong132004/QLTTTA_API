using Oracle.ManagedDataAccess.Client;

namespace QLTTTA_API.Services
{
    /// <summary>
    /// Provider mở kết nối Oracle sử dụng Proxy Authentication: APP_PROXY -> (as) user đích.
    /// Mục tiêu: không bao giờ gửi mật khẩu người dùng cuối xuống DB; chặn đăng nhập trực tiếp bằng PROXY ONLY CONNECT.
    /// </summary>
    public class OracleProxyConnectionProvider : IOracleConnectionProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly IUserCredentialCache _credCache;
        private readonly ILogger<OracleProxyConnectionProvider> _logger;

        public OracleProxyConnectionProvider(
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            IUserCredentialCache credCache,
            ILogger<OracleProxyConnectionProvider> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _credCache = credCache;
            _logger = logger;
        }

        /// <summary>
        /// Trả về một OracleConnection được mở bởi tài khoản proxy (APP_PROXY), đại diện (proxy) cho user ứng dụng.
        /// </summary>
        public async Task<OracleConnection> GetUserConnectionAsync(CancellationToken ct = default)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                throw new InvalidOperationException("No HttpContext available for connection resolution");
            }

            var headers = httpContext.Request.Headers;
            var sessionId = headers["X-Session-Id"].FirstOrDefault();
            var deviceType = headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(deviceType)) deviceType = "pc";
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

            // Xác thực session với DB bằng admin connection (giống provider cũ)
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
                var col = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                using var checkCmd = new OracleCommand($"SELECT COUNT(*) FROM TAI_KHOAN WHERE ID_NGUOI_DUNG = :id AND {col} = :sid", adminConn)
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

            // Đọc cấu hình proxy
            var proxyUser = _configuration["Oracle:ProxyUser"]; // ví dụ: APP_PROXY
            var proxyPassword = _configuration["Oracle:ProxyPassword"];
            if (string.IsNullOrWhiteSpace(proxyUser) || string.IsNullOrWhiteSpace(proxyPassword))
            {
                throw new InvalidOperationException("Thiếu cấu hình Oracle:ProxyUser hoặc Oracle:ProxyPassword");
            }

            // Dùng DataSource từ chuỗi kết nối admin để đảm bảo cùng máy chủ/CDB/PDB
            var baseCs = new OracleConnectionStringBuilder(_configuration.GetConnectionString("OracleDbConnection"));
            var csb = new OracleConnectionStringBuilder
            {
                DataSource = baseCs.DataSource,
                UserID = proxyUser,
                Password = proxyPassword,
                ProxyUserId = cred.Username // ánh xạ 1-1: TEN_DANG_NHAP == username DB. Nếu khác, thay bằng tên user DB tương ứng.
            };

            var conn = new OracleConnection(csb.ConnectionString);
            await conn.OpenAsync(ct);

            // Gắn Client Identifier để VPD/Audit có thể nhận diện người dùng ứng dụng
            try
            {
                using var idCmd = new OracleCommand("BEGIN DBMS_SESSION.SET_IDENTIFIER(:id); END;", conn) { BindByName = true };
                idCmd.Parameters.Add(":id", OracleDbType.Varchar2).Value = cred.Username;
                await idCmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể set CLIENT_IDENTIFIER - tiếp tục không chặn.");
            }

            return conn;
        }
    }
}
