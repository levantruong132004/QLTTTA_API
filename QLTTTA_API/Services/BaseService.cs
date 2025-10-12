using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace QLTTTA_API.Services
{
    public interface IBaseService
    {
        Task<OracleConnection> GetConnectionAsync();
        Task<OracleConnection> GetAdminConnectionAsync();
    }

    public class BaseService : IBaseService
    {
        protected readonly string _connectionString;
        private readonly IOracleConnectionProvider? _userConnProvider;
        protected readonly ILogger _logger;

        public BaseService(IConfiguration configuration, ILogger logger, IOracleConnectionProvider? userConnProvider = null)
        {
            _connectionString = configuration.GetConnectionString("OracleDbConnection") ??
                throw new ArgumentNullException("Connection string not found");
            _logger = logger;
            _userConnProvider = userConnProvider;
        }

        public async Task<OracleConnection> GetConnectionAsync()
        {
            if (_userConnProvider != null)
            {
                try
                {
                    return await _userConnProvider.GetUserConnectionAsync();
                }
                catch (UnauthorizedAccessException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to obtain user connection, falling back to admin connection");
                }
            }
            var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

        public async Task<OracleConnection> GetAdminConnectionAsync()
        {
            var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

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

        // Admin-connection variants to bypass per-user DB permissions
        protected async Task<List<T>> ExecuteQueryAdminAsync<T>(string sql, object? parameters = null,
            Func<OracleDataReader, T>? mapper = null) where T : new()
        {
            var results = new List<T>();
            try
            {
                using var connection = await GetAdminConnectionAsync();
                using var command = new OracleCommand(sql, connection);
                if (parameters != null)
                {
                    AddParameters(command, parameters);
                }
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(mapper != null ? mapper(reader) : MapToObject<T>(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing admin query: {SQL}", sql);
                throw;
            }
            return results;
        }

        protected async Task<T?> ExecuteQuerySingleAdminAsync<T>(string sql, object? parameters = null,
            Func<OracleDataReader, T>? mapper = null) where T : class, new()
        {
            try
            {
                using var connection = await GetAdminConnectionAsync();
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
                _logger.LogError(ex, "Error executing single admin query: {SQL}", sql);
                throw;
            }
            return null;
        }

        protected async Task<int> ExecuteNonQueryAdminAsync(string sql, object? parameters = null)
        {
            try
            {
                using var connection = await GetAdminConnectionAsync();
                using var command = new OracleCommand(sql, connection);
                if (parameters != null)
                {
                    AddParameters(command, parameters);
                }
                return await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing admin non-query: {SQL}", sql);
                throw;
            }
        }

        protected async Task<object?> ExecuteScalarAdminAsync(string sql, object? parameters = null)
        {
            try
            {
                using var connection = await GetAdminConnectionAsync();
                using var command = new OracleCommand(sql, connection);
                if (parameters != null)
                {
                    AddParameters(command, parameters);
                }
                return await command.ExecuteScalarAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing admin scalar: {SQL}", sql);
                throw;
            }
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

        private void AddParameters(OracleCommand command, object parameters)
        {
            var properties = parameters.GetType().GetProperties();
            foreach (var property in properties)
            {
                var value = property.GetValue(parameters);
                command.Parameters.Add($":{property.Name.ToLower()}", value ?? DBNull.Value);
            }
        }

        private T MapToObject<T>(OracleDataReader reader) where T : new()
        {
            var obj = new T();
            var properties = typeof(T).GetProperties();

            foreach (var property in properties)
            {
                try
                {
                    // Try alias/camelCase first, then English, then Vietnamese column names
                    foreach (var candidate in GetColumnCandidates(property.Name))
                    {
                        if (!TryGetColumnOrdinal(reader, candidate, out var ord) || reader.IsDBNull(ord))
                            continue;

                        var value = reader.GetValue(ord);

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
                        // mapped successfully, move to next property
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error mapping property {PropertyName}", property.Name);
                }
            }

            return obj;
        }
        
        private IEnumerable<string> GetColumnCandidates(string propertyName)
        {
            // Prefer alias/property name first, then English, then Vietnamese counterparts
            yield return propertyName; // alias like "StudentId"
            foreach (var alt in GetEnglishAndVietnameseNames(propertyName))
                yield return alt;
            yield return propertyName.ToUpper();
        }

        private IEnumerable<string> GetEnglishAndVietnameseNames(string propertyName)
        {
            return propertyName switch
            {
                // Student
                "StudentId" => new[] { "STUDENT_ID", "ID_HOC_VIEN" },
                "FullName" => new[] { "FULL_NAME", "HO_TEN" },
                "StudentCode" => new[] { "STUDENT_CODE", "MA_HOC_VIEN" },
                "Sex" => new[] { "SEX", "GIOI_TINH" },
                "DateOfBirth" => new[] { "DATE_OF_BIRTH", "NGAY_SINH" },
                "PhoneNumber" => new[] { "PHONE_NUMBER", "SO_DIEN_THOAI" },
                "Address" => new[] { "ADDRESS", "DIA_CHI" },
                // Account
                "UserId" => new[] { "USER_ID", "ID_NGUOI_DUNG" },
                "Username" => new[] { "USERNAME", "TEN_DANG_NHAP" },
                "Password" => new[] { "PASSWORD", "MAT_KHAU" },
                "Email" => new[] { "EMAIL" },
                "Role" => new[] { "ROLE", "TEN_VAI_TRO" },
                // Course
                "CourseId" => new[] { "COURSE_ID", "ID_KHOA_HOC" },
                "CourseCode" => new[] { "COURSE_CODE", "MA_KHOA_HOC" },
                "CourseName" => new[] { "COURSE_NAME", "TEN_KHOA_HOC" },
                "StandardFee" => new[] { "STANDARD_FEE", "HOC_PHI_TIEU_CHUAN" },
                _ => Array.Empty<string>()
            };
        }

        private bool HasColumn(OracleDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private bool TryGetColumnOrdinal(OracleDataReader reader, string columnName, out int ordinal)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    ordinal = i;
                    return true;
                }
            }
            ordinal = -1;
            return false;
        }
    }
}