using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using System.Data;
using Microsoft.AspNetCore.Http;

namespace QLTTTA_API.Services
{
    /// <summary>
    /// Interface khai báo các nghiệp vụ xác thực & phiên làm việc người dùng.
    /// </summary>
    public interface IAuthService
    {
        Task<LoginResponse> AuthenticateAsync(LoginRequest request);
        Task<RegisterResponse> RegisterAsync(RegisterRequest request);
        Task<string> TestDatabaseAsync();
        Task<bool> CheckSessionAsync(string username, string sessionId, string? deviceType = null);
        Task LogoutAsync(string username, string sessionId, bool logoutAll = false);
        Task<OtpInitiateResponse> InitiateRegisterOtpAsync(RegisterRequest request);
        Task<RegisterResponse> VerifyRegisterOtpAsync(OtpVerifyRequest request);
        Task<ForgotPasswordInitiateResponse> InitiateForgotPasswordAsync(ForgotPasswordInitiateRequest request);
        Task<BasicResponse> VerifyForgotPasswordAsync(ForgotPasswordVerifyRequest request);
    }

    /// <summary>
    /// Triển khai nghiệp vụ xác thực người dùng dựa trên tài khoản Oracle thực (per-user connection) và quản lý SessionId.
    /// </summary>
    public class AuthService : BaseService, IAuthService
    {
        private readonly IUserCredentialCache _credCache;
        private readonly IOtpStore _otpStore;
        private readonly IEmailService _emailService;
        private readonly bool _useSpRegister;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthService(IConfiguration configuration, ILogger<AuthService> logger, IUserCredentialCache credCache, IOtpStore otpStore, IEmailService emailService, IHttpContextAccessor httpContextAccessor)
            : base(configuration, logger, null) // AuthService uses admin connection primarily
        {
            _credCache = credCache;
            _otpStore = otpStore;
            _emailService = emailService;
            _httpContextAccessor = httpContextAccessor;
            var useSp = configuration["Registration:UseStoredProcedure"];
            _useSpRegister = string.IsNullOrWhiteSpace(useSp) ? false : useSp.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<LoginResponse> AuthenticateAsync(LoginRequest request)
        {
            try
            {
                var deviceType = (request.DeviceType ?? "pc").Trim().ToLowerInvariant();
                if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
                
                _logger.LogInformation("Login attempt - Username: {Username}", request.Username);

                // 1. Check Oracle User Exists
                using (var adminConn = await GetAdminConnectionAsync())
                {
                    using (var cmd = new OracleCommand("SP_CHECK_ORACLE_USER_EXISTS", adminConn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = request.Username;
                        var pCount = cmd.Parameters.Add("p_count", OracleDbType.Int32);
                        pCount.Direction = ParameterDirection.Output;
                        await cmd.ExecuteNonQueryAsync();
                        
                        if (Convert.ToInt32(pCount.Value.ToString()) == 0)
                        {
                            _logger.LogWarning("Oracle USER not found in ALL_USERS for {Username}", request.Username);
                        }
                    }
                }

                // 2. Test Credential Login
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
                    await userConn.OpenAsync();
                }
                catch (OracleException oex)
                {
                    if (oex.Number == 28000) return new LoginResponse { Success = false, Message = "Tài khoản đã bị khóa do nhập sai mật khẩu quá số lần cho phép." };
                    if (oex.Number == 28001) return new LoginResponse { Success = false, Message = "Mật khẩu đã hết hạn." };
                    if (oex.Number == 1017) return new LoginResponse { Success = false, Message = "Sai tên đăng nhập hoặc mật khẩu" };
                    return new LoginResponse { Success = false, Message = $"Lỗi Oracle {oex.Number}" };
                }

                // 3. Get User Info & Update Session
                using (var adminConn = await GetAdminConnectionAsync())
                {
                    // Get Info
                    var users = await ExecuteStoredProcedureQueryAsync<UserInfo>("SP_GET_USER_INFO_BY_USERNAME", new { p_username = request.Username });
                    var userInfo = users.FirstOrDefault();
                    
                    if (userInfo == null) return new LoginResponse { Success = false, Message = "Tài khoản không tồn tại trong hệ thống quản lý" };
                    
                    if (userInfo.IsActive == 0) return new LoginResponse { Success = false, Message = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên." };
                    
                    // Update Session
                    var sessionId = Guid.NewGuid().ToString("N");
                    
                    // Lock row & Update
                    using var tx = adminConn.BeginTransaction();
                    try
                    {
                        // Lock
                        using (var cmdLock = new OracleCommand("SP_LOCK_SESSION_ROW", adminConn))
                        {
                            cmdLock.Transaction = tx;
                            cmdLock.CommandType = CommandType.StoredProcedure;
                            cmdLock.Parameters.Add("p_user_id", OracleDbType.Int32).Value = userInfo.UserId;
                            cmdLock.Parameters.Add("p_cursor", OracleDbType.RefCursor).Direction = ParameterDirection.Output;
                            using var rdr = await cmdLock.ExecuteReaderAsync(); // Just to lock
                        }

                        // Update
                        using (var cmdUp = new OracleCommand("SP_UPDATE_SESSION_ID", adminConn))
                        {
                            cmdUp.Transaction = tx;
                            cmdUp.CommandType = CommandType.StoredProcedure;
                            cmdUp.Parameters.Add("p_user_id", OracleDbType.Int32).Value = userInfo.UserId;
                            cmdUp.Parameters.Add("p_session_id", OracleDbType.Varchar2).Value = sessionId;
                            cmdUp.Parameters.Add("p_device_type", OracleDbType.Varchar2).Value = deviceType;
                            await cmdUp.ExecuteNonQueryAsync();
                        }
                        
                        await tx.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await tx.RollbackAsync();
                        _logger.LogError(ex, "Error updating session");
                        return new LoginResponse { Success = false, Message = "Lỗi cập nhật phiên làm việc" };
                    }

                    _credCache.Set(sessionId, request.Username!.Trim(), request.Password!, TimeSpan.FromHours(1));

                    return new LoginResponse
                    {
                        Success = true,
                        Message = "Đăng nhập thành công",
                        Token = GenerateToken(userInfo),
                        SessionId = sessionId,
                        User = userInfo
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error");
                return new LoginResponse { Success = false, Message = "Lỗi đăng nhập: " + ex.Message };
            }
        }

        private string GenerateToken(UserInfo user)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{user.UserId}:{user.Username}:{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"));
        }

        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                using var connection = await GetAdminConnectionAsync();
                
                // 1. Try SP_DANG_KY_HOC_VIEN if enabled
                if (_useSpRegister)
                {
                    try
                    {
                        using var cmd = new OracleCommand("SP_DANG_KY_HOC_VIEN", connection);
                        cmd.CommandType = CommandType.StoredProcedure;
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
                        if (!resultMsg.Contains("thành công", StringComparison.OrdinalIgnoreCase))
                        {
                            return new RegisterResponse { Success = false, Message = resultMsg };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "SP_DANG_KY_HOC_VIEN failed, falling back to inline logic");
                        return await RegisterFallbackAsync(connection, request);
                    }
                }
                else
                {
                    return await RegisterFallbackAsync(connection, request);
                }

                // Create Oracle User
                await CreateOracleUserAsync(connection, request.Username, request.Password);

                // Get Info
                var users = await ExecuteStoredProcedureQueryAsync<UserInfo>("SP_GET_USER_INFO_BY_USERNAME", new { p_username = request.Username });
                return new RegisterResponse { Success = true, Message = "Đăng ký thành công", User = users.FirstOrDefault() };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Register error");
                return new RegisterResponse { Success = false, Message = "Lỗi đăng ký: " + ex.Message };
            }
        }

        private async Task<RegisterResponse> RegisterFallbackAsync(OracleConnection connection, RegisterRequest request)
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                // Check Username
                using (var cmd = new OracleCommand("SP_CHECK_USERNAME_EXISTS", connection))
                {
                    cmd.Transaction = transaction;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = request.Username;
                    var pCount = cmd.Parameters.Add("p_count", OracleDbType.Int32);
                    pCount.Direction = ParameterDirection.Output;
                    await cmd.ExecuteNonQueryAsync();
                    if (Convert.ToInt32(pCount.Value.ToString()) > 0) return new RegisterResponse { Success = false, Message = "Tên đăng nhập đã tồn tại" };
                }

                // Check Email
                using (var cmd = new OracleCommand("SP_CHECK_EMAIL_EXISTS", connection))
                {
                    cmd.Transaction = transaction;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = request.Email;
                    var pCount = cmd.Parameters.Add("p_count", OracleDbType.Int32);
                    pCount.Direction = ParameterDirection.Output;
                    await cmd.ExecuteNonQueryAsync();
                    if (Convert.ToInt32(pCount.Value.ToString()) > 0) return new RegisterResponse { Success = false, Message = "Email đã được sử dụng" };
                }

                // Get Role
                int roleId = 1;
                using (var cmd = new OracleCommand("SP_GET_ROLE_ID_BY_NAME", connection))
                {
                    cmd.Transaction = transaction;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_role_name", OracleDbType.Varchar2).Value = "HOC VIEN";
                    var pRoleId = cmd.Parameters.Add("p_role_id", OracleDbType.Int32);
                    pRoleId.Direction = ParameterDirection.Output;
                    await cmd.ExecuteNonQueryAsync();
                    if (pRoleId.Value != null && int.TryParse(pRoleId.Value.ToString(), out var rid) && rid > 0) roleId = rid;
                }

                // Create Account
                int newUserId = 0;
                using (var cmd = new OracleCommand("SP_CREATE_ACCOUNT", connection))
                {
                    cmd.Transaction = transaction;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = request.Username;
                    cmd.Parameters.Add("p_password", OracleDbType.Varchar2).Value = HashPasswordSha256(request.Password);
                    cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = request.Email;
                    cmd.Parameters.Add("p_role_id", OracleDbType.Int32).Value = roleId;
                    var pUserId = cmd.Parameters.Add("p_user_id", OracleDbType.Int32);
                    pUserId.Direction = ParameterDirection.Output;
                    await cmd.ExecuteNonQueryAsync();
                    if (pUserId.Value != null && int.TryParse(pUserId.Value.ToString(), out var uid)) newUserId = uid;
                }

                if (newUserId <= 0) throw new Exception("Failed to create account");

                // Create Student
                using (var cmd = new OracleCommand("SP_CREATE_STUDENT", connection))
                {
                    cmd.Transaction = transaction;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = newUserId;
                    cmd.Parameters.Add("p_fullname", OracleDbType.NVarchar2).Value = request.FullName;
                    cmd.Parameters.Add("p_sex", OracleDbType.NVarchar2).Value = (object?)request.Sex ?? DBNull.Value;
                    cmd.Parameters.Add("p_dob", OracleDbType.Date).Value = (object?)request.DateOfBirth ?? DBNull.Value;
                    cmd.Parameters.Add("p_phone", OracleDbType.Varchar2).Value = request.PhoneNumber;
                    cmd.Parameters.Add("p_addr", OracleDbType.NVarchar2).Value = (object?)request.Address ?? DBNull.Value;
                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                
                // Create Oracle User
                await CreateOracleUserAsync(connection, request.Username, request.Password);

                return new RegisterResponse { Success = true, Message = "Đăng ký thành công" };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Fallback register error");
                return new RegisterResponse { Success = false, Message = "Lỗi đăng ký: " + ex.Message };
            }
        }

        private static string HashPasswordSha256(string password)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(password);
            var hash = sha.ComputeHash(bytes);
            return "SHA256:" + BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        private async Task<bool> CreateOracleUserAsync(OracleConnection adminConnection, string username, string password)
        {
            // Keep existing DDL logic
            var uname = username.Trim().ToUpperInvariant();
            try
            {
                using (var existCmd = new OracleCommand("SP_CHECK_ORACLE_USER_EXISTS", adminConnection))
                {
                    existCmd.CommandType = CommandType.StoredProcedure;
                    existCmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = uname;
                    var pCount = existCmd.Parameters.Add("p_count", OracleDbType.Int32);
                    pCount.Direction = ParameterDirection.Output;
                    await existCmd.ExecuteNonQueryAsync();
                    if (Convert.ToInt32(pCount.Value.ToString()) > 0)
                    {
                        // Update password logic if needed
                        return true;
                    }
                }

                var createSql = $"CREATE USER \"{uname}\" IDENTIFIED BY \"{password}\" DEFAULT TABLESPACE USERS TEMPORARY TABLESPACE TEMP QUOTA UNLIMITED ON USERS";
                using (var createCmd = new OracleCommand(createSql, adminConnection)) await createCmd.ExecuteNonQueryAsync();

                var grantSql = $"GRANT CONNECT, RESOURCE TO \"{uname}\"";
                using (var grantCmd = new OracleCommand(grantSql, adminConnection)) await grantCmd.ExecuteNonQueryAsync();

                await EnsureStudentGrantsAsync(adminConnection, uname);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Oracle USER");
                return false;
            }
        }

        private async Task EnsureStudentGrantsAsync(OracleConnection adminConnection, string upperUsername)
        {
            try
            {
                using var g1 = new OracleCommand($"GRANT role_hocvien TO \"{upperUsername}\"", adminConnection);
                await g1.ExecuteNonQueryAsync();
            }
            catch {}
            try
            {
                using var g2 = new OracleCommand($"GRANT CREATE SESSION TO \"{upperUsername}\"", adminConnection);
                await g2.ExecuteNonQueryAsync();
            }
            catch {}
        }

        public async Task<string> TestDatabaseAsync()
        {
            // Simplified test
            using var conn = await GetAdminConnectionAsync();
            using var cmd = new OracleCommand("SELECT 1 FROM DUAL", conn);
            await cmd.ExecuteScalarAsync();
            return "Database connected";
        }

        public async Task<bool> CheckSessionAsync(string username, string sessionId, string? deviceType = null)
        {
            try
            {
                using var connection = await GetAdminConnectionAsync();
                using var cmd = new OracleCommand("SP_GET_SESSION_ID", connection);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = username;
                cmd.Parameters.Add("p_device_type", OracleDbType.Varchar2).Value = deviceType ?? "pc";
                var pSid = cmd.Parameters.Add("p_session_id", OracleDbType.Varchar2, 100);
                pSid.Direction = ParameterDirection.Output;
                
                await cmd.ExecuteNonQueryAsync();
                var currentSid = pSid.Value?.ToString();
                
                if (string.IsNullOrEmpty(currentSid)) return false;
                return string.Equals(currentSid, sessionId, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        public async Task LogoutAsync(string username, string sessionId, bool logoutAll = false)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return;
            _credCache.Remove(sessionId);
            
            try
            {
                using var conn = await GetAdminConnectionAsync();
                var deviceType = _httpContextAccessor.HttpContext?.Request?.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
                
                if (string.IsNullOrWhiteSpace(username))
                {
                    using var cmd = new OracleCommand("SP_GET_USERNAME_BY_SESSION", conn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_session_id", OracleDbType.Varchar2).Value = sessionId;
                    cmd.Parameters.Add("p_device_type", OracleDbType.Varchar2).Value = deviceType;
                    var pUser = cmd.Parameters.Add("p_username", OracleDbType.Varchar2, 100);
                    pUser.Direction = ParameterDirection.Output;
                    await cmd.ExecuteNonQueryAsync();
                    username = pUser.Value?.ToString() ?? string.Empty;
                }

                if (logoutAll)
                {
                    // Update both columns to NULL
                    using var cmdAll = new OracleCommand("UPDATE TAI_KHOAN SET SESSION_ID_PC = NULL, SESSION_ID_MOBILE = NULL WHERE TEN_DANG_NHAP = :username", conn);
                    cmdAll.Parameters.Add(":username", OracleDbType.Varchar2).Value = username;
                    await cmdAll.ExecuteNonQueryAsync();
                }
                else
                {
                    using var cmdClear = new OracleCommand("SP_CLEAR_SESSION", conn);
                    cmdClear.CommandType = CommandType.StoredProcedure;
                    cmdClear.Parameters.Add("p_session_id", OracleDbType.Varchar2).Value = sessionId;
                    cmdClear.Parameters.Add("p_device_type", OracleDbType.Varchar2).Value = deviceType;
                    await cmdClear.ExecuteNonQueryAsync();
                }

                // Kill session logic
                using var cmdKill = new OracleCommand("SP_KILL_USER_SESSION", conn);
                cmdKill.CommandType = CommandType.StoredProcedure;
                cmdKill.Parameters.Add("p_username", OracleDbType.Varchar2).Value = username;
                await cmdKill.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout error");
            }
        }

        public async Task<OtpInitiateResponse> InitiateRegisterOtpAsync(RegisterRequest request)
        {
            try
            {
                using var connection = await GetAdminConnectionAsync();
                
                // Check Username
                using (var cmd = new OracleCommand("SP_CHECK_USERNAME_EXISTS", connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = request.Username;
                    var pCount = cmd.Parameters.Add("p_count", OracleDbType.Int32);
                    pCount.Direction = ParameterDirection.Output;
                    await cmd.ExecuteNonQueryAsync();
                    if (Convert.ToInt32(pCount.Value.ToString()) > 0) return new OtpInitiateResponse { Success = false, Message = "Tên đăng nhập đã tồn tại" };
                }

                // Check Email
                using (var cmd = new OracleCommand("SP_CHECK_EMAIL_EXISTS", connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = request.Email;
                    var pCount = cmd.Parameters.Add("p_count", OracleDbType.Int32);
                    pCount.Direction = ParameterDirection.Output;
                    await cmd.ExecuteNonQueryAsync();
                    if (Convert.ToInt32(pCount.Value.ToString()) > 0) return new OtpInitiateResponse { Success = false, Message = "Email đã được sử dụng" };
                }
            }
            catch {}

            var entry = _otpStore.Create("register", request.Email, request.Username, request, TimeSpan.FromMinutes(10));
            await _emailService.SendAsync(request.Email, "Xác thực đăng ký tài khoản", 
                $"<p>Xin chào {request.Username},</p><p>Mã OTP xác thực đăng ký của bạn là: <b>{entry.OtpCode}</b></p><p>Mã có hiệu lực trong 10 phút.</p>");
            return new OtpInitiateResponse { Success = true, CorrelationId = entry.CorrelationId };
        }

        public async Task<RegisterResponse> VerifyRegisterOtpAsync(OtpVerifyRequest request)
        {
            if (!_otpStore.Validate(request.CorrelationId, request.Otp, out var entry) || entry == null)
                return new RegisterResponse { Success = false, Message = "Mã OTP không hợp lệ hoặc đã hết hạn" };
            
            if (entry.Payload is RegisterRequest reg)
            {
                var res = await RegisterAsync(reg);
                if (res.Success) _otpStore.Remove(request.CorrelationId);
                return res;
            }
            return new RegisterResponse { Success = false, Message = "Dữ liệu đăng ký không hợp lệ" };
        }

        public async Task<ForgotPasswordInitiateResponse> InitiateForgotPasswordAsync(ForgotPasswordInitiateRequest request)
        {
            // Check if user exists
             var users = await ExecuteStoredProcedureQueryAsync<UserInfo>("SP_GET_USER_INFO_BY_USERNAME", new { p_username = request.Username });
             if (!users.Any() || !users.First().Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase))
             {
                 return new ForgotPasswordInitiateResponse { Success = false, Message = "Tài khoản không tồn tại hoặc email không khớp" };
             }

             var entry = _otpStore.Create("forgot", request.Email, request.Username, null, TimeSpan.FromMinutes(10));
             await _emailService.SendAsync(request.Email, "Yêu cầu đặt lại mật khẩu", 
                 $"<p>Xin chào {request.Username},</p><p>Mã OTP đặt lại mật khẩu của bạn là: <b>{entry.OtpCode}</b></p><p>Mã có hiệu lực trong 10 phút.</p>");
             return new ForgotPasswordInitiateResponse { Success = true, CorrelationId = entry.CorrelationId };
        }

        public async Task<BasicResponse> VerifyForgotPasswordAsync(ForgotPasswordVerifyRequest request)
        {
            if (!_otpStore.Validate(request.CorrelationId, request.Otp, out var entry))
                return new BasicResponse { Success = false, Message = "Mã OTP không hợp lệ hoặc đã hết hạn" };

            // Reset logic (DDL)
            try
            {
                using var conn = await GetAdminConnectionAsync();
                var sql = $"ALTER USER \"{entry.Username.ToUpper()}\" IDENTIFIED BY \"{request.NewPassword}\"";
                using var cmd = new OracleCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
                _otpStore.Remove(request.CorrelationId);
                return new BasicResponse { Success = true, Message = "Đổi mật khẩu thành công" };
            }
            catch (Exception ex)
            {
                return new BasicResponse { Success = false, Message = "Lỗi: " + ex.Message };
            }
        }
    }
}