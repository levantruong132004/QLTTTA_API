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

                // Truy vấn kiểm tra tài khoản từ bảng QLTT_ADMIN.ACCOUNTS
                var sql = @"
                    SELECT 
                        a.USER_ID,
                        a.USERNAME,
                        a.EMAIL,
                        a.ROLE_ID,
                        r.ROLE_NAME
                    FROM QLTT_ADMIN.ACCOUNTS a
                    LEFT JOIN QLTT_ADMIN.ROLES r ON a.ROLE_ID = r.ROLE_ID
                    WHERE a.USERNAME = :username AND a.PASSWORD = :password";

                using var command = new OracleCommand(sql, connection);
                command.Parameters.Add(":username", OracleDbType.Varchar2).Value = request.Username;
                command.Parameters.Add(":password", OracleDbType.Varchar2).Value = request.Password; // Trong thực tế nên hash password

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var userInfo = new UserInfo
                    {
                        UserId = reader.IsDBNull("USER_ID") ? 0 : reader.GetInt32("USER_ID"),
                        Username = reader.IsDBNull("USERNAME") ? "" : reader.GetString("USERNAME"),
                        Email = reader.IsDBNull("EMAIL") ? "" : reader.GetString("EMAIL"),
                        Role = reader.IsDBNull("ROLE_NAME") ? "" : reader.GetString("ROLE_NAME")
                    };

                    return new LoginResponse
                    {
                        Success = true,
                        Message = "Đăng nhập thành công",
                        Token = GenerateToken(userInfo), // Tạo JWT token nếu cần
                        User = userInfo
                    };
                }
                else
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Tên tài khoản hoặc mật khẩu không đúng"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xác thực người dùng");
                return new LoginResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra trong quá trình đăng nhập"
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

                // Lấy danh sách cột thực tế của bảng ACCOUNTS
                var columns = new List<string>();
                var columnsSql = @"SELECT COLUMN_NAME FROM USER_TAB_COLUMNS 
                                    WHERE TABLE_NAME = 'ACCOUNTS'";
                using (var colCmd = new OracleCommand(columnsSql, connection))
                using (var colReader = await colCmd.ExecuteReaderAsync())
                {
                    while (await colReader.ReadAsync())
                    {
                        columns.Add(colReader.GetString(0).ToUpperInvariant());
                    }
                }
                _logger.LogInformation("ACCOUNTS columns: {Cols}", string.Join(",", columns));

                // Các cột chuẩn mà ta mong muốn
                var desired = new[] { "USERNAME", "PASSWORD", "EMAIL", "FULL_NAME", "PHONE_NUMBER", "ROLE_ID", "CREATED_DATE" };
                // Giữ lại những cột thực sự tồn tại và cần thiết
                var insertable = desired.Where(c => columns.Contains(c) && c != "ROLE_ID" && c != "CREATED_DATE").ToList();
                // ROLE_ID xử lý riêng nếu tồn tại
                var hasRoleId = columns.Contains("ROLE_ID");
                var hasCreated = columns.Contains("CREATED_DATE");

                // Build phần cột
                var colList = new List<string>(insertable);
                if (hasRoleId) colList.Add("ROLE_ID");
                if (hasCreated) colList.Add("CREATED_DATE");

                // Build phần values
                var valueList = new List<string>();
                foreach (var c in insertable)
                {
                    valueList.Add(":" + c.ToLowerInvariant());
                }

                // Lấy ROLE_ID cho STUDENT động thay vì hard-code (tránh gán nhầm quyền cao hơn)
                int studentRoleId = 0;
                if (hasRoleId)
                {
                    try
                    {
                        var roleSql = "SELECT ROLE_ID FROM QLTT_ADMIN.ROLES WHERE UPPER(ROLE_NAME) = 'STUDENT'";
                        using var roleCmd = new OracleCommand(roleSql, connection);
                        var roleResult = await roleCmd.ExecuteScalarAsync();
                        if (roleResult != null && int.TryParse(roleResult.ToString(), out var rid))
                        {
                            studentRoleId = rid;
                        }
                    }
                    catch (Exception exRole)
                    {
                        _logger.LogWarning(exRole, "Không lấy được ROLE_ID STUDENT, sẽ dùng fallback");
                    }

                    // Fallback nếu không tìm được (cập nhật giá trị này theo dữ liệu thực tế của bạn)
                    if (studentRoleId == 0)
                    {
                        studentRoleId = 3; // giả định 3 là ROLE_ID của STUDENT
                    }

                    valueList.Add(":role_id");
                }
                if (hasCreated) valueList.Add("SYSDATE");

                var insertSql = $"INSERT INTO QLTT_ADMIN.ACCOUNTS (" + string.Join(", ", colList) + ") VALUES (" + string.Join(", ", valueList) + ")";
                _logger.LogInformation("Dynamic INSERT SQL: {SQL}", insertSql);

                int rowsAffected;
                using (var insertCommand = new OracleCommand(insertSql, connection))
                {
                    // Thêm parameters tương ứng với các cột tồn tại
                    if (insertable.Contains("USERNAME")) insertCommand.Parameters.Add(":username", OracleDbType.Varchar2).Value = request.Username;
                    if (insertable.Contains("PASSWORD")) insertCommand.Parameters.Add(":password", OracleDbType.Varchar2).Value = request.Password;
                    if (insertable.Contains("EMAIL")) insertCommand.Parameters.Add(":email", OracleDbType.Varchar2).Value = request.Email;
                    if (insertable.Contains("FULL_NAME")) insertCommand.Parameters.Add(":full_name", OracleDbType.Varchar2).Value = request.FullName;
                    if (insertable.Contains("PHONE_NUMBER")) insertCommand.Parameters.Add(":phone_number", OracleDbType.Varchar2).Value = request.PhoneNumber;

                    if (hasRoleId)
                    {
                        insertCommand.Parameters.Add(":role_id", OracleDbType.Int32).Value = studentRoleId;
                    }

                    rowsAffected = await insertCommand.ExecuteNonQueryAsync();
                }
                _logger.LogInformation("INSERT hoàn thành (dynamic), rows affected: {RowsAffected}", rowsAffected);
                if (rowsAffected <= 0)
                {
                    return new RegisterResponse
                    {
                        Success = false,
                        Message = "Không chèn được bản ghi mới"
                    };
                }

                if (rowsAffected > 0)
                {
                    // Lấy thông tin user vừa tạo (dynamic columns)
                    var selectCols = new List<string> { "a.USER_ID", "a.USERNAME" };
                    if (columns.Contains("EMAIL")) selectCols.Add("a.EMAIL");
                    if (columns.Contains("FULL_NAME")) selectCols.Add("a.FULL_NAME");
                    selectCols.Add("a.ROLE_ID");
                    selectCols.Add("r.ROLE_NAME");

                    var getUserSql = $@"SELECT {string.Join(",", selectCols)}
                                        FROM QLTT_ADMIN.ACCOUNTS a
                                        LEFT JOIN QLTT_ADMIN.ROLES r ON a.ROLE_ID = r.ROLE_ID
                                        WHERE a.USERNAME = :username";

                    using (var getUserCommand = new OracleCommand(getUserSql, connection))
                    {
                        getUserCommand.Parameters.Add(":username", OracleDbType.Varchar2).Value = request.Username;
                        using var reader2 = await getUserCommand.ExecuteReaderAsync();
                        if (await reader2.ReadAsync())
                        {
                            string emailVal = columns.Contains("EMAIL") && !reader2.IsDBNull(reader2.GetOrdinal("EMAIL")) ? reader2.GetString(reader2.GetOrdinal("EMAIL")) : "";
                            string fullNameVal = columns.Contains("FULL_NAME") && !reader2.IsDBNull(reader2.GetOrdinal("FULL_NAME")) ? reader2.GetString(reader2.GetOrdinal("FULL_NAME")) : "";
                            var userInfo = new UserInfo
                            {
                                UserId = reader2.IsDBNull(reader2.GetOrdinal("USER_ID")) ? 0 : reader2.GetInt32(reader2.GetOrdinal("USER_ID")),
                                Username = reader2.IsDBNull(reader2.GetOrdinal("USERNAME")) ? "" : reader2.GetString(reader2.GetOrdinal("USERNAME")),
                                FullName = fullNameVal,
                                Email = emailVal,
                                Role = reader2.IsDBNull(reader2.GetOrdinal("ROLE_NAME")) ? "" : reader2.GetString(reader2.GetOrdinal("ROLE_NAME"))
                            };

                            return new RegisterResponse
                            {
                                Success = true,
                                Message = "Đăng ký tài khoản thành công",
                                User = userInfo
                            };
                        }
                    }
                }

                return new RegisterResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi tạo tài khoản"
                };
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