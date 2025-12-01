using Oracle.ManagedDataAccess.Client;
using System.Data;

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

        /// <summary>
        /// Khởi tạo BaseService với cấu hình DB, logger và provider kết nối user (có thể null nếu service không cần per-user).
        /// </summary>
        public BaseService(IConfiguration configuration, ILogger logger, IOracleConnectionProvider? userConnProvider = null)
        {
            _connectionString = configuration.GetConnectionString("OracleDbConnection") ??
                throw new ArgumentNullException("Connection string not found");
            _logger = logger;
            _userConnProvider = userConnProvider;
        }

        /// <summary>
        /// Trả về kết nối theo user (per-user). Không fallback về admin nếu có provider; nếu phiên không hợp lệ sẽ ném lỗi.
        /// </summary>
        public async Task<OracleConnection> GetConnectionAsync()
        {
            if (_userConnProvider != null)
            {
                var userConn = await _userConnProvider.GetUserConnectionAsync();
                await EnsureCurrentSchemaAsync(userConn);
                return userConn;
            }
            // Không có provider (trường hợp đặc biệt) mới dùng admin
            var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureCurrentSchemaAsync(connection);
            return connection;
        }

        /// <summary>
        /// Luôn trả về kết nối admin (bỏ qua cơ chế user). Dùng cho các tác vụ hệ thống hoặc fallback.
        /// </summary>
        public async Task<OracleConnection> GetAdminConnectionAsync()
        {
            var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureCurrentSchemaAsync(connection);
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

        protected async Task<List<T>> ExecuteStoredProcedureQueryAsync<T>(string spName, object? parameters = null) where T : new()
        {
            var results = new List<T>();
            try
            {
                using var connection = await GetConnectionAsync();
                using var command = new OracleCommand(spName, connection);
                command.CommandType = CommandType.StoredProcedure;

                if (parameters != null)
                {
                    AddParameters(command, parameters);
                }

                // Assume output cursor is named p_cursor if not provided?
                // Or just execute reader?
                // If the SP returns a cursor, we need to bind it?
                // Most of my SPs use p_cursor OUT SYS_REFCURSOR.
                // But OracleCommand.ExecuteReaderAsync can automatically pick up the first cursor?
                // No, usually need to bind it.
                // But if I use `ExecuteReader`, I need to bind the RefCursor parameter.
                // I'll assume the caller handles parameters if complex, but for simple query SPs, I need to add p_cursor.
                // I'll add "p_cursor" as Output RefCursor.
                
                var hasCursor = false;
                foreach (OracleParameter p in command.Parameters)
                {
                    if (p.OracleDbType == OracleDbType.RefCursor) hasCursor = true;
                }
                
                if (!hasCursor)
                {
                    command.Parameters.Add("p_cursor", OracleDbType.RefCursor).Direction = ParameterDirection.Output;
                }

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapToObject<T>(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing SP query: {SP}", spName);
                throw;
            }
            return results;
        }

        /// <summary>
        /// Thêm parameters vào OracleCommand: lấy tất cả property của object truyền vào và tạo tham số :propertyname.
        /// Chuyển Null sang DBNull.Value.
        /// </summary>
        protected void AddParameters(OracleCommand command, object parameters)
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
        protected T MapToObject<T>(OracleDataReader reader) where T : new()
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
        protected IEnumerable<string> GetPossibleColumnNames(string propertyName)
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
                case "ClassCode": return new[] { "CLASS_CODE", "MA_LOP_HOC", GetColumnName(propertyName) };
                case "ClassName": return new[] { "CLASS_NAME", "TEN_LOP_HOC", GetColumnName(propertyName) };
                case "StartDate": return new[] { "START_DATE", "NGAY_BAT_DAU", GetColumnName(propertyName) };
                case "EndDate": return new[] { "END_DATE", "NGAY_KET_THUC", GetColumnName(propertyName) };
                case "MaxSize": return new[] { "MAX_SIZE", "SI_SO_TOI_DA", GetColumnName(propertyName) };
                case "TeacherId": return new[] { "TEACHER_ID", "ID_GIANG_VIEN", GetColumnName(propertyName) };
                case "ApprovedCount": return new[] { "APPROVED_COUNT", GetColumnName(propertyName) };
                case "Status": return new[] { "STATUS", "TRANG_THAI", GetColumnName(propertyName) };
                case "StudyDate": return new[] { "STUDY_DATE", "NGAY_HOC", GetColumnName(propertyName) };
                case "StudentName": return new[] { "STUDENT_NAME", "HO_TEN", GetColumnName(propertyName) };
                case "StaffId": return new[] { "STAFF_ID", "ID_NHAN_VIEN_DUYET", GetColumnName(propertyName) };
                case "StandardFee": return new[] { "HOC_PHI_TIEU_CHUAN", "HOC_PHI", GetColumnName(propertyName) };

                // Invoice / Payment
                case "InvoiceId": return new[] { "INVOICE_ID", "ID_HOA_DON", GetColumnName(propertyName) };
                case "InvoiceCode": return new[] { "INVOCE_CODE", "MA_HOA_DON", GetColumnName(propertyName) };
                case "CreatedDate": return new[] { "CREATED_DATE", "NGAY_TAO", GetColumnName(propertyName) };
                case "DueDate": return new[] { "DUE_DATE", "NGAY_HET_HAN", GetColumnName(propertyName) };
                case "Amount": return new[] { "AMOUNT", "SO_TIEN", GetColumnName(propertyName) };
                case "PaymentId": return new[] { "PAYMENT_ID", "ID_THANH_TOAN", GetColumnName(propertyName) };
                case "PaymentDate": return new[] { "PAYMENT_DATE", "NGAY_THANH_TOAN", GetColumnName(propertyName) };
                case "PaymentMethod": return new[] { "PAYMENT_METHOD", "PHUONG_THUC_THANH_TOAN", GetColumnName(propertyName) };
                
                // Signature fields
                case "SignatureBase64": return new[] { "SIGNATURE_BASE64", "CHU_KY_BASE64", GetColumnName(propertyName) };
                case "Algorithm": return new[] { "ALGORITHM", "THUAT_TOAN", GetColumnName(propertyName) };
                case "AccountantId": return new[] { "ACCOUNTANT_ID", "ID_KE_TOAN_KY", GetColumnName(propertyName) };
                case "SignedDate": return new[] { "SIGNED_DATE", "NGAY_KY", GetColumnName(propertyName) };
                case "SignatureImageBase64": return new[] { "SIGNATURE_IMAGE_BASE64", "CHU_KY_HINH_BASE64", GetColumnName(propertyName) };
                case "IsPrinted": return new[] { "IS_PRINTED", "DA_IN", GetColumnName(propertyName) };

                // Account / Role
                case "UserId": return new[] { "USER_ID", "ID_NGUOI_DUNG", GetColumnName(propertyName) };
                case "Username": return new[] { "USERNAME", "TEN_DANG_NHAP", GetColumnName(propertyName) };
                case "Password": return new[] { "PASSWORD", "MAT_KHAU", GetColumnName(propertyName) };
                case "Email": return new[] { "EMAIL", GetColumnName(propertyName) };
                case "Role": return new[] { "ROLE", "TEN_VAI_TRO", GetColumnName(propertyName) };
                case "RoleId": return new[] { "ROLE_ID", "ID_VAI_TRO", GetColumnName(propertyName) };
                case "IsActive": return new[] { "IS_ACTIVE", "TRANG_THAI_KICH_HOAT", GetColumnName(propertyName) };

                default:
                    return new[] { GetColumnName(propertyName) };
            }
        }

        /// <summary>
        /// Chuyển tên property C# sang tên cột Oracle tương ứng (mapping đặc biệt một số thuộc tính).
        /// </summary>
        protected string GetColumnName(string propertyName)
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
                "IsActive" => "TRANG_THAI_KICH_HOAT",
                // INVOICE / PAYMENT
                "InvoiceId" => "ID_HOA_DON",
                "InvoiceCode" => "MA_HOA_DON",
                "CreatedDate" => "NGAY_TAO",
                "DueDate" => "NGAY_HET_HAN",
                "Amount" => "SO_TIEN",
                "PaymentId" => "ID_THANH_TOAN",
                "PaymentDate" => "NGAY_THANH_TOAN",
                "PaymentMethod" => "PHUONG_THUC_THANH_TOAN",
                // Signature fields
                "SignatureBase64" => "CHU_KY_BASE64",
                "Algorithm" => "THUAT_TOAN",
                "AccountantId" => "ID_KE_TOAN_KY",
                "SignedDate" => "NGAY_KY",
                "SignatureImageBase64" => "CHU_KY_HINH_BASE64",
                "IsPrinted" => "DA_IN",
                _ => propertyName.ToUpper()
            };
        }

        /// <summary>
        /// Kiểm tra reader có chứa cột tên columnName (không phân biệt hoa thường).
        /// </summary>
        protected bool HasColumn(OracleDataReader reader, string columnName)
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