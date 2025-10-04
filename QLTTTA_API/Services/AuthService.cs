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
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();
                _logger.LogInformation("Login attempt - Username: {Username}", request.Username);

                // Thêm FULL_NAME từ STUDENTS (nếu là học viên sẽ có bản ghi)
                var sql = @"SELECT a.USER_ID,
                                                                     a.USERNAME,
                                                                     a.EMAIL,
                                                                     a.ROLE_ID,
                                                                     r.ROLE_NAME,
                                                                     a.IS_ACTIVE,
                                                                     s.FULL_NAME
                                                            FROM QLTT_ADMIN.ACCOUNTS a
                                                            LEFT JOIN QLTT_ADMIN.ROLES r ON a.ROLE_ID = r.ROLE_ID
                                                            LEFT JOIN QLTT_ADMIN.STUDENTS s ON s.STUDENT_ID = a.USER_ID
                                                         WHERE a.USERNAME = :username
                                                             AND a.PASSWORD = :password"; // Có thể bổ sung AND a.IS_ACTIVE = 1 nếu muốn chặn hẳn

                using var command = new OracleCommand(sql, connection)
                {
                    BindByName = true
                };
                command.Parameters.Add(":username", OracleDbType.Varchar2).Value = request.Username?.Trim();
                command.Parameters.Add(":password", OracleDbType.Varchar2).Value = request.Password; // TODO: Hash password

                using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (await reader.ReadAsync())
                {
                    // Sử dụng ordinal để tránh lỗi truy cập theo tên cột (đảm bảo tương thích OracleDataReader)
                    int ordUserId = reader.GetOrdinal("USER_ID");
                    int ordUsername = reader.GetOrdinal("USERNAME");
                    int ordEmail = reader.GetOrdinal("EMAIL");
                    int ordRoleName = reader.GetOrdinal("ROLE_NAME");
                    int ordIsActive = reader.GetOrdinal("IS_ACTIVE");
                    int ordFullName = -1;
                    try { ordFullName = reader.GetOrdinal("FULL_NAME"); } catch { }

                    // Nếu cột IS_ACTIVE tồn tại và =0 thì báo lỗi
                    bool inactive = !reader.IsDBNull(ordIsActive) && reader.GetInt32(ordIsActive) == 0;
                    if (inactive)
                    {
                        return new LoginResponse
                        {
                            Success = false,
                            Message = "Tài khoản đã bị khóa / vô hiệu hóa"
                        };
                    }

                    var userInfo = new UserInfo
                    {
                        UserId = reader.IsDBNull(ordUserId) ? 0 : reader.GetInt32(ordUserId),
                        Username = reader.IsDBNull(ordUsername) ? string.Empty : reader.GetString(ordUsername),
                        Email = reader.IsDBNull(ordEmail) ? string.Empty : reader.GetString(ordEmail),
                        Role = reader.IsDBNull(ordRoleName) ? string.Empty : reader.GetString(ordRoleName),
                        FullName = (ordFullName >= 0 && !reader.IsDBNull(ordFullName)) ? reader.GetString(ordFullName) : string.Empty
                    };

                    _logger.LogInformation("Login success for {Username} with role {Role}", userInfo.Username, userInfo.Role);
                    return new LoginResponse
                    {
                        Success = true,
                        Message = "Đăng nhập thành công",
                        Token = GenerateToken(userInfo),
                        User = userInfo
                    };
                }

                _logger.LogWarning("Login failed - invalid credentials for {Username}", request.Username);
                return new LoginResponse
                {
                    Success = false,
                    Message = "Tên tài khoản hoặc mật khẩu không đúng"
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
                _logger.LogInformation("Bắt đầu đăng ký user: {Username}", request.Username);

                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();
                _logger.LogInformation("Kết nối database thành công");

                // Kiểm tra xem username đã tồn tại chưa
                var checkUserSql = "SELECT COUNT(*) FROM QLTT_ADMIN.ACCOUNTS WHERE USERNAME = :username";
                using var checkCommand = new OracleCommand(checkUserSql, connection);
                checkCommand.Parameters.Add(":username", OracleDbType.Varchar2).Value = request.Username;

                var userExists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;
                _logger.LogInformation("Kiểm tra username tồn tại: {UserExists}", userExists);

                if (userExists)
                {
                    return new RegisterResponse
                    {
                        Success = false,
                        Message = "Tên tài khoản đã tồn tại"
                    };
                }

                // Kiểm tra email đã tồn tại chưa
                var checkEmailSql = "SELECT COUNT(*) FROM QLTT_ADMIN.ACCOUNTS WHERE EMAIL = :email";
                using var checkEmailCommand = new OracleCommand(checkEmailSql, connection);
                checkEmailCommand.Parameters.Add(":email", OracleDbType.Varchar2).Value = request.Email;

                var emailExists = Convert.ToInt32(await checkEmailCommand.ExecuteScalarAsync()) > 0;
                _logger.LogInformation("Kiểm tra email tồn tại: {EmailExists}", emailExists);

                if (emailExists)
                {
                    return new RegisterResponse
                    {
                        Success = false,
                        Message = "Email đã được sử dụng"
                    };
                }

                // BẮT ĐẦU TRANSACTION tạo Account + Student
                using var transaction = connection.BeginTransaction();
                try
                {
                    // 1. Role học viên
                    int studentRoleId = 0;
                    try
                    {
                        var roleSql = @"SELECT ROLE_ID FROM QLTT_ADMIN.ROLES WHERE UPPER(ROLE_NAME) IN ('STUDENT','HỌC VIÊN','HOC VIEN') FETCH FIRST 1 ROWS ONLY";
                        using var roleCmd = new OracleCommand(roleSql, connection) { BindByName = true, Transaction = transaction };
                        var roleResult = await roleCmd.ExecuteScalarAsync();
                        if (roleResult != null && int.TryParse(roleResult.ToString(), out var rid) && rid > 0)
                            studentRoleId = rid;
                    }
                    catch (Exception exRole)
                    {
                        _logger.LogWarning(exRole, "Không lấy được ROLE_ID cho student, fallback=4");
                    }
                    if (studentRoleId == 0) studentRoleId = 4; // fallback theo sample_data.sql

                    // 2. Insert ACCOUNTS RETURNING USER_ID
                    int newUserId = 0;
                    using (var accCmd = new OracleCommand(@"INSERT INTO QLTT_ADMIN.ACCOUNTS (USERNAME,PASSWORD,EMAIL,ROLE_ID,IS_ACTIVE)
                                                             VALUES (:username,:password,:email,:roleId,1)
                                                             RETURNING USER_ID INTO :p_user_id", connection))
                    {
                        accCmd.Transaction = transaction;
                        accCmd.BindByName = true;
                        accCmd.Parameters.Add(":username", OracleDbType.Varchar2).Value = request.Username.Trim();
                        accCmd.Parameters.Add(":password", OracleDbType.Varchar2).Value = request.Password; // TODO: hash
                        accCmd.Parameters.Add(":email", OracleDbType.Varchar2).Value = request.Email.Trim();
                        accCmd.Parameters.Add(":roleId", OracleDbType.Int32).Value = studentRoleId;
                        var outParam = new OracleParameter(":p_user_id", OracleDbType.Int32, System.Data.ParameterDirection.Output);
                        accCmd.Parameters.Add(outParam);
                        await accCmd.ExecuteNonQueryAsync();
                        if (outParam.Value != null && int.TryParse(outParam.Value.ToString(), out var tmp)) newUserId = tmp;
                    }
                    if (newUserId <= 0)
                    {
                        transaction.Rollback();
                        return new RegisterResponse { Success = false, Message = "Không lấy được USER_ID mới" };
                    }

                    // 3. Insert STUDENTS (profile) - để trigger sinh STUDENT_CODE nếu có
                    using (var stuCmd = new OracleCommand(@"INSERT INTO QLTT_ADMIN.STUDENTS (STUDENT_ID,FULL_NAME,SEX,DATE_OF_BIRTH,PHONE_NUMBER,ADDRESS)
                                                             VALUES (:id,:full,:sex,:dob,:phone,:addr)", connection))
                    {
                        stuCmd.Transaction = transaction;
                        stuCmd.BindByName = true;
                        stuCmd.Parameters.Add(":id", OracleDbType.Int32).Value = newUserId;
                        stuCmd.Parameters.Add(":full", OracleDbType.NVarchar2).Value = request.FullName;
                        stuCmd.Parameters.Add(":sex", OracleDbType.NVarchar2).Value = request.Sex;
                        stuCmd.Parameters.Add(":dob", OracleDbType.Date).Value = (object?)request.DateOfBirth ?? DBNull.Value;
                        stuCmd.Parameters.Add(":phone", OracleDbType.Varchar2).Value = request.PhoneNumber;
                        stuCmd.Parameters.Add(":addr", OracleDbType.NVarchar2).Value = (object?)request.Address ?? DBNull.Value;
                        await stuCmd.ExecuteNonQueryAsync();
                    }

                    // 4. Commit
                    transaction.Commit();

                    // 5. Lấy lại thông tin (join students để có FULL_NAME & STUDENT_CODE nếu cần sau này)
                    using (var infoCmd = new OracleCommand(@"SELECT a.USER_ID, a.USERNAME, a.EMAIL, r.ROLE_NAME, s.FULL_NAME
                                                             FROM QLTT_ADMIN.ACCOUNTS a
                                                             LEFT JOIN QLTT_ADMIN.ROLES r ON a.ROLE_ID = r.ROLE_ID
                                                             LEFT JOIN QLTT_ADMIN.STUDENTS s ON s.STUDENT_ID = a.USER_ID
                                                             WHERE a.USER_ID = :id", connection))
                    {
                        infoCmd.BindByName = true;
                        infoCmd.Parameters.Add(":id", OracleDbType.Int32).Value = newUserId;
                        using var rdr = await infoCmd.ExecuteReaderAsync();
                        if (await rdr.ReadAsync())
                        {
                            var userInfo = new UserInfo
                            {
                                UserId = rdr.IsDBNull(rdr.GetOrdinal("USER_ID")) ? 0 : rdr.GetInt32(rdr.GetOrdinal("USER_ID")),
                                Username = rdr.IsDBNull(rdr.GetOrdinal("USERNAME")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("USERNAME")),
                                Email = rdr.IsDBNull(rdr.GetOrdinal("EMAIL")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("EMAIL")),
                                Role = rdr.IsDBNull(rdr.GetOrdinal("ROLE_NAME")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("ROLE_NAME")),
                                FullName = rdr.IsDBNull(rdr.GetOrdinal("FULL_NAME")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("FULL_NAME"))
                            };

                            return new RegisterResponse
                            {
                                Success = true,
                                Message = "Đăng ký học viên thành công",
                                User = userInfo
                            };
                        }
                    }

                    return new RegisterResponse { Success = true, Message = "Đăng ký thành công (không lấy được chi tiết sau commit)" };
                }
                catch (Exception txEx)
                {
                    try { transaction.Rollback(); } catch { }
                    _logger.LogError(txEx, "Rollback đăng ký user={Username}", request.Username);
                    return new RegisterResponse { Success = false, Message = "Lỗi khi tạo học viên: " + txEx.Message };
                }
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
    }
}