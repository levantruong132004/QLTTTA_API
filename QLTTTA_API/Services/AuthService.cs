using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using System.Data;

namespace QLTTTA_API.Services
{
    /// <summary>
    /// Interface khai báo các nghiệp vụ xác thực & phiên làm việc người dùng.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Xác thực đăng nhập bằng tài khoản học viên (Oracle USER). Trả về LoginResponse chứa SessionId và thông tin user nếu thành công.
        /// </summary>
        Task<LoginResponse> AuthenticateAsync(LoginRequest request);
        /// <summary>
        /// Đăng ký học viên mới thông qua Stored Procedure (SP_DANG_KY_HOC_VIEN).
        /// </summary>
        Task<RegisterResponse> RegisterAsync(RegisterRequest request);
        /// <summary>
        /// Kiểm tra cơ bản kết nối DB và cấu trúc bảng ACCOUNTS (demo / kiểm thử).
        /// </summary>
        Task<string> TestDatabaseAsync();
        /// <summary>
        /// Kiểm tra phiên hiện tại còn hợp lệ không theo loại thiết bị (pc/mobile).
        /// Kiểm tra theo cột SESSION_ID_PC hoặc SESSION_ID_MOBILE tùy deviceType.
        /// </summary>
        Task<bool> CheckSessionAsync(string username, string sessionId, string? deviceType = null);
        /// <summary>
        /// Đăng xuất: xóa credential cache theo SessionId (không xóa SESSION_ID_PC/SESSION_ID_MOBILE trong DB theo yêu cầu).
        /// </summary>
        Task LogoutAsync(string username, string sessionId);

