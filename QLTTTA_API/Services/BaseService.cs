using Oracle.ManagedDataAccess.Client;
using System.Data;
using Microsoft.Extensions.Logging;

namespace QLTTTA_API.Services
{
    /// <summary>
    /// Interface chuẩn cho các service thao tác dữ liệu: cung cấp kết nối theo user (nếu có) hoặc admin.
    /// </summary>
    public interface IBaseService
    {
        /// <summary>
        /// Lấy kết nối ưu tiên theo user (per-user). Nếu không có provider hoặc lỗi sẽ fallback sang admin.
        /// </summary>
        Task<OracleConnection> GetConnectionAsync();
        /// <summary>
        /// Lấy kết nối admin cố định (bỏ qua cơ chế per-user).
        /// </summary>
        Task<OracleConnection> GetAdminConnectionAsync();
    }

    /// <summary>
    /// Lớp cơ sở cung cấp tiện ích truy vấn Oracle (Query, Scalar, NonQuery) và ánh xạ kết quả về object.
    /// - Tự động chọn kết nối user nếu có (đảm bảo quyền hạn đúng học viên).
    /// - Cung cấp hàm ánh xạ linh động bằng reflection.
    /// </summary>
    public class BaseService : IBaseService
    {
        protected readonly string _connectionString;
        private readonly IOracleConnectionProvider? _userConnProvider;
        protected readonly ILogger _logger;
        private readonly IHttpContextAccessor? _httpContextAccessor;

