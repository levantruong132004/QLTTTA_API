using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using System.Data;

namespace QLTTTA_API.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> AuthenticateAsync(LoginRequest request);
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
    }
}