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
                    // Map C# property names to Oracle column names
                    var columnName = GetColumnName(property.Name);

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
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error mapping property {PropertyName}", property.Name);
                }
            }

            return obj;
        }

        private string GetColumnName(string propertyName)
        {
            // Map C# property names to Oracle column names
            return propertyName switch
            {
                "StudentId" => "STUDENT_ID",
                "FullName" => "FULL_NAME",
                "StudentCode" => "STUDENT_CODE",
                "Sex" => "SEX",
                "DateOfBirth" => "DATE_OF_BIRTH",
                "PhoneNumber" => "PHONE_NUMBER",
                "Address" => "ADDRESS",
                "CourseId" => "COURSE_ID",
                "CourseName" => "COURSE_NAME",
                "Description" => "DESCRIPTION",
                "UserId" => "USER_ID",
                "Username" => "USERNAME",
                "Password" => "PASSWORD",
                "Email" => "EMAIL",
                "Role" => "ROLE",
                _ => propertyName.ToUpper()
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
    }
}