        /// <summary>
        /// Khởi tạo BaseService với cấu hình DB, logger và provider kết nối user (có thể null nếu service không cần per-user).
        /// </summary>
        public BaseService(IConfiguration configuration, ILogger logger, IOracleConnectionProvider? userConnProvider = null, IHttpContextAccessor? httpContextAccessor = null)
        {
            _connectionString = configuration.GetConnectionString("OracleDbConnection") ??
                throw new ArgumentNullException("Connection string not found");
            _logger = logger;
            _userConnProvider = userConnProvider;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Trả về kết nối ưu tiên theo user. Nếu provider ném lỗi (không hợp lệ / hết hạn) sẽ log và fallback sang admin.
        /// </summary>
        public async Task<OracleConnection> GetConnectionAsync()
        {
            // Ưu tiên kết nối per-user nếu có provider, để các truy vấn chạy đúng ngữ cảnh USER (học viên)
            if (_userConnProvider != null)
            {
                try
                {
                    return await _userConnProvider.GetUserConnectionAsync();
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning(ex, "Per-user connection unavailable, falling back to admin connection");
                    return await GetAdminConnectionAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Per-user connection failed unexpectedly, fallback admin");
                    return await GetAdminConnectionAsync();
                }
            }
            // Nếu không cấu hình provider thì dùng admin
            return await GetAdminConnectionAsync();
        }

        /// <summary>
        /// Luôn trả về kết nối admin (bỏ qua cơ chế user). Dùng cho các tác vụ hệ thống hoặc fallback.
        /// SET CLIENT_IDENTIFIER để VPD policy hoạt động đúng.
        /// </summary>
        public async Task<OracleConnection> GetAdminConnectionAsync()
        {
            var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureCurrentSchemaAsync(connection);
            
            // Set CLIENT_IDENTIFIER từ session để VPD nhận dạng user
            await SetClientIdentifierAsync(connection);
            
            return connection;
        }

        /// <summary>
        /// Đặt CURRENT_SCHEMA về QLTT_ADMIN để các câu lệnh không có prefix schema vẫn trỏ đúng schema dữ liệu.
        /// Người dùng vẫn cần có quyền trên đối tượng thuộc schema này (qua GRANT/ROLE).
        /// </summary>
        private static async Task EnsureCurrentSchemaAsync(OracleConnection conn)
        {
            try
            {
                using var cmd = new OracleCommand("ALTER SESSION SET CURRENT_SCHEMA = QLTT_ADMIN", conn)
                {
                    CommandType = CommandType.Text
                };
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Không chặn luồng nếu ALTER SESSION lỗi; tiếp tục dùng schema mặc định của user
            }
        }

        /// <summary>
        /// Set CLIENT_IDENTIFIER từ session header để VPD policy hoạt động
        /// </summary>
        private async Task SetClientIdentifierAsync(OracleConnection conn)
        {
            try
            {
                string? username = null;
                string? sessionId = null;
                string deviceType = "pc";
                if (_httpContextAccessor?.HttpContext != null)
                {
                    // Ưu tiên header
                    sessionId = _httpContextAccessor.HttpContext.Request.Headers["X-Session-Id"].FirstOrDefault();
                    deviceType = _httpContextAccessor.HttpContext.Request.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
                    if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
                    // Fallback cookie nếu header trống (khi gọi trực tiếp API qua Swagger / JS fetch chưa gắn handler)
                    if (string.IsNullOrWhiteSpace(sessionId))
                    {
                        sessionId = _httpContextAccessor.HttpContext.Request.Cookies["SessionId"];
                    }
                    if (!string.IsNullOrWhiteSpace(sessionId))
                    {
                        var columnName = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                        try
                        {
                            using var cmd = new OracleCommand($"SELECT TEN_DANG_NHAP FROM TAI_KHOAN WHERE {columnName} = :sid", conn) { BindByName = true };
                            cmd.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sessionId;
                            var result = await cmd.ExecuteScalarAsync();
                            if (result != null && result != DBNull.Value)
                            {
                                username = result.ToString();
                            }
                        }
                        catch (OracleException oex) when (oex.Number == 904) // ORA-00904 invalid identifier
                        {
                            // Fallback legacy cột SESSION_ID_HIENTAI nếu chưa migrate
                            using var cmd2 = new OracleCommand("SELECT TEN_DANG_NHAP FROM TAI_KHOAN WHERE SESSION_ID_HIENTAI = :sid", conn) { BindByName = true };
                            cmd2.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sessionId;
                            var result2 = await cmd2.ExecuteScalarAsync();
                            if (result2 != null && result2 != DBNull.Value)
                            {
                                username = result2.ToString();
                            }
                        }
                    }
                }
                if (!string.IsNullOrEmpty(username))
                {
                    using var setCmd = new OracleCommand("BEGIN DBMS_SESSION.SET_IDENTIFIER(:ident); END;", conn) { BindByName = true };
                    setCmd.Parameters.Add(":ident", OracleDbType.Varchar2).Value = username;
                    await setCmd.ExecuteNonQueryAsync();
                    _logger.LogDebug("Set CLIENT_IDENTIFIER to {Username} (sid={SessionId}, device={DeviceType})", username, sessionId ?? "NULL", deviceType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set CLIENT_IDENTIFIER");
            }
        }

        /// <summary>
        /// Thực thi câu lệnh SELECT trả về nhiều dòng. Cho phép truyền mapper tùy chỉnh;
        /// nếu mapper null sẽ dùng MapToObject reflection.
        /// </summary>
        protected async Task<List<T>> ExecuteQueryAsync<T>(string sql, object? parameters = null,
            Func<OracleDataReader, T>? mapper = null) where T : new()
        {
            var results = new List<T>();

            try
            {
                using var connection = await GetConnectionAsync();
                using var command = new OracleCommand(sql, connection);

                if (parameters != null)
                {
                    AddParameters(command, parameters);
                }

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (mapper != null)
                    {
                        results.Add(mapper(reader));
                    }
                    else
                    {
                        results.Add(MapToObject<T>(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query: {SQL}", sql);
                throw;
            }

            return results;
        }

        /// <summary>
        /// Thực thi SELECT lấy duy nhất 1 dòng (hoặc null nếu không có). Mapper tùy chọn.
        /// </summary>
        protected async Task<T?> ExecuteQuerySingleAsync<T>(string sql, object? parameters = null,
            Func<OracleDataReader, T>? mapper = null) where T : class, new()
        {
            try
            {
                using var connection = await GetConnectionAsync();
                using var command = new OracleCommand(sql, connection);

                if (parameters != null)
                {
                    AddParameters(command, parameters);
                }

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return mapper != null ? mapper(reader) : MapToObject<T>(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing single query: {SQL}", sql);
                throw;
            }

            return null;
        }

        /// <summary>
        /// Thực thi câu lệnh INSERT/UPDATE/DELETE. Trả về số dòng bị ảnh hưởng.
        /// </summary>
        protected async Task<int> ExecuteNonQueryAsync(string sql, object? parameters = null)
        {
            try
            {
                using var connection = await GetConnectionAsync();
                using var command = new OracleCommand(sql, connection);

                if (parameters != null)
                {
                    AddParameters(command, parameters);
                }

                return await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing non-query: {SQL}", sql);
                throw;
            }
        }

        /// <summary>
        /// Thực thi câu lệnh trả về giá trị đơn (ví dụ COUNT(*), MAX...).
        /// </summary>
        protected async Task<object?> ExecuteScalarAsync(string sql, object? parameters = null)
        {
            try
            {
                using var connection = await GetConnectionAsync();
                using var command = new OracleCommand(sql, connection);

                if (parameters != null)
                {
                    AddParameters(command, parameters);
                }

                return await command.ExecuteScalarAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing scalar: {SQL}", sql);
                throw;
            }
        }

        /// <summary>
        /// Thêm parameters vào OracleCommand: lấy tất cả property của object truyền vào và tạo tham số :propertyname.
        /// Chuyển Null sang DBNull.Value.
        /// </summary>
        private void AddParameters(OracleCommand command, object parameters)
        {
            var properties = parameters.GetType().GetProperties();
            foreach (var property in properties)
            {
                var value = property.GetValue(parameters);
                command.Parameters.Add($":{property.Name.ToLower()}", value ?? DBNull.Value);
            }
        }

        /// <summary>
        /// Ánh xạ một dòng OracleDataReader sang object kiểu T bằng reflection, có xử lý Nullable.
        /// </summary>
        private T MapToObject<T>(OracleDataReader reader) where T : new()
        {
            var obj = new T();
            var properties = typeof(T).GetProperties();

            foreach (var property in properties)
            {
                try
                {
                    // Try multiple possible column names (aliases and base names)
                    foreach (var columnName in GetPossibleColumnNames(property.Name))
                    {
                        if (HasColumn(reader, columnName) && !reader.IsDBNull(columnName))
                        {
                            var value = reader[columnName];

                            if (property.PropertyType == typeof(DateTime?) && value is DateTime dateValue)
                            {
                                property.SetValue(obj, dateValue);
                            }
                            else if (property.PropertyType == typeof(TimeSpan?) && value is TimeSpan timeValue)
                            {
                                property.SetValue(obj, timeValue);
                            }
                            else if (property.PropertyType.IsGenericType &&
                                     property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                            {
                                var underlyingType = Nullable.GetUnderlyingType(property.PropertyType);
                                property.SetValue(obj, Convert.ChangeType(value, underlyingType!));
                            }
                            else
                            {
                                property.SetValue(obj, Convert.ChangeType(value, property.PropertyType));
                            }
                            break; // Stop after first matching column
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error mapping property {PropertyName}", property.Name);
                }
            }

            return obj;
        }

        // Provide alias + base names for known properties so mapping works regardless of SELECT aliases.
        private IEnumerable<string> GetPossibleColumnNames(string propertyName)
        {
            // Normalize switch for known models
            switch (propertyName)
            {
                // Student (both EN + VI schemas)
                case "StudentId": return new[] { "STUDENT_ID", "ID_HOC_VIEN", GetColumnName(propertyName) };
                case "FullName": return new[] { "FULL_NAME", "HO_TEN", GetColumnName(propertyName) };
                case "StudentCode": return new[] { "STUDENT_CODE", "MA_HOC_VIEN", GetColumnName(propertyName) };
                case "Sex": return new[] { "SEX", "GIOI_TINH", GetColumnName(propertyName) };
                case "DateOfBirth": return new[] { "DATE_OF_BIRTH", "NGAY_SINH", GetColumnName(propertyName) };
                case "PhoneNumber": return new[] { "PHONE_NUMBER", "SO_DIEN_THOAI", GetColumnName(propertyName) };
                case "Address": return new[] { "ADDRESS", "DIA_CHI", GetColumnName(propertyName) };

                // Course
                case "CourseId": return new[] { "ID_KHOA_HOC", "COURSE_ID", GetColumnName(propertyName) };
                case "CourseCode": return new[] { "MA_KHOA_HOC", "COURSE_CODE", GetColumnName(propertyName) };
                case "CourseName": return new[] { "COURSE_NAME", "TEN_KHOA_HOC", GetColumnName(propertyName) };
                case "Description": return new[] { "DESCRIPTION", "MO_TA", GetColumnName(propertyName) };
                case "StandardFee": return new[] { "STANDARD_FEE", "HOC_PHI_TIEU_CHUAN", GetColumnName(propertyName) };

                // Class
                case "ClassId": return new[] { "CLASS_ID", "ID_LOP_HOC", GetColumnName(propertyName) };
                case "ClassCode": return new[] { "CLASS_CODE", "MA_LOP_HOC", GetColumnName(propertyName) };
                case "ClassName": return new[] { "CLASS_NAME", "TEN_LOP_HOC", GetColumnName(propertyName) };
                case "StartDate": return new[] { "START_DATE", "NGAY_BAT_DAU", GetColumnName(propertyName) };
                case "EndDate": return new[] { "END_DATE", "NGAY_KET_THUC", GetColumnName(propertyName) };
                case "MaxSize": return new[] { "MAX_SIZE", "SI_SO_TOI_DA", GetColumnName(propertyName) };
                case "TeacherId": return new[] { "TEACHER_ID", "ID_GIANG_VIEN", GetColumnName(propertyName) };

                // Schedule
                case "ScheduleId": return new[] { "SCHEDULE_ID", "ID_LICH_HOC", GetColumnName(propertyName) };
                case "DayOfWeek": return new[] { "DAY_OF_WEEK", "THU_TRONG_TUAN", GetColumnName(propertyName) };
                case "StartTime": return new[] { "START_TIME", "GIO_BAT_DAU", GetColumnName(propertyName) };
                case "EndTime": return new[] { "END_TIME", "GIO_KET_THUC", GetColumnName(propertyName) };
                case "ScheduleText": return new[] { "SCHEDULE_TEXT", GetColumnName(propertyName) };

                // Registration
                case "RegistrationId": return new[] { "REGISTRATION_ID", "ID_DANG_KY", GetColumnName(propertyName) };
                case "RegistrationCode": return new[] { "REGISTRATION_CODE", "MA_DANG_KY", GetColumnName(propertyName) };
                case "RegistrationDate": return new[] { "REGISTRATION_DATE", "NGAY_DANG_KY", GetColumnName(propertyName) };
                case "Status": return new[] { "STATUS", "TRANG_THAI", GetColumnName(propertyName) };
                case "StudyDate": return new[] { "STUDY_DATE", "NGAY_HOC", GetColumnName(propertyName) };
                case "StaffId": return new[] { "STAFF_ID", "ID_NHAN_VIEN_DUYET", GetColumnName(propertyName) };

                // Account / Role
                case "UserId": return new[] { "USER_ID", "ID_NGUOI_DUNG", GetColumnName(propertyName) };
                case "Username": return new[] { "USERNAME", "TEN_DANG_NHAP", GetColumnName(propertyName) };
                case "Password": return new[] { "PASSWORD", "MAT_KHAU", GetColumnName(propertyName) };
                case "Email": return new[] { "EMAIL", GetColumnName(propertyName) };
                case "Role": return new[] { "ROLE", "TEN_VAI_TRO", GetColumnName(propertyName) };

                default:
                    return new[] { GetColumnName(propertyName) };
            }
        }

        /// <summary>
        /// Chuyển tên property C# sang tên cột Oracle tương ứng (mapping đặc biệt một số thuộc tính).
        /// </summary>
        private string GetColumnName(string propertyName)
        {
            // Map C# property names to Oracle column names
            return propertyName switch
            {
                // HOC_VIEN
                "StudentId" => "ID_HOC_VIEN",
                "FullName" => "HO_TEN",
                "StudentCode" => "MA_HOC_VIEN",
                "Sex" => "GIOI_TINH",
                "DateOfBirth" => "NGAY_SINH",
                "PhoneNumber" => "SO_DIEN_THOAI",
                "Address" => "DIA_CHI",
                // KHOA_HOC
                "CourseId" => "ID_KHOA_HOC",
                "CourseCode" => "MA_KHOA_HOC",
                "CourseName" => "TEN_KHOA_HOC",
                "Description" => "MO_TA",
                "StandardFee" => "HOC_PHI_TIEU_CHUAN",
                // LOP_HOC
                "ClassId" => "ID_LOP_HOC",
                "ClassCode" => "MA_LOP_HOC",
                "ClassName" => "TEN_LOP_HOC",
                "StartDate" => "NGAY_BAT_DAU",
                "EndDate" => "NGAY_KET_THUC",
                "MaxSize" => "SI_SO_TOI_DA",
                "TeacherId" => "ID_GIANG_VIEN",
                // LICH_HOC
                "ScheduleId" => "ID_LICH_HOC",
                "DayOfWeek" => "THU_TRONG_TUAN",
                "StartTime" => "GIO_BAT_DAU",
                "EndTime" => "GIO_KET_THUC",
                // DON_DANG_KY
                "RegistrationId" => "ID_DANG_KY",
                "RegistrationCode" => "REGISTRATION_CODE",
                "RegistrationDate" => "NGAY_DANG_KY",
                "Status" => "TRANG_THAI",
                "StudyDate" => "NGAY_HOC",
                "StaffId" => "ID_NHAN_VIEN_DUYET",
                // ACCOUNT / ROLE (nếu dùng)
                "UserId" => "ID_NGUOI_DUNG",
                "Username" => "TEN_DANG_NHAP",
                "Password" => "MAT_KHAU",
                "Email" => "EMAIL",
                "Role" => "TEN_VAI_TRO",
                _ => propertyName.ToUpper()
            };
        }

        /// <summary>
        /// Kiểm tra reader có chứa cột tên columnName (không phân biệt hoa thường).
        /// </summary>
        private bool HasColumn(OracleDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}