        // New: OTP registration + forgot password
        Task<OtpInitiateResponse> InitiateRegisterOtpAsync(RegisterRequest request);
        Task<RegisterResponse> VerifyRegisterOtpAsync(OtpVerifyRequest request);
        Task<ForgotPasswordInitiateResponse> InitiateForgotPasswordAsync(ForgotPasswordInitiateRequest request);
        Task<BasicResponse> VerifyForgotPasswordAsync(ForgotPasswordVerifyRequest request);
    }

    /// <summary>
    /// Triển khai nghiệp vụ xác thực người dùng dựa trên tài khoản Oracle thực (per-user connection) và quản lý SessionId.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly string _connectionString; // Chuỗi kết nối admin gốc, lấy DataSource phục vụ mở kết nối user.
        private readonly ILogger<AuthService> _logger;
        private readonly IUserCredentialCache _credCache; // Cache tạm giữ username/password theo SessionId.
        private readonly IOtpStore _otpStore;
        private readonly IEmailService _emailService;
        private readonly bool _useSpRegister;

        /// <summary>
        /// Khởi tạo service với cấu hình DB, logger và cache phiên.
        /// </summary>
        public AuthService(IConfiguration configuration, ILogger<AuthService> logger, IUserCredentialCache credCache, IOtpStore otpStore, IEmailService emailService)
        {
            _connectionString = configuration.GetConnectionString("OracleDbConnection") ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger;
            _credCache = credCache;
            _otpStore = otpStore;
            _emailService = emailService;
            // Allow bypassing stored procedure for registration if environment has issues
            var useSp = configuration["Registration:UseStoredProcedure"];
            _useSpRegister = string.IsNullOrWhiteSpace(useSp) ? false : useSp.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Xác thực đăng nhập: kiểm tra tồn tại Oracle USER, thử mở kết nối bằng credential học viên;
        /// sau đó cập nhật SESSION_ID theo loại thiết bị (SESSION_ID_PC/SESSION_ID_MOBILE) với khóa hàng & retry, và lưu credential vào cache.
        /// </summary>
        public async Task<LoginResponse> AuthenticateAsync(LoginRequest request)
        {
            try
            {
                var deviceType = (request.DeviceType ?? "pc").Trim().ToLowerInvariant();
                if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
                // 1) Thử kết nối bằng tài khoản người dùng để xác thực username/password thật
                _logger.LogInformation("Login attempt - Username: {Username}", request.Username);
                // Kiểm tra USER Oracle có tồn tại không (tránh trường hợp chỉ tạo dòng trong TAI_KHOAN)
                try
                {
                    using var adminCheckConn = new OracleConnection(_connectionString);
                    await adminCheckConn.OpenAsync();
                    using var existCmd = new OracleCommand("SELECT COUNT(*) FROM ALL_USERS WHERE USERNAME = :u", adminCheckConn)
                    { BindByName = true };
                    existCmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = (request.Username ?? string.Empty).Trim().ToUpperInvariant();
                    var cntObj = await existCmd.ExecuteScalarAsync();
                    var cnt = Convert.ToInt32(cntObj ?? 0);
                    if (cnt == 0)
                    {
                        // Trước đây trả lỗi luôn. Giờ chỉ cảnh báo và tiếp tục thử credential để an toàn hơn.
                        _logger.LogWarning("Oracle USER not found in ALL_USERS for {Username}, will still try credential login.", request.Username);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not verify ALL_USERS, continue with credential test");
                    // Không chặn, tiếp tục xác thực bằng credential
                }
                // Phân tích chuỗi kết nối admin để lấy DataSource
                var baseCs = new OracleConnectionStringBuilder(_connectionString);
                var userCs = new OracleConnectionStringBuilder
                {
                    DataSource = baseCs.DataSource,
                    UserID = request.Username?.Trim(),
                    Password = request.Password
                };
                try
                {
                    using var userConn = new OracleConnection(userCs.ConnectionString); // Mở kết nối bằng Oracle USER học viên
                    await userConn.OpenAsync(); // Sai mật khẩu / user không tồn tại => ném exception
                }
                catch (OracleException oex) when (oex.Number == 28000)
                {
                    // ORA-28000: the account is locked (do profile hoặc ADMIN khóa sau nhiều lần sai mật khẩu)
                    _logger.LogWarning(oex, "Account locked (ORA-28000) for {Username}", request.Username);
                    return new LoginResponse { Success = false, Message = "Tài khoản đã bị khóa do nhập sai mật khẩu quá số lần cho phép. Vui lòng liên hệ quản trị để mở khóa." };
                }
                catch (OracleException oex) when (oex.Number == 28001)
                {
                    // ORA-28001: the password has expired
                    _logger.LogWarning(oex, "Password expired (ORA-28001) for {Username}", request.Username);
                    return new LoginResponse { Success = false, Message = "Mật khẩu đã hết hạn. Vui lòng liên hệ quản trị để đặt lại." };
                }
                catch (OracleException oex) when (oex.Number == 1017)
                {
                    // ORA-01017: invalid username/password; logon denied
                    _logger.LogWarning(oex, "Invalid credentials (ORA-01017) for {Username}", request.Username);
                    return new LoginResponse { Success = false, Message = "Sai tên đăng nhập hoặc mật khẩu" };
                }
                catch (OracleException oex)
                {
                    // Các lỗi Oracle khác khi mở kết nối người dùng
                    _logger.LogWarning(oex, "Oracle error {Code} during user credential test for {Username}", oex.Number, request.Username);
                    return new LoginResponse { Success = false, Message = $"Không thể đăng nhập: Lỗi Oracle {oex.Number}" };
                }
                catch (Exception credEx)
                {
                    _logger.LogWarning(credEx, "User credential connection failed for {Username}", request.Username);
                    return new LoginResponse { Success = false, Message = "Không thể kết nối. Vui lòng thử lại." };
                }

                // 2) Dùng kết nối quản trị để lấy thông tin và cập nhật SESSION_ID_{PC|MOBILE}, kiểm tra TRANG_THAI_KICH_HOAT
                // Kết nối admin để truy vấn bảng TAI_KHOAN & cập nhật session theo loại thiết bị
                using var adminConn = new OracleConnection(_connectionString);
                await adminConn.OpenAsync();

                // Kiểm tra kích hoạt và lấy thông tin người dùng từ schema tiếng Việt (không phân biệt hoa/thường)
                var infoSql = @"SELECT tk.ID_NGUOI_DUNG,
                                         tk.TEN_DANG_NHAP,
                                         tk.EMAIL,
                                         tk.TRANG_THAI_KICH_HOAT,
                                 vt.TEN_VAI_TRO,
                                 vt.ID_VAI_TRO,
                                         hv.HO_TEN
                                    FROM TAI_KHOAN tk
                               LEFT JOIN VAI_TRO vt ON vt.ID_VAI_TRO = tk.ID_VAI_TRO
                               LEFT JOIN HOC_VIEN hv ON hv.ID_HOC_VIEN = tk.ID_NGUOI_DUNG
                                   WHERE UPPER(tk.TEN_DANG_NHAP) = UPPER(:u)";
                using var infoCmd = new OracleCommand(infoSql, adminConn) { BindByName = true };
                infoCmd.CommandTimeout = 10;
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
                int ordRoleId = rdr.GetOrdinal("ID_VAI_TRO");
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
                    RoleId = rdr.IsDBNull(ordRoleId) ? 0 : rdr.GetInt32(ordRoleId),
                    Role = rdr.IsDBNull(ordRole) ? string.Empty : rdr.GetString(ordRole),
                    FullName = (ordFull >= 0 && !rdr.IsDBNull(ordFull)) ? rdr.GetString(ordFull) : string.Empty
                };

                // Tạo và lưu Session ID mới (ngăn đăng nhập đồng thời theo từng loại thiết bị) trong transaction để tránh race
                var sessionId = Guid.NewGuid().ToString("N"); // SessionId duy nhất dùng làm khóa tra cache & kiểm tra phiên
                var maxAttempts = 3;
                var attempt = 0;
                bool updated = false;
                while (attempt < maxAttempts && !updated)
                {
                    attempt++;
                    using var tx = adminConn.BeginTransaction();
                    // Khóa dòng để kiểm tra/ghi phiên theo loại thiết bị
                    using var lockCmd = new OracleCommand("SELECT SESSION_ID_PC, SESSION_ID_MOBILE FROM TAI_KHOAN WHERE ID_NGUOI_DUNG = :id FOR UPDATE WAIT 1", adminConn) { BindByName = true };
                    lockCmd.Parameters.Add(":id", OracleDbType.Int32).Value = userInfo.UserId;

                    lockCmd.Transaction = tx;
                    lockCmd.CommandTimeout = 3;
                    try
                    {
                        // Đọc phiên cũ nếu cần dùng cho log (không chặn đăng nhập mới)
                        using (var rdr2 = await lockCmd.ExecuteReaderAsync(CommandBehavior.SingleRow))
                        {
                            if (await rdr2.ReadAsync())
                            {
                                var existingPc = rdr2.IsDBNull(0) ? null : rdr2.GetString(0);
                                var existingMobile = rdr2.IsDBNull(1) ? null : rdr2.GetString(1);
                                if (deviceType == "pc" && !string.IsNullOrEmpty(existingPc))
                                {
                                    _logger.LogInformation("Replacing existing PC session for userId={UserId}", userInfo.UserId);
                                }
                                if (deviceType == "mobile" && !string.IsNullOrEmpty(existingMobile))
                                {
                                    _logger.LogInformation("Replacing existing Mobile session for userId={UserId}", userInfo.UserId);
                                }
                            }
                        }
                    }
                    catch (OracleException oex) when (oex.Number == 54 || oex.Number == 30006)
                    {
                        await tx.RollbackAsync(); // Row đang bị khóa bởi session khác
                        _logger.LogWarning(oex, "Row locked (attempt {Attempt}/{Max}) when setting session for userId={UserId}", attempt, maxAttempts, userInfo.UserId);
                        if (attempt >= maxAttempts)
                        {
                            return new LoginResponse { Success = false, Message = "Tài khoản đang được cập nhật. Vui lòng thử lại sau ít phút." };
                        }
                        await Task.Delay(300); // backoff nhẹ trước khi thử lại
                        continue;
                    }

                    // Ghi session theo loại thiết bị
                    var sqlUpdate = deviceType == "mobile"
                        ? "UPDATE TAI_KHOAN SET SESSION_ID_MOBILE = :sid WHERE ID_NGUOI_DUNG = :id"
                        : "UPDATE TAI_KHOAN SET SESSION_ID_PC = :sid WHERE ID_NGUOI_DUNG = :id";
                    using var upCmd = new OracleCommand(sqlUpdate, adminConn) { Transaction = tx, BindByName = true, CommandTimeout = 3 };
                    upCmd.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sessionId;
                    upCmd.Parameters.Add(":id", OracleDbType.Int32).Value = userInfo.UserId;
                    var affected = await upCmd.ExecuteNonQueryAsync();
                    if (affected == 1)
                    {
                        await tx.CommitAsync();
                        updated = true;
                    }
                    else
                    {
                        await tx.RollbackAsync();
                        _logger.LogError("Unexpected rows affected when setting session: {Affected} for user {User}", affected, userInfo.Username);
                        return new LoginResponse { Success = false, Message = "Không thể tạo phiên đăng nhập (lỗi cập nhật phiên)" };
                    }
                }

                // Lưu thông tin chứng thực tạm thời để các request tiếp theo mở kết nối theo user
                _credCache.Set(sessionId, request.Username!.Trim(), request.Password!, TimeSpan.FromHours(1)); // Lưu credential vào cache

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

        /// <summary>
        /// Sinh token đơn giản (demo). Có thể thay bằng JWT thật nếu cần.
        /// </summary>
        private string GenerateToken(UserInfo user)
        {
            // Tạo JWT token đơn giản hoặc session token
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{user.UserId}:{user.Username}:{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"));
        }

        /// <summary>
        /// Đăng ký học viên mới bằng Stored Procedure SP_DANG_KY_HOC_VIEN.
        /// Sau khi SP chạy, truy vấn lại thông tin người dùng vừa tạo và TẠO ORACLE USER.
        /// </summary>
        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("Bắt đầu đăng ký user: {Username} (UseSP={UseSp})", request.Username, _useSpRegister);
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();
                bool spSucceeded = false;
                if (_useSpRegister)
                {
                    try
                    {
                        using var cmd = new OracleCommand("SP_DANG_KY_HOC_VIEN", connection);
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
                        if (resultMsg.Contains("thành công", StringComparison.OrdinalIgnoreCase))
                        {
                            spSucceeded = true;
                        }
                        else
                        {
                            // SP chạy nhưng trả thông báo lỗi nghiệp vụ
                            return new RegisterResponse { Success = false, Message = resultMsg };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "SP_DANG_KY_HOC_VIEN failed, falling back to inline registration for {Username}", request.Username);
                        var inlineRes = await RegisterInlineFallbackAsync(connection, request);
                        if (!inlineRes.Success) return inlineRes;
                        spSucceeded = true;
                    }
                }
                else
                {
                    // Direct inline path (recommended for dev): skip SP entirely
                    var inlineRes = await RegisterInlineFallbackAsync(connection, request);
                    if (!inlineRes.Success) return inlineRes;
                    spSucceeded = true;
                }

                // TẠO ORACLE USER sau khi đăng ký thành công
                try
                {
                    var created = await CreateOracleUserAsync(connection, request.Username, request.Password);
                    if (!created)
                    {
                        _logger.LogWarning("Could not create Oracle USER for {Username}, user may already exist", request.Username);
                    }
                    else
                    {
                        _logger.LogInformation("Successfully created Oracle USER for {Username}", request.Username);
                    }
                }
                catch (Exception userEx)
                {
                    _logger.LogError(userEx, "Error creating Oracle USER for {Username}", request.Username);
                    // Không chặn đăng ký, chỉ cảnh báo
                }

                // Lấy lại thông tin user vừa tạo
                using (var infoCmd = new OracleCommand(@"SELECT tk.ID_NGUOI_DUNG, tk.TEN_DANG_NHAP, tk.EMAIL, vt.TEN_VAI_TRO, vt.ID_VAI_TRO, hv.HO_TEN
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
                            RoleId = rdr.IsDBNull(rdr.GetOrdinal("ID_VAI_TRO")) ? 0 : rdr.GetInt32(rdr.GetOrdinal("ID_VAI_TRO")),
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

        private async Task<RegisterResponse> RegisterInlineFallbackAsync(OracleConnection connection, RegisterRequest request)
        {
            // Ensure CLIENT_IDENTIFIER is set for this session (helps VPD/policies relying on it)
            try
            {
                using var setId = new OracleCommand("BEGIN DBMS_SESSION.SET_IDENTIFIER(:u); END;", connection) { BindByName = true };
                setId.Parameters.Add(":u", OracleDbType.Varchar2).Value = request.Username?.Trim();
                await setId.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not set CLIENT_IDENTIFIER for registration session of {Username}", request.Username);
            }

            // Duplicate checks
            using (var dup = new OracleCommand("SELECT (SELECT COUNT(*) FROM TAI_KHOAN WHERE UPPER(TEN_DANG_NHAP)=UPPER(:u)) C1, (SELECT COUNT(*) FROM TAI_KHOAN WHERE UPPER(EMAIL)=UPPER(:e)) C2 FROM DUAL", connection) { BindByName = true })
            {
                dup.Parameters.Add(":u", OracleDbType.Varchar2).Value = request.Username;
                dup.Parameters.Add(":e", OracleDbType.Varchar2).Value = request.Email;
                using var rdr = await dup.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (await rdr.ReadAsync())
                {
                    var c1 = Convert.ToInt32(rdr[0]);
                    var c2 = Convert.ToInt32(rdr[1]);
                    if (c1 > 0) return new RegisterResponse { Success = false, Message = "Tên đăng nhập đã tồn tại" };
                    if (c2 > 0) return new RegisterResponse { Success = false, Message = "Email đã được sử dụng" };
                }
            }

            // Resolve role id for HocVien
            int roleId = 0;
            using (var roleCmd = new OracleCommand("SELECT ID_VAI_TRO FROM VAI_TRO WHERE TEN_VAI_TRO = 'HocVien'", connection))
            {
                var obj = await roleCmd.ExecuteScalarAsync();
                roleId = Convert.ToInt32(obj ?? 0);
                if (roleId == 0) return new RegisterResponse { Success = false, Message = "Không tìm thấy vai trò HocVien" };
            }

            using var tx = connection.BeginTransaction();
            try
            {
                // Insert TAI_KHOAN
                var hashed = HashPasswordSha256(request.Password);
                int newUserId = 0;
                try
                {
                    using var ins = new OracleCommand("INSERT INTO TAI_KHOAN (TEN_DANG_NHAP, MAT_KHAU, EMAIL, ID_VAI_TRO, TRANG_THAI_KICH_HOAT) VALUES (:u,:p,:e,:r,1) RETURNING ID_NGUOI_DUNG INTO :id", connection) { BindByName = true, Transaction = tx };
                    ins.Parameters.Add(":u", OracleDbType.Varchar2).Value = request.Username;
                    ins.Parameters.Add(":p", OracleDbType.Varchar2).Value = hashed;
                    ins.Parameters.Add(":e", OracleDbType.Varchar2).Value = request.Email;
                    ins.Parameters.Add(":r", OracleDbType.Int32).Value = roleId;
                    var idParam = new OracleParameter(":id", OracleDbType.Int32) { Direction = ParameterDirection.Output };
                    ins.Parameters.Add(idParam);
                    await ins.ExecuteNonQueryAsync();
                    newUserId = Convert.ToInt32(idParam.Value?.ToString() ?? "0");
                    if (newUserId <= 0) throw new Exception("Không nhận được ID người dùng mới");
                }
                catch (OracleException oex)
                {
                    await tx.RollbackAsync();
                    _logger.LogError(oex, "Insert into TAI_KHOAN failed for {Username} (ORA-{Code})", request.Username, oex.Number);
                    return new RegisterResponse { Success = false, Message = $"Lỗi khi tạo bản ghi TAI_KHOAN (ORA-{oex.Number})" };
                }

                // Insert HOC_VIEN
                try
                {
                    using var insHv = new OracleCommand("INSERT INTO HOC_VIEN (ID_HOC_VIEN, HO_TEN, MA_HOC_VIEN, GIOI_TINH, NGAY_SINH, SO_DIEN_THOAI, DIA_CHI) VALUES (:id,:hoten,NULL,:sex,:dob,:phone,:addr)", connection) { BindByName = true, Transaction = tx };
                    insHv.Parameters.Add(":id", OracleDbType.Int32).Value = newUserId;
                    insHv.Parameters.Add(":hoten", OracleDbType.NVarchar2).Value = request.FullName;
                    insHv.Parameters.Add(":sex", OracleDbType.NVarchar2).Value = request.Sex;
                    insHv.Parameters.Add(":dob", OracleDbType.Date).Value = (object?)request.DateOfBirth ?? DBNull.Value;
                    insHv.Parameters.Add(":phone", OracleDbType.Varchar2).Value = request.PhoneNumber;
                    insHv.Parameters.Add(":addr", OracleDbType.NVarchar2).Value = (object?)request.Address ?? DBNull.Value;
                    await insHv.ExecuteNonQueryAsync();
                }
                catch (OracleException oex)
                {
                    await tx.RollbackAsync();
                    _logger.LogError(oex, "Insert into HOC_VIEN failed for {Username} (ORA-{Code})", request.Username, oex.Number);
                    // Deep diagnostic: collect triggers & source lines referencing SYS_CONTEXT on HOC_VIEN
                    try
                    {
                        var triggers = new List<string>();
                        using (var trgCmd = new OracleCommand("SELECT TRIGGER_NAME FROM USER_TRIGGERS WHERE TABLE_NAME = 'HOC_VIEN'", connection))
                        using (var trgRdr = await trgCmd.ExecuteReaderAsync())
                        {
                            while (await trgRdr.ReadAsync()) triggers.Add(trgRdr.GetString(0));
                        }
                        if (triggers.Count == 0)
                        {
                            _logger.LogInformation("[Diag] Không có trigger nào trên bảng HOC_VIEN.");
                        }
                        else
                        {
                            _logger.LogInformation("[Diag] Trigger trên HOC_VIEN: {Triggers}", string.Join(",", triggers));
                            foreach (var trg in triggers)
                            {
                                using var srcCmd = new OracleCommand("SELECT LINE, TEXT FROM USER_SOURCE WHERE NAME = :n AND TYPE='TRIGGER' ORDER BY LINE", connection) { BindByName = true };
                                srcCmd.Parameters.Add(":n", OracleDbType.Varchar2).Value = trg;
                                using var srcRdr = await srcCmd.ExecuteReaderAsync();
                                var sb = new System.Text.StringBuilder();
                                while (await srcRdr.ReadAsync())
                                {
                                    var line = srcRdr.GetInt32(0);
                                    var text = srcRdr.IsDBNull(1) ? string.Empty : srcRdr.GetString(1);
                                    if (text.ToUpperInvariant().Contains("SYS_CONTEXT('USERENV"))
                                    {
                                        sb.AppendLine($"[DiagTrigger {trg}] LINE {line}: {text.Trim()}");
                                    }
                                    if (text.ToUpperInvariant().Contains("SYS_CONTEXT('ISERENV"))
                                    {
                                        sb.AppendLine($"[DiagTrigger {trg}] POSSIBLE TYPO LINE {line}: {text.Trim()}");
                                    }
                                }
                                if (sb.Length > 0)
                                {
                                    _logger.LogInformation(sb.ToString());
                                }
                            }
                        }
                        // Also check any policies that might include INSERT unexpectedly
                        using (var polCmd = new OracleCommand("SELECT POLICY_NAME, FUNCTION_SCHEMA, POLICY_FUNCTION, STATEMENT_TYPES FROM USER_POLICIES WHERE OBJECT_NAME='HOC_VIEN'", connection))
                        using (var polRdr = await polCmd.ExecuteReaderAsync())
                        {
                            while (await polRdr.ReadAsync())
                            {
                                var pName = polRdr.GetString(0);
                                var stmtTypes = polRdr.IsDBNull(3) ? string.Empty : polRdr.GetString(3);
                                _logger.LogInformation("[DiagPolicy] {Policy} STATEMENT_TYPES={Types}", pName, stmtTypes);
                            }
                        }
                    }
                    catch (Exception diagEx)
                    {
                        _logger.LogWarning(diagEx, "[Diag] Không thể thu thập thông tin trigger/policy HOC_VIEN");
                    }
                    return new RegisterResponse { Success = false, Message = $"Lỗi khi tạo bản ghi HOC_VIEN (ORA-{oex.Number})" };
                }

                await tx.CommitAsync();
                return new RegisterResponse { Success = true, Message = "Đăng ký học viên thành công" };
            }
            catch (OracleException oex)
            {
                await tx.RollbackAsync();
                _logger.LogError(oex, "Inline registration failed for {Username} (ORA-{Code})", request.Username, oex.Number);
                // Map some common Oracle errors to user-friendly messages
                string msg = oex.Number switch
                {
                    1      => "Tên đăng nhập hoặc email đã tồn tại (trùng UNIQUE)",
                    1400   => "Thiếu dữ liệu bắt buộc (không được để trống)",
                    12899  => "Giá trị quá dài so với cột (vui lòng rút gọn)",
                    2291   => "Không tìm thấy tham chiếu khoá ngoại phù hợp",
                    _      => $"Không thể tạo tài khoản (DB ORA-{oex.Number})"
                };
                return new RegisterResponse { Success = false, Message = msg };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Inline registration failed for {Username}", request.Username);
                return new RegisterResponse { Success = false, Message = $"Không thể tạo tài khoản: {ex.Message}" };
            }
        }

        private static string HashPasswordSha256(string password)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(password);
            var hash = sha.ComputeHash(bytes);
            return "SHA256:" + BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        /// <summary>
        /// Tạo Oracle USER mới với mật khẩu. Trả về true nếu thành công, false nếu user đã tồn tại hoặc lỗi khác.
        /// </summary>
        private async Task<bool> CreateOracleUserAsync(OracleConnection adminConnection, string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return false;

            // Validate password format
            if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"^[A-Za-z0-9_@#\-]{6,64}$"))
            {
                _logger.LogWarning("Password format invalid for user {Username}", username);
                throw new ArgumentException("Mật khẩu chỉ được phép chứa chữ, số, và ký tự _ @ # - , tối thiểu 6 ký tự");
            }

            var uname = username.Trim().ToUpperInvariant();

            try
            {
                // Kiểm tra USER đã tồn tại chưa
                using (var existCmd = new OracleCommand("SELECT COUNT(*) FROM ALL_USERS WHERE USERNAME = :u", adminConnection) { BindByName = true })
                {
                    existCmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = uname;
                    var cnt = Convert.ToInt32(await existCmd.ExecuteScalarAsync());
                    if (cnt > 0)
                    {
                        _logger.LogInformation("Oracle USER {Username} already exists, skipping creation", uname);
                        // Thử cập nhật mật khẩu cho user đã tồn tại
                        try
                        {
                            var sql = $"ALTER USER \"{uname}\" IDENTIFIED BY \"{password}\" ACCOUNT UNLOCK";
                            using var alterCmd = new OracleCommand(sql, adminConnection);
                            await alterCmd.ExecuteNonQueryAsync();
                            _logger.LogInformation("Updated password for existing Oracle USER {Username}", uname);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not update password for existing user {Username}", uname);
                        }
                        return true; // User đã tồn tại
                    }
                }

                // Tạo USER mới
                var createSql = $"CREATE USER \"{uname}\" IDENTIFIED BY \"{password}\" DEFAULT TABLESPACE USERS TEMPORARY TABLESPACE TEMP QUOTA UNLIMITED ON USERS";
                using (var createCmd = new OracleCommand(createSql, adminConnection))
                {
                    await createCmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("Created Oracle USER {Username}", uname);
                }

                // Cấp quyền kết nối và quyền cơ bản
                var grantSql = $"GRANT CONNECT, RESOURCE TO \"{uname}\"";
                using (var grantCmd = new OracleCommand(grantSql, adminConnection))
                {
                    await grantCmd.ExecuteNonQueryAsync();
                }

                return true;
            }
            catch (OracleException oex) when (oex.Number == 1920) // ORA-01920: user name conflicts with another user or role name
            {
                _logger.LogWarning("Oracle USER {Username} conflicts with existing user/role", uname);
                return false;
            }
            catch (OracleException oex) when (oex.Number == 1031) // ORA-01031: insufficient privileges
            {
                _logger.LogError(oex, "Insufficient privileges to CREATE USER for {Username}", uname);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Oracle USER {Username}", uname);
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra kết nối DB và sự tồn tại bảng ACCOUNTS (chỉ phục vụ test/hướng dẫn).
        /// </summary>
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

        /// <summary>
        /// Kiểm tra phiên (SessionId) của người dùng còn khớp trong DB hay không.
        /// Chỉ kiểm tra theo cột SESSION_ID_PC/SESSION_ID_MOBILE tùy deviceType.
        /// </summary>
        public async Task<bool> CheckSessionAsync(string username, string sessionId, string? deviceType = null)
        {
            try
            {
                deviceType = (deviceType ?? "pc").Trim().ToLowerInvariant();
                if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";

                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                var columnName = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                var sqlNew = $"SELECT {columnName} FROM TAI_KHOAN WHERE TEN_DANG_NHAP = :u";
                using var cmdNew = new OracleCommand(sqlNew, connection) { BindByName = true };
                cmdNew.Parameters.Add(":u", OracleDbType.Varchar2).Value = username?.Trim();
                var dbValNew = await cmdNew.ExecuteScalarAsync();
                var currentSidNew = dbValNew?.ToString();
                if (string.IsNullOrEmpty(currentSidNew)) return false;
                return string.Equals(currentSidNew, sessionId, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CheckSessionAsync error for user {Username}", username);
                return false;
            }
        }

        /// <summary>
        /// Đăng xuất: xóa credential trong cache. Không xóa SESSION_ID_HIENTAI theo yêu cầu (giữ để thiết bị cũ tự out khi đăng nhập mới).
        /// </summary>
        public Task LogoutAsync(string username, string sessionId)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(sessionId)) return Task.CompletedTask;
            try
            {
                _credCache.Remove(sessionId);
                // Theo yêu cầu mới: KHÔNG set NULL SESSION_ID_HIENTAI khi logout.
                // Cơ chế kiểm tra phiên sẽ dựa vào so khớp cookie với SESSION_ID_HIENTAI hiện tại trong DB.
                // Nếu người dùng đăng nhập nơi khác, SESSION_ID_HIENTAI sẽ được cập nhật giá trị mới và thiết bị cũ tự bị out do mismatch.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout for user {Username} session {SessionId}", username, sessionId);
            }
            return Task.CompletedTask;
        }

        public async Task<OtpInitiateResponse> InitiateRegisterOtpAsync(RegisterRequest request)
        {
            // Basic validation (email format etc. are via DataAnnotations)
            // Optionally check duplicate username/email
            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();
                var dupSql = "SELECT (SELECT COUNT(*) FROM TAI_KHOAN WHERE TEN_DANG_NHAP=:u) AS C1, (SELECT COUNT(*) FROM TAI_KHOAN WHERE EMAIL=:e) AS C2 FROM DUAL";
                using var dupCmd = new OracleCommand(dupSql, connection) { BindByName = true };
                dupCmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = request.Username;
                dupCmd.Parameters.Add(":e", OracleDbType.Varchar2).Value = request.Email;
                using var rdr = await dupCmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (await rdr.ReadAsync())
                {
                    var c1 = Convert.ToInt32(rdr[0]);
                    var c2 = Convert.ToInt32(rdr[1]);
                    if (c1 > 0)
                    {
                        return new OtpInitiateResponse { Success = false, Message = "Tên đăng nhập đã tồn tại" };
                    }
                    if (c2 > 0)
                    {
                        return new OtpInitiateResponse { Success = false, Message = "Email đã được sử dụng" };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skip duplicate check due to error");
            }

            var entry = _otpStore.Create("register", request.Email, request.Username, request, TimeSpan.FromMinutes(10));
            var subject = "Mã xác thực đăng ký (OTP) - Trung tâm LDA";
            var body = $"<p>Xin chào {System.Net.WebUtility.HtmlEncode(request.FullName)},</p>" +
                       $"<p>Mã OTP của bạn là: <strong style='font-size:18px'>{entry.OtpCode}</strong></p>" +
                       "<p>Mã có hiệu lực trong 10 phút. Không chia sẻ mã cho bất kỳ ai.</p>";
            await _emailService.SendAsync(request.Email, subject, body);

            return new OtpInitiateResponse
            {
                Success = true,
                Message = "Đã gửi mã OTP đến email của bạn",
                CorrelationId = entry.CorrelationId,
                ExpiresInSeconds = 600
            };
        }

        public async Task<RegisterResponse> VerifyRegisterOtpAsync(OtpVerifyRequest request)
        {
            if (!_otpStore.Validate(request.CorrelationId, request.Otp, out var entry) || entry == null || entry.Purpose != "register")
            {
                return new RegisterResponse { Success = false, Message = "Mã OTP không hợp lệ hoặc đã hết hạn" };
            }

            try
            {
                if (entry.Payload is RegisterRequest reg)
                {
                    var res = await RegisterAsync(reg);
                    if (res.Success)
                    {
                        _otpStore.Remove(request.CorrelationId);
                    }
                    return res;
                }
                return new RegisterResponse { Success = false, Message = "Dữ liệu đăng ký không hợp lệ" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyRegisterOtpAsync error");
                return new RegisterResponse { Success = false, Message = "Có lỗi xảy ra khi tạo tài khoản" };
            }
        }

        public async Task<ForgotPasswordInitiateResponse> InitiateForgotPasswordAsync(ForgotPasswordInitiateRequest request)
        {
            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();
                var sql = "SELECT COUNT(*) FROM TAI_KHOAN WHERE TEN_DANG_NHAP=:u AND EMAIL=:e";
                using var cmd = new OracleCommand(sql, connection) { BindByName = true };
                cmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = request.Username;
                cmd.Parameters.Add(":e", OracleDbType.Varchar2).Value = request.Email;
                var cnt = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (cnt == 0)
                {
                    return new ForgotPasswordInitiateResponse
                    {
                        Success = false,
                        Message = "Không tìm thấy tài khoản phù hợp. Vui lòng đăng ký mới.",
                        ShouldRegister = true,
                        ExpiresInSeconds = 0
                    };
                }

                var entry = _otpStore.Create("forgot", request.Email, request.Username, null, TimeSpan.FromMinutes(10));
                var subject = "Mã OTP đặt lại mật khẩu - Trung tâm LDA";
                var body = $"<p>Xin chào {System.Net.WebUtility.HtmlEncode(request.Username)},</p>" +
                           $"<p>Mã OTP đặt lại mật khẩu của bạn là: <strong style='font-size:18px'>{entry.OtpCode}</strong></p>" +
                           "<p>Mã có hiệu lực trong 10 phút.</p>";
                await _emailService.SendAsync(request.Email, subject, body);

                return new ForgotPasswordInitiateResponse
                {
                    Success = true,
                    Message = "Đã gửi mã OTP đến email",
                    CorrelationId = entry.CorrelationId,
                    ExpiresInSeconds = 600,
                    ShouldRegister = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InitiateForgotPasswordAsync error");
                return new ForgotPasswordInitiateResponse { Success = false, Message = "Không thể xử lý yêu cầu", ShouldRegister = false };
            }
        }

        public async Task<BasicResponse> VerifyForgotPasswordAsync(ForgotPasswordVerifyRequest request)
        {
            if (!_otpStore.Validate(request.CorrelationId, request.Otp, out var entry) || entry == null || entry.Purpose != "forgot")
            {
                return new BasicResponse { Success = false, Message = "Mã OTP không hợp lệ hoặc đã hết hạn" };
            }

            var username = entry.Username ?? string.Empty;
            try
            {
                var ok = await ResetOracleUserPasswordAsync(username, request.NewPassword);
                if (ok)
                {
                    _otpStore.Remove(request.CorrelationId);
                    return new BasicResponse { Success = true, Message = "Đặt lại mật khẩu thành công" };
                }
                return new BasicResponse { Success = false, Message = "Không thể đặt lại mật khẩu" };
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                _logger.LogError(oex, "Insufficient privileges to ALTER USER for {User}", username);
                return new BasicResponse { Success = false, Message = "Hệ thống không đủ quyền để đổi mật khẩu (ORA-01031). Vui lòng cấp quyền ALTER USER cho tài khoản kết nối." };
            }
            catch (InvalidOperationException ex)
            {
                // Oracle USER không tồn tại - thử tạo mới
                _logger.LogWarning(ex, "Oracle USER not found for {User}, attempting to create", username);
                try
                {
                    using var connection = new OracleConnection(_connectionString);
                    await connection.OpenAsync();
                    var created = await CreateOracleUserAsync(connection, username, request.NewPassword);
                    if (created)
                    {
                        _otpStore.Remove(request.CorrelationId);
                        return new BasicResponse { Success = true, Message = "Đã tạo tài khoản Oracle và đặt mật khẩu thành công" };
                    }
                    return new BasicResponse { Success = false, Message = "Không thể tạo tài khoản Oracle" };
                }
                catch (Exception createEx)
                {
                    _logger.LogError(createEx, "Failed to create Oracle USER for {User}", username);
                    return new BasicResponse { Success = false, Message = "Tài khoản Oracle không tồn tại và không thể tạo mới. Vui lòng liên hệ quản trị viên." };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyForgotPasswordAsync error for {User}", username);
                return new BasicResponse { Success = false, Message = "Có lỗi xảy ra khi đặt lại mật khẩu" };
            }
        }

        private async Task<bool> ResetOracleUserPasswordAsync(string username, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(newPassword)) return false;

            // Only allow safe characters to avoid SQL injection since ALTER USER likely doesn't support bind for password
            if (!System.Text.RegularExpressions.Regex.IsMatch(newPassword, @"^[A-Za-z0-9_@#\-]{6,64}$"))
            {
                throw new ArgumentException("Mật khẩu chỉ được phép chứa chữ, số, và ký tự _ @ # - , tối thiểu 6 ký tự");
            }

            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            // 1) Kiểm tra USER Oracle có tồn tại không
            using (var existCmd = new OracleCommand("SELECT COUNT(*) FROM ALL_USERS WHERE USERNAME = :u", connection) { BindByName = true })
            {
                existCmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = username.Trim().ToUpperInvariant();
                var cnt = Convert.ToInt32(await existCmd.ExecuteScalarAsync());
                if (cnt == 0)
                {
                    throw new InvalidOperationException("Tài khoản Oracle không tồn tại");
                }
            }

            // 2) Thực hiện ALTER USER
            var uname = username.Trim().ToUpperInvariant();
            var sql = $"ALTER USER \"{uname}\" IDENTIFIED BY \"{newPassword}\""; // quote identifier and password
            using (var cmd = new OracleCommand(sql, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }
            // Mở khóa tài khoản phòng trường hợp bị lock do nhập sai nhiều lần
            using (var unlockCmd = new OracleCommand($"ALTER USER \"{uname}\" ACCOUNT UNLOCK", connection))
            {
                await unlockCmd.ExecuteNonQueryAsync();
            }

            // 3) Xác minh bằng cách mở kết nối bằng mật khẩu mới
            var baseCs = new OracleConnectionStringBuilder(_connectionString);
            var userCs = new OracleConnectionStringBuilder
            {
                DataSource = baseCs.DataSource,
                UserID = username.Trim(),
                Password = newPassword
            };
            try
            {
                using var testConn = new OracleConnection(userCs.ConnectionString);
                await testConn.OpenAsync();

                // 4) Đồng bộ cột MAT_KHAU (nếu tồn tại) trong bảng TAI_KHOAN để phù hợp kỳ vọng hiển thị trên SQL Developer
                await TryUpdateTaiKhoanPasswordAsync(connection, uname, newPassword);
                return true;
            }
            catch (OracleException oex)
            {
                _logger.LogWarning(oex, "Password verification login failed for {User}", username);
                return false;
            }
        }

        private async Task TryUpdateTaiKhoanPasswordAsync(OracleConnection adminConnection, string upperUsername, string newPassword)
        {
            try
            {
                // Kiểm tra cột MAT_KHAU có tồn tại không
                using (var checkCol = new OracleCommand("SELECT COUNT(*) FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'TAI_KHOAN' AND COLUMN_NAME = 'MAT_KHAU'", adminConnection))
                {
                    var colCnt = Convert.ToInt32(await checkCol.ExecuteScalarAsync());
                    if (colCnt == 0) return; // Không có cột để sync
                }

                // Hash SHA-256 để tránh lưu plain text
                string hashed;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(newPassword);
                    var hash = sha.ComputeHash(bytes);
                    hashed = "SHA256:" + BitConverter.ToString(hash).Replace("-", string.Empty);
                }

                using var up = new OracleCommand("UPDATE TAI_KHOAN SET MAT_KHAU = :p WHERE UPPER(TEN_DANG_NHAP) = :u", adminConnection) { BindByName = true };
                up.Parameters.Add(":p", OracleDbType.Varchar2).Value = hashed;
                up.Parameters.Add(":u", OracleDbType.Varchar2).Value = upperUsername;
                await up.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not sync MAT_KHAU column for user {User}", upperUsername);
            }
        }
    }
}