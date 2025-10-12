using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using System.Data;

namespace QLTTTA_API.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> AuthenticateAsync(LoginRequest request);
        Task<RegisterResponse> RegisterAsync(RegisterRequest request);
        Task<string> TestDatabaseAsync();
        Task<bool> CheckSessionAsync(string username, string sessionId);
    }

    public class AuthService : IAuthService
    {
        private readonly string _connectionString;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IConfiguration configuration, ILogger<AuthService> logger)
        {
            _connectionString = configuration.GetConnectionString("OracleDbConnection") ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger;
        }

        public async Task<LoginResponse> AuthenticateAsync(LoginRequest request)
        {
            try
            {
                // 1) Thử kết nối bằng tài khoản người dùng để xác thực username/password thật
                _logger.LogInformation("Login attempt - Username: {Username}", request.Username);
                var baseCs = new OracleConnectionStringBuilder(_connectionString);
                var userCs = new OracleConnectionStringBuilder
                {
                    DataSource = baseCs.DataSource,
                    UserID = request.Username?.Trim(),
                    Password = request.Password
                };
                try
                {
                    using var userConn = new OracleConnection(userCs.ConnectionString);
                    await userConn.OpenAsync(); // nếu sai mật khẩu sẽ throw
                }
                catch (Exception credEx)
                {
                    _logger.LogWarning(credEx, "User credential connection failed for {Username}", request.Username);
                    return new LoginResponse { Success = false, Message = "Sai tên đăng nhập hoặc mật khẩu" };
                }

                // 2) Dùng kết nối quản trị để lấy thông tin và cập nhật SESSION_ID_HIENTAI, kiểm tra TRANG_THAI_KICH_HOAT
                using var adminConn = new OracleConnection(_connectionString);
                await adminConn.OpenAsync();

                // Kiểm tra kích hoạt và lấy thông tin người dùng từ schema tiếng Việt
                var infoSql = @"SELECT tk.ID_NGUOI_DUNG,
                                         tk.TEN_DANG_NHAP,
                                         tk.EMAIL,
                                         tk.TRANG_THAI_KICH_HOAT,
                                         vt.TEN_VAI_TRO,
                                         hv.HO_TEN
                                    FROM TAI_KHOAN tk
                               LEFT JOIN VAI_TRO vt ON vt.ID_VAI_TRO = tk.ID_VAI_TRO
                               LEFT JOIN HOC_VIEN hv ON hv.ID_HOC_VIEN = tk.ID_NGUOI_DUNG
                                   WHERE tk.TEN_DANG_NHAP = :u";
                using var infoCmd = new OracleCommand(infoSql, adminConn) { BindByName = true };
                infoCmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = request.Username?.Trim();
                using var rdr = await infoCmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (!await rdr.ReadAsync())
                {
                    return new LoginResponse { Success = false, Message = "Tài khoản không tồn tại" };
                }
                int ordId = rdr.GetOrdinal("ID_NGUOI_DUNG");
                int ordUser = rdr.GetOrdinal("TEN_DANG_NHAP");
                int ordEmail = rdr.GetOrdinal("EMAIL");
                int ordActive = rdr.GetOrdinal("TRANG_THAI_KICH_HOAT");
                int ordRole = rdr.GetOrdinal("TEN_VAI_TRO");
                int ordFull = -1; try { ordFull = rdr.GetOrdinal("HO_TEN"); } catch { }

                bool isActive = !rdr.IsDBNull(ordActive) && rdr.GetInt32(ordActive) == 1;
                if (!isActive)
                {
                    return new LoginResponse { Success = false, Message = "Tài khoản đã bị khoá" };
                }

                var userInfo = new UserInfo
                {
                    UserId = rdr.IsDBNull(ordId) ? 0 : rdr.GetInt32(ordId),
                    Username = rdr.IsDBNull(ordUser) ? string.Empty : rdr.GetString(ordUser),
                    Email = rdr.IsDBNull(ordEmail) ? string.Empty : rdr.GetString(ordEmail),
                    Role = rdr.IsDBNull(ordRole) ? string.Empty : rdr.GetString(ordRole),
                    FullName = (ordFull >= 0 && !rdr.IsDBNull(ordFull)) ? rdr.GetString(ordFull) : string.Empty
                };

                // Tạo và lưu Session ID mới (ngăn đăng nhập đồng thời)
                var sessionId = Guid.NewGuid().ToString("N");
                using (var upCmd = new OracleCommand("UPDATE TAI_KHOAN SET SESSION_ID_HIENTAI = :sid WHERE TEN_DANG_NHAP = :u", adminConn))
                {
                    upCmd.BindByName = true;
                    upCmd.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sessionId;
                    upCmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = userInfo.Username;
                    await upCmd.ExecuteNonQueryAsync();
                }

                _logger.LogInformation("Login success for {Username} with role {Role}", userInfo.Username, userInfo.Role);
                return new LoginResponse
                {
                    Success = true,
                    Message = "Đăng nhập thành công",
                    Token = GenerateToken(userInfo),
                    SessionId = sessionId,
                    User = userInfo
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xác thực người dùng (username={Username})", request?.Username);
                return new LoginResponse
                {
                    Success = false,
                    Message = $"Có lỗi xảy ra trong quá trình đăng nhập: {ex.Message}"
                };
            }
        }

        private string GenerateToken(UserInfo user)
        {
            // Tạo JWT token đơn giản hoặc session token
            // Trong demo này trả về một token đơn giản
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{user.UserId}:{user.Username}:{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"));
        }

        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("Bắt đầu đăng ký (SP_DANG_KY_HOC_VIEN) user: {Username}", request.Username);
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                using (var cmd = new OracleCommand("SP_DANG_KY_HOC_VIEN", connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.BindByName = true;
                    cmd.Parameters.Add("p_ten_dang_nhap", OracleDbType.Varchar2).Value = request.Username;
                    cmd.Parameters.Add("p_mat_khau", OracleDbType.Varchar2).Value = request.Password;
                    cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = request.Email;
                    cmd.Parameters.Add("p_ho_ten", OracleDbType.NVarchar2).Value = request.FullName;
                    cmd.Parameters.Add("p_gioi_tinh", OracleDbType.NVarchar2).Value = request.Sex;
                    cmd.Parameters.Add("p_ngay_sinh", OracleDbType.Date).Value = (object?)request.DateOfBirth ?? DBNull.Value;
                    cmd.Parameters.Add("p_sdt", OracleDbType.Varchar2).Value = request.PhoneNumber;
                    cmd.Parameters.Add("p_dia_chi", OracleDbType.NVarchar2).Value = (object?)request.Address ?? DBNull.Value;
                    var outMsg = new OracleParameter("p_ket_qua", OracleDbType.NVarchar2, 4000) { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(outMsg);

                    await cmd.ExecuteNonQueryAsync();
                    var resultMsg = outMsg.Value?.ToString() ?? string.Empty;
                    _logger.LogInformation("SP_DANG_KY_HOC_VIEN result: {Msg}", resultMsg);
                    if (!resultMsg.Contains("thành công", StringComparison.OrdinalIgnoreCase))
                    {
                        return new RegisterResponse { Success = false, Message = resultMsg };
                    }
                }

                // Lấy lại thông tin user vừa tạo
                using (var infoCmd = new OracleCommand(@"SELECT tk.ID_NGUOI_DUNG, tk.TEN_DANG_NHAP, tk.EMAIL, vt.TEN_VAI_TRO, hv.HO_TEN
                                                         FROM TAI_KHOAN tk
                                                         LEFT JOIN VAI_TRO vt ON vt.ID_VAI_TRO = tk.ID_VAI_TRO
                                                         LEFT JOIN HOC_VIEN hv ON hv.ID_HOC_VIEN = tk.ID_NGUOI_DUNG
                                                        WHERE tk.TEN_DANG_NHAP = :u", connection))
                {
                    infoCmd.BindByName = true;
                    infoCmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = request.Username;
                    using var rdr = await infoCmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
                    if (await rdr.ReadAsync())
                    {
                        var userInfo = new UserInfo
                        {
                            UserId = rdr.IsDBNull(rdr.GetOrdinal("ID_NGUOI_DUNG")) ? 0 : rdr.GetInt32(rdr.GetOrdinal("ID_NGUOI_DUNG")),
                            Username = rdr.IsDBNull(rdr.GetOrdinal("TEN_DANG_NHAP")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("TEN_DANG_NHAP")),
                            Email = rdr.IsDBNull(rdr.GetOrdinal("EMAIL")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("EMAIL")),
                            Role = rdr.IsDBNull(rdr.GetOrdinal("TEN_VAI_TRO")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("TEN_VAI_TRO")),
                            FullName = rdr.IsDBNull(rdr.GetOrdinal("HO_TEN")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("HO_TEN"))
                        };

                        return new RegisterResponse { Success = true, Message = "Đăng ký học viên thành công", User = userInfo };
                    }
                }

                return new RegisterResponse { Success = true, Message = "Đăng ký thành công" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đăng ký người dùng - Username: {Username}, Email: {Email}, ErrorMessage: {Message}",
                    request.Username, request.Email, ex.Message);

                // Trả về thông báo lỗi chi tiết hơn trong development
                var errorMessage = ex.Message.Contains("ORA-")
                    ? $"Lỗi database: {ex.Message}"
                    : "Có lỗi xảy ra trong quá trình đăng ký";

                return new RegisterResponse
                {
                    Success = false,
                    Message = errorMessage
                };
            }
        }

        public async Task<string> TestDatabaseAsync()
        {
            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                // Test basic connection
                var testSql = "SELECT 1 FROM DUAL";
                using var testCommand = new OracleCommand(testSql, connection);
                var result = await testCommand.ExecuteScalarAsync();

                // Check if ACCOUNTS table exists
                var checkTableSql = "SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME = 'ACCOUNTS'";
                using var checkTableCommand = new OracleCommand(checkTableSql, connection);
                var tableExists = Convert.ToInt32(await checkTableCommand.ExecuteScalarAsync()) > 0;

                // Get table structure if exists
                string tableInfo = "";
                if (tableExists)
                {
                    var columnsSql = "SELECT COLUMN_NAME, DATA_TYPE FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'ACCOUNTS' ORDER BY COLUMN_ID";
                    using var columnsCommand = new OracleCommand(columnsSql, connection);
                    using var reader = await columnsCommand.ExecuteReaderAsync();

                    var columns = new List<string>();
                    while (await reader.ReadAsync())
                    {
                        columns.Add($"{reader.GetString("COLUMN_NAME")} ({reader.GetString("DATA_TYPE")})");
                    }
                    tableInfo = string.Join(", ", columns);
                }

                return $"Database connected successfully. ACCOUNTS table exists: {tableExists}. Columns: {tableInfo}";
            }
            catch (Exception ex)
            {
                throw new Exception($"Database test failed: {ex.Message}");
            }
        }

        public async Task<bool> CheckSessionAsync(string username, string sessionId)
        {
            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();
                var sql = "SELECT SESSION_ID_HIENTAI FROM TAI_KHOAN WHERE TEN_DANG_NHAP = :u";
                using var cmd = new OracleCommand(sql, connection) { BindByName = true };
                cmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = username?.Trim();
                var dbVal = await cmd.ExecuteScalarAsync();
                var currentSid = dbVal?.ToString();
                if (string.IsNullOrEmpty(currentSid)) return false; // chưa có phiên hợp lệ
                return string.Equals(currentSid, sessionId, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CheckSessionAsync error for user {Username}", username);
                return false;
            }
        }
    }
}