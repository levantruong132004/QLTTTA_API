using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using System.Data;
using System.Text.Json;

namespace QLTTTA_API.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> AuthenticateAsync(LoginRequest request);
        Task<RegisterResponse> RegisterAsync(RegisterRequest request);
        Task<OtpResponse> VerifyOtpAsync(VerifyOtpRequest request);
        Task<OtpResponse> ResendOtpAsync(string username);
        Task<string> TestDatabaseAsync();
        Task<bool> CheckSessionAsync(string username, string sessionId);
        Task LogoutAsync(string sessionId);
        Task<(bool Success, string Message)> VerifyEmailAsync(string username, string token);
        Task<OtpResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
        Task<OtpResponse> ResetPasswordAsync(ResetPasswordRequest request);
    }

    public class AuthService : IAuthService
    {
        private readonly string _connectionString;
        private readonly ILogger<AuthService> _logger;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;
        private readonly IEmailTemplateRenderer _tmpl;

        public AuthService(IConfiguration configuration, ILogger<AuthService> logger, IEmailService emailService, IEmailTemplateRenderer tmpl)
        {
            _connectionString = configuration.GetConnectionString("OracleDbConnection") ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger;
            _emailService = emailService;
            _config = configuration;
            _tmpl = tmpl;
        }

        public async Task<LoginResponse> AuthenticateAsync(LoginRequest request)
        {
            try
            {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();

                const string sql = @"SELECT TEN_DANG_NHAP, MAT_KHAU, EMAIL, TRANG_THAI_KICH_HOAT 
                                      FROM TAI_KHOAN WHERE TEN_DANG_NHAP = :u";
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = request.Username?.Trim();
                using var rdr = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (!await rdr.ReadAsync())
                    return new LoginResponse { Success = false, Message = "Tài khoản không tồn tại." };

                var dbPassword = rdr.GetString(1);
                var email = rdr.GetString(2);
                var active = rdr.GetInt32(3) == 1;

                if (!active)
                    return new LoginResponse { Success = false, Message = "Tài khoản chưa kích hoạt. Vui lòng xác thực bằng OTP." };

                if (!string.Equals(dbPassword, request.Password))
                    return new LoginResponse { Success = false, Message = "Sai mật khẩu." };

                return new LoginResponse
                {
                    Success = true,
                    Message = "Đăng nhập thành công.",
                    User = new UserInfo { Username = request.Username!, Email = email, Role = "User" }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AuthenticateAsync error");
                return new LoginResponse { Success = false, Message = "Lỗi đăng nhập: " + ex.Message };
            }
        }

        // OTP-first: store registration data with OTP and return quickly
        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                var csb = new OracleConnectionStringBuilder(_connectionString) { ConnectionTimeout = 15 };
                using var conn = new OracleConnection(csb.ConnectionString);
                await conn.OpenAsync();

                await EnsureOtpTableAsync(conn);

                // Normalize inputs to avoid nullability issues
                var username = (request.Username ?? string.Empty).Trim();
                var email = (request.Email ?? string.Empty).Trim();
                var fullName = string.IsNullOrWhiteSpace(request.FullName) ? username : request.FullName;
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
                {
                    return new RegisterResponse { Success = false, Message = "Thiếu tên đăng nhập hoặc email." };
                }

                // 1) Check if username already exists
                string? existingEmail = null; int existingActive = 0; bool userExists = false;
                using (var chkUser = new OracleCommand("SELECT EMAIL, NVL(TRANG_THAI_KICH_HOAT,0) FROM TAI_KHOAN WHERE TEN_DANG_NHAP = :u", conn) { BindByName = true })
                {
                    chkUser.Parameters.Add(":u", OracleDbType.Varchar2).Value = username;
                    using var rdr = await chkUser.ExecuteReaderAsync(CommandBehavior.SingleRow);
                    if (await rdr.ReadAsync())
                    {
                        userExists = true;
                        existingEmail = rdr.IsDBNull(0) ? null : rdr.GetString(0);
                        existingActive = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                    }
                }

                if (userExists)
                {
                    if (existingActive == 1)
                    {
                        return new RegisterResponse { Success = false, Message = "Tên đăng nhập đã tồn tại." };
                    }

                    // Username exists but not active -> resend OTP to the account's registered email
                    var otp = new Random().Next(100000, 999999).ToString();

                    // Clear previous OTPs for this username
                    using (var del = new OracleCommand("DELETE FROM EMAIL_OTP_CODES WHERE USERNAME = :u", conn) { BindByName = true })
                    {
                        del.Parameters.Add(":u", OracleDbType.NVarchar2).Value = username;
                        await del.ExecuteNonQueryAsync();
                    }

                    // Keep the latest registration data just in case (could be null)
                    var regJson = JsonSerializer.Serialize(request);
                    using (var ins = new OracleCommand("INSERT INTO EMAIL_OTP_CODES (USERNAME, OTP_CODE, EXPIRES_AT, REG_DATA) VALUES (:u, :c, :exp, :data)", conn) { BindByName = true })
                    {
                        ins.Parameters.Add(":u", OracleDbType.NVarchar2).Value = username;
                        ins.Parameters.Add(":c", OracleDbType.Varchar2).Value = otp;
                        ins.Parameters.Add(":exp", OracleDbType.Date).Value = DateTime.UtcNow.AddMinutes(10);
                        ins.Parameters.Add(":data", OracleDbType.Clob).Value = regJson;
                        await ins.ExecuteNonQueryAsync();
                    }

                    var toEmail = string.IsNullOrWhiteSpace(existingEmail) ? email : existingEmail;
                    var model = new Dictionary<string, string>
                    {
                        ["FullName"] = fullName,
                        ["Username"] = username,
                        ["Otp"] = otp,
                        ["AppName"] = _config["Email:FromName"] ?? "QLTTTA"
                    };
                    var body = await _tmpl.RenderAsync("Otp", model);
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        body = $@"<p>Xin chào {System.Net.WebUtility.HtmlEncode(fullName)},</p>
                               <p>Mã OTP xác thực tài khoản của bạn là:</p>
                               <h2 style='letter-spacing:4px'>{otp}</h2>
                               <p>OTP có hiệu lực trong 10 phút.</p>";
                    }
                    try
                    {
                        await _emailService.SendAsync(toEmail, "OTP xác thực tài khoản QLTTTA", body);
                    }
                    catch (Exception mailEx)
                    {
                        _logger.LogError(mailEx, "Resend OTP on re-register failed for {Username}", username);
                    }

                    return new RegisterResponse { Success = true, Message = "Tài khoản đã tồn tại nhưng chưa kích hoạt. Đã gửi lại OTP tới email của bạn." };
                }

                // 2) Check if email already used by another ACTIVE account (case-insensitive, trimmed)
                int emailCount;
                using (var chkEmail = new OracleCommand("SELECT COUNT(*) FROM TAI_KHOAN WHERE UPPER(TRIM(EMAIL)) = UPPER(TRIM(:e)) AND NVL(TRANG_THAI_KICH_HOAT,0) = 1", conn) { BindByName = true })
                {
                    chkEmail.Parameters.Add(":e", OracleDbType.Varchar2).Value = email;
                    emailCount = Convert.ToInt32(await chkEmail.ExecuteScalarAsync());
                }
                _logger.LogInformation("Register: email '{Email}' activeCount={Count}", email, emailCount);
                if (emailCount > 0)
                {
                    return new RegisterResponse { Success = false, Message = "Email đã được sử dụng." };
                }

                // Clear previous OTPs for this username (fresh register)
                using (var del = new OracleCommand("DELETE FROM EMAIL_OTP_CODES WHERE USERNAME = :u", conn) { BindByName = true })
                {
                    del.Parameters.Add(":u", OracleDbType.NVarchar2).Value = username;
                    await del.ExecuteNonQueryAsync();
                }

                var otpNew = new Random().Next(100000, 999999).ToString();
                var regJsonNew = JsonSerializer.Serialize(request);

                // Insert pending registration with OTP and JSON data
                using (var ins = new OracleCommand("INSERT INTO EMAIL_OTP_CODES (USERNAME, OTP_CODE, EXPIRES_AT, REG_DATA) VALUES (:u, :c, :exp, :data)", conn)
                { BindByName = true })
                {
                    ins.Parameters.Add(":u", OracleDbType.NVarchar2).Value = username;
                    ins.Parameters.Add(":c", OracleDbType.Varchar2).Value = otpNew;
                    ins.Parameters.Add(":exp", OracleDbType.Date).Value = DateTime.UtcNow.AddMinutes(10);
                    ins.Parameters.Add(":data", OracleDbType.Clob).Value = regJsonNew;
                    await ins.ExecuteNonQueryAsync();
                }

                // Send OTP email
                var smtpUser = _config["Email:SmtpUser"]; var smtpPass = _config["Email:SmtpPass"];
                if (string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
                {
                    _logger.LogWarning("[DEV] Email not configured, OTP for {Username} is {Otp}", username, otpNew);
                }

                var modelNew = new Dictionary<string, string>
                {
                    ["FullName"] = fullName,
                    ["Username"] = username,
                    ["Otp"] = otpNew,
                    ["AppName"] = _config["Email:FromName"] ?? "QLTTTA"
                };
                var bodyNew = await _tmpl.RenderAsync("Otp", modelNew);
                if (string.IsNullOrWhiteSpace(bodyNew))
                {
                    bodyNew = $@"<p>Xin chào {System.Net.WebUtility.HtmlEncode(fullName)},</p>
                               <p>Mã OTP xác thực tài khoản của bạn là:</p>
                               <h2 style='letter-spacing:4px'>{otpNew}</h2>
                               <p>OTP có hiệu lực trong 10 phút. Nếu bạn không yêu cầu, vui lòng bỏ qua email này.</p>";
                }
                try
                {
                    await _emailService.SendAsync(email, "OTP xác thực tài khoản QLTTTA", bodyNew);
                }
                catch (Exception mailEx)
                {
                    _logger.LogError(mailEx, "Send OTP failed for {Username}", username);
                }

                return new RegisterResponse { Success = true, Message = "Đã gửi OTP. Vui lòng kiểm tra email và nhập mã OTP để hoàn tất đăng ký." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RegisterAsync error");
                return new RegisterResponse { Success = false, Message = ex.Message.Contains("ORA-") ? ("Lỗi database: " + ex.Message) : "Có lỗi xảy ra khi đăng ký" };
            }
        }

        public async Task<OtpResponse> VerifyOtpAsync(VerifyOtpRequest request)
        {
            try
            {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();

                // Validate OTP and get reg data
                string? regData = null;
                DateTime expires;
                using (var cmd = new OracleCommand(@"SELECT EXPIRES_AT, REG_DATA FROM EMAIL_OTP_CODES WHERE USERNAME = :u AND OTP_CODE = :c", conn)
                { BindByName = true })
                {
                    cmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = request.Username;
                    cmd.Parameters.Add(":c", OracleDbType.Varchar2).Value = request.Otp;
                    using var rdr = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
                    if (!await rdr.ReadAsync())
                        return new OtpResponse { Success = false, Message = "OTP không hợp lệ." };
                    expires = rdr.GetDateTime(0);
                    regData = rdr.IsDBNull(1) ? null : rdr.GetString(1);
                }
                if (expires < DateTime.UtcNow)
                    return new OtpResponse { Success = false, Message = "OTP đã hết hạn." };

                // If account not exists, create via SP using regData
                int exists;
                using (var chk = new OracleCommand("SELECT COUNT(*) FROM TAI_KHOAN WHERE TEN_DANG_NHAP = :u", conn) { BindByName = true })
                {
                    chk.Parameters.Add(":u", OracleDbType.Varchar2).Value = request.Username;
                    exists = Convert.ToInt32(await chk.ExecuteScalarAsync());
                }

                if (exists == 0)
                {
                    if (string.IsNullOrWhiteSpace(regData))
                        return new OtpResponse { Success = false, Message = "Thiếu dữ liệu đăng ký." };

                    var reg = JsonSerializer.Deserialize<RegisterRequest>(regData!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

                    using (var cmd = new OracleCommand("SP_DANG_KY_HOC_VIEN", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.BindByName = true;
                        cmd.CommandTimeout = 30;

                        cmd.Parameters.Add("p_ten_dang_nhap", OracleDbType.Varchar2).Value = reg.Username;
                        cmd.Parameters.Add("p_mat_khau", OracleDbType.Varchar2).Value = reg.Password;
                        cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = reg.Email;
                        cmd.Parameters.Add("p_ho_ten", OracleDbType.NVarchar2).Value = reg.FullName;
                        cmd.Parameters.Add("p_gioi_tinh", OracleDbType.NVarchar2).Value = reg.Sex;
                        cmd.Parameters.Add("p_ngay_sinh", OracleDbType.Date).Value = (object?)reg.DateOfBirth ?? DBNull.Value;
                        cmd.Parameters.Add("p_sdt", OracleDbType.Varchar2).Value = reg.PhoneNumber;
                        cmd.Parameters.Add("p_dia_chi", OracleDbType.NVarchar2).Value = (object?)reg.Address ?? DBNull.Value;
                        var outMsg = new OracleParameter("p_ket_qua", OracleDbType.NVarchar2, 4000) { Direction = ParameterDirection.Output };
                        cmd.Parameters.Add(outMsg);

                        await cmd.ExecuteNonQueryAsync();
                        var resultMsg = outMsg.Value?.ToString() ?? string.Empty;
                        if (!resultMsg.Contains("thành công", StringComparison.OrdinalIgnoreCase))
                        {
                            return new OtpResponse { Success = false, Message = resultMsg };
                        }
                    }
                }

                // Activate account
                using (var up = new OracleCommand("UPDATE TAI_KHOAN SET TRANG_THAI_KICH_HOAT = 1 WHERE TEN_DANG_NHAP = :u", conn) { BindByName = true })
                {
                    up.Parameters.Add(":u", OracleDbType.Varchar2).Value = request.Username;
                    await up.ExecuteNonQueryAsync();
                }

                // Clean used OTP row
                using (var del = new OracleCommand("DELETE FROM EMAIL_OTP_CODES WHERE USERNAME = :u", conn) { BindByName = true })
                {
                    del.Parameters.Add(":u", OracleDbType.Varchar2).Value = request.Username;
                    await del.ExecuteNonQueryAsync();
                }

                return new OtpResponse { Success = true, Message = "Xác thực OTP thành công. Tài khoản đã được kích hoạt." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyOtpAsync error");
                return new OtpResponse { Success = false, Message = "Lỗi xác thực: " + ex.Message };
            }
        }

        public async Task<OtpResponse> ResendOtpAsync(string username)
        {
            try
            {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                await EnsureOtpTableAsync(conn);

                // Try to get email from existing account first
                string? email = null; string displayName = username;
                using (var q1 = new OracleCommand(@"SELECT tk.EMAIL, NVL(hv.HO_TEN, TO_NCHAR(tk.TEN_DANG_NHAP)) FROM TAI_KHOAN tk
                                                    LEFT JOIN HOC_VIEN hv ON hv.ID_HOC_VIEN = tk.ID_NGUOI_DUNG
                                                   WHERE tk.TEN_DANG_NHAP = :u", conn) { BindByName = true })
                {
                    q1.Parameters.Add(":u", OracleDbType.Varchar2).Value = username;
                    using var rdr = await q1.ExecuteReaderAsync(CommandBehavior.SingleRow);
                    if (await rdr.ReadAsync())
                    {
                        email = rdr.IsDBNull(0) ? null : rdr.GetString(0);
                        displayName = rdr.IsDBNull(1) ? username : rdr.GetString(1);
                    }
                }

                // If account not exists, get email from pending reg data
                string? regJson = null;
                if (string.IsNullOrWhiteSpace(email))
                {
                    using var q2 = new OracleCommand("SELECT REG_DATA FROM EMAIL_OTP_CODES WHERE USERNAME = :u FETCH FIRST 1 ROWS ONLY", conn) { BindByName = true };
                    q2.Parameters.Add(":u", OracleDbType.Varchar2).Value = username;
                    regJson = (await q2.ExecuteScalarAsync())?.ToString();
                    if (!string.IsNullOrWhiteSpace(regJson))
                    {
                        var reg = JsonSerializer.Deserialize<RegisterRequest>(regJson!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        email = reg?.Email;
                        displayName = reg?.FullName ?? username;
                    }
                }

                if (string.IsNullOrWhiteSpace(email))
                    return new OtpResponse { Success = false, Message = "Không tìm thấy email để gửi OTP." };

                // Generate and upsert OTP (preserve REG_DATA read above)
                var otp = new Random().Next(100000, 999999).ToString();
                using (var del = new OracleCommand("DELETE FROM EMAIL_OTP_CODES WHERE USERNAME = :u", conn) { BindByName = true })
                {
                    del.Parameters.Add(":u", OracleDbType.NVarchar2).Value = username;
                    await del.ExecuteNonQueryAsync();
                }
                using (var ins = new OracleCommand("INSERT INTO EMAIL_OTP_CODES (USERNAME, OTP_CODE, EXPIRES_AT, REG_DATA) VALUES (:u, :c, :exp, :data)", conn) { BindByName = true })
                {
                    ins.Parameters.Add(":u", OracleDbType.NVarchar2).Value = username;
                    ins.Parameters.Add(":c", OracleDbType.Varchar2).Value = otp;
                    ins.Parameters.Add(":exp", OracleDbType.Date).Value = DateTime.UtcNow.AddMinutes(10);
                    ins.Parameters.Add(":data", OracleDbType.Clob).Value = (object?)regJson ?? DBNull.Value;
                    await ins.ExecuteNonQueryAsync();
                }

                var model = new Dictionary<string, string>
                {
                    ["FullName"] = displayName,
                    ["Username"] = username,
                    ["Otp"] = otp,
                    ["AppName"] = _config["Email:FromName"] ?? "QLTTTA"
                };
                var html = await _tmpl.RenderAsync("Otp", model);
                if (string.IsNullOrWhiteSpace(html))
                {
                    html = $@"<p>Xin chào {System.Net.WebUtility.HtmlEncode(displayName)},</p>
                              <p>Mã OTP của bạn là:</p>
                              <h2 style='letter-spacing:4px'>{otp}</h2>
                              <p>Có hiệu lực trong 10 phút.</p>";
                }
                await _emailService.SendAsync(email!, "Mã OTP xác thực tài khoản QLTTTA", html);

                return new OtpResponse { Success = true, Message = "Đã gửi lại OTP đến email của bạn." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ResendOtpAsync error");
                return new OtpResponse { Success = false, Message = "Không thể gửi lại OTP: " + ex.Message };
            }
        }

        public async Task<string> TestDatabaseAsync()
        {
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new OracleCommand("SELECT 1 FROM DUAL", conn);
            _ = await cmd.ExecuteScalarAsync();
            return "Database connected";
        }

        public Task<bool> CheckSessionAsync(string username, string sessionId) => Task.FromResult(true);
        public Task LogoutAsync(string sessionId) => Task.CompletedTask;

        // Legacy email link verification retained for compatibility (no-op)
        public Task<(bool Success, string Message)> VerifyEmailAsync(string username, string token)
            => Task.FromResult((false, "Email verify link is not used. Vui lòng dùng OTP."));

        public async Task<OtpResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            try
            {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                await EnsureOtpTableAsync(conn);

                var email = (request.Email ?? string.Empty).Trim();
                var username = (request.Username ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(username))
                    return new OtpResponse { Success = false, Message = "Thiếu thông tin yêu cầu." };

                string? dbUsername = null; int active = 0; string displayName = username;
                using (var cmd = new OracleCommand(@"SELECT tk.TEN_DANG_NHAP, NVL(tk.TRANG_THAI_KICH_HOAT,0), NVL(hv.HO_TEN, TO_NCHAR(tk.TEN_DANG_NHAP))
                                                     FROM TAI_KHOAN tk
                                                     LEFT JOIN HOC_VIEN hv ON hv.ID_HOC_VIEN = tk.ID_NGUOI_DUNG
                                                     WHERE tk.TEN_DANG_NHAP = :u AND UPPER(TRIM(tk.EMAIL)) = UPPER(TRIM(:e))", conn) { BindByName = true })
                {
                    cmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = username;
                    cmd.Parameters.Add(":e", OracleDbType.Varchar2).Value = email;
                    using var rdr = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
                    if (await rdr.ReadAsync())
                    {
                        dbUsername = rdr.IsDBNull(0) ? null : rdr.GetString(0);
                        active = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                        displayName = rdr.IsDBNull(2) ? username : rdr.GetString(2);
                    }
                }

                if (string.IsNullOrWhiteSpace(dbUsername))
                {
                    return new OtpResponse { Success = false, Message = "Email chưa được đăng ký." };
                }
                if (active != 1)
                {
                    return new OtpResponse { Success = false, Message = "Tài khoản chưa kích hoạt. Vui lòng đăng ký/xác thực trước." };
                }

                // Generate OTP for reset
                var otp = new Random().Next(100000, 999999).ToString();

                // Clear previous OTPs for this username
                using (var del = new OracleCommand("DELETE FROM EMAIL_OTP_CODES WHERE USERNAME = :u", conn) { BindByName = true })
                {
                    del.Parameters.Add(":u", OracleDbType.NVarchar2).Value = username;
                    await del.ExecuteNonQueryAsync();
                }

                // Store NULL REG_DATA (no password captured at this step)
                using (var ins = new OracleCommand("INSERT INTO EMAIL_OTP_CODES (USERNAME, OTP_CODE, EXPIRES_AT, REG_DATA) VALUES (:u, :c, :exp, NULL)", conn) { BindByName = true })
                {
                    ins.Parameters.Add(":u", OracleDbType.NVarchar2).Value = username;
                    ins.Parameters.Add(":c", OracleDbType.Varchar2).Value = otp;
                    ins.Parameters.Add(":exp", OracleDbType.Date).Value = DateTime.UtcNow.AddMinutes(10);
                    await ins.ExecuteNonQueryAsync();
                }

                var model = new Dictionary<string, string>
                {
                    ["FullName"] = displayName,
                    ["Username"] = username,
                    ["Otp"] = otp,
                    ["AppName"] = _config["Email:FromName"] ?? "QLTTTA"
                };
                var html = await _tmpl.RenderAsync("Otp", model);
                if (string.IsNullOrWhiteSpace(html))
                {
                    html = $@"<p>Xin chào {System.Net.WebUtility.HtmlEncode(displayName)},</p>
                              <p>Mã OTP đặt lại mật khẩu của bạn là:</p>
                              <h2 style='letter-spacing:4px'>{otp}</h2>
                              <p>Có hiệu lực trong 10 phút.</p>";
                }

                await _emailService.SendAsync(email, "Mã OTP đặt lại mật khẩu QLTTTA", html);
                return new OtpResponse { Success = true, Message = "Đã gửi OTP đặt lại mật khẩu đến email của bạn." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ForgotPasswordAsync error");
                return new OtpResponse { Success = false, Message = "Không thể xử lý yêu cầu: " + ex.Message };
            }
        }

        public async Task<OtpResponse> ResetPasswordAsync(ResetPasswordRequest request)
        {
            try
            {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();

                // Validate OTP and expiry and fetch REG_DATA
                DateTime expires; string? regData = null;
                using (var cmd = new OracleCommand(@"SELECT EXPIRES_AT, REG_DATA FROM EMAIL_OTP_CODES WHERE USERNAME = :u AND OTP_CODE = :c", conn) { BindByName = true })
                {
                    cmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = request.Username;
                    cmd.Parameters.Add(":c", OracleDbType.Varchar2).Value = request.Otp;
                    using var rdr = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
                    if (!await rdr.ReadAsync())
                        return new OtpResponse { Success = false, Message = "OTP không hợp lệ." };
                    expires = rdr.GetDateTime(0);
                    regData = rdr.IsDBNull(1) ? null : rdr.GetString(1);
                }
                if (expires < DateTime.UtcNow)
                    return new OtpResponse { Success = false, Message = "OTP đã hết hạn." };

                var newPassword = request.NewPassword;
                if (string.IsNullOrWhiteSpace(newPassword) && !string.IsNullOrWhiteSpace(regData))
                {
                    try
                    {
                        var json = JsonSerializer.Deserialize<Dictionary<string, string>>(regData);
                        if (json != null && json.TryGetValue("NewPassword", out var np))
                            newPassword = np;
                    }
                    catch { }
                }
                if (string.IsNullOrWhiteSpace(newPassword))
                    return new OtpResponse { Success = false, Message = "Thiếu mật khẩu mới." };

                // Update password
                using (var up = new OracleCommand("UPDATE TAI_KHOAN SET MAT_KHAU = :p WHERE TEN_DANG_NHAP = :u", conn) { BindByName = true })
                {
                    up.Parameters.Add(":p", OracleDbType.Varchar2).Value = newPassword;
                    up.Parameters.Add(":u", OracleDbType.Varchar2).Value = request.Username;
                    var rows = await up.ExecuteNonQueryAsync();
                    if (rows == 0)
                        return new OtpResponse { Success = false, Message = "Không tìm thấy tài khoản." };
                }

                // Clean OTP
                using (var del = new OracleCommand("DELETE FROM EMAIL_OTP_CODES WHERE USERNAME = :u", conn) { BindByName = true })
                {
                    del.Parameters.Add(":u", OracleDbType.Varchar2).Value = request.Username;
                    await del.ExecuteNonQueryAsync();
                }

                return new OtpResponse { Success = true, Message = "Đặt lại mật khẩu thành công." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ResetPasswordAsync error");
                return new OtpResponse { Success = false, Message = "Không thể đặt lại mật khẩu: " + ex.Message };
            }
        }

        private async Task EnsureOtpTableAsync(OracleConnection conn)
        {
            // Create table if not exists
            var exists = Convert.ToInt32(await new OracleCommand("SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME = 'EMAIL_OTP_CODES'", conn).ExecuteScalarAsync()) > 0;
            if (!exists)
            {
                var ddl = @"CREATE TABLE EMAIL_OTP_CODES (
                                USERNAME NVARCHAR2(50) NOT NULL,
                                OTP_CODE VARCHAR2(6) NOT NULL,
                                EXPIRES_AT DATE NOT NULL,
                                REG_DATA CLOB NULL,
                                CONSTRAINT PK_EMAIL_OTP PRIMARY KEY (USERNAME, OTP_CODE)
                             )";
                using var create = new OracleCommand(ddl, conn);
                await create.ExecuteNonQueryAsync();
                return;
            }

            // Ensure REG_DATA column exists
            var colExists = Convert.ToInt32(await new OracleCommand("SELECT COUNT(*) FROM USER_TAB_COLUMNS WHERE TABLE_NAME='EMAIL_OTP_CODES' AND COLUMN_NAME='REG_DATA'", conn).ExecuteScalarAsync()) > 0;
            if (!colExists)
            {
                try
                {
                    using var alter = new OracleCommand("ALTER TABLE EMAIL_OTP_CODES ADD (REG_DATA CLOB NULL)", conn);
                    await alter.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add REG_DATA column (may already exist) ");
                }
            }
        }
    }
}
