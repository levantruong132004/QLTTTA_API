using Microsoft.AspNetCore.Http;
using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;
using System.Data;

namespace QLTTTA_API.Services
{
    public interface IStaffService
    {
        Task<List<StudentBrief>> SearchStudentsAsync(string? keyword);
        Task<ApiResponse<bool>> LockStudentAsync(int userId);
        Task<ApiResponse<bool>> UnlockStudentAsync(int userId);
        Task<List<Registration>> GetStudentRegistrationsAsync(int userId);
        Task<List<InvoiceBrief>> GetStudentInvoicesAsync(int userId);
    }

    public class StaffService : BaseService, IStaffService
    {
        private readonly IHttpContextAccessor _http;
        public StaffService(IConfiguration cfg, ILogger<StaffService> logger, IOracleConnectionProvider userConnProvider, IHttpContextAccessor http)
            : base(cfg, logger, userConnProvider)
        {
            _http = http;
        }

        public async Task<List<StudentBrief>> SearchStudentsAsync(string? keyword)
        {
            using var conn = await GetAdminConnectionAsync();
            using var cmd = new OracleCommand("SP_STAFF_SEARCH_STUDENTS", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("p_keyword", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(keyword) ? DBNull.Value : $"%{keyword.Trim()}%";
            cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor).Direction = ParameterDirection.Output;
            
            var list = new List<StudentBrief>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapToObject<StudentBrief>(reader));
            }
            return list;
        }

        public async Task<ApiResponse<bool>> LockStudentAsync(int userId)
        {
            using var conn = await GetAdminConnectionAsync();
            using var cmd = new OracleCommand("SP_LOCK_ACCOUNT", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = userId;
            var pRows = cmd.Parameters.Add("p_rows", OracleDbType.Int32);
            pRows.Direction = ParameterDirection.Output;
            await cmd.ExecuteNonQueryAsync();
            
            bool success = Convert.ToInt32(pRows.Value.ToString()) > 0;
            return new ApiResponse<bool> { Success = success, Message = success ? "Đã khóa tài khoản" : "Không tìm thấy tài khoản", Data = success };
        }

        public async Task<ApiResponse<bool>> UnlockStudentAsync(int userId)
        {
            using var conn = await GetAdminConnectionAsync();
            using var cmd = new OracleCommand("SP_UNLOCK_ACCOUNT", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = userId;
            var pRows = cmd.Parameters.Add("p_rows", OracleDbType.Int32);
            pRows.Direction = ParameterDirection.Output;
            await cmd.ExecuteNonQueryAsync();
            
            bool success = Convert.ToInt32(pRows.Value.ToString()) > 0;
            return new ApiResponse<bool> { Success = success, Message = success ? "Đã mở khóa tài khoản" : "Không tìm thấy tài khoản", Data = success };
        }

        public async Task<List<Registration>> GetStudentRegistrationsAsync(int userId)
        {
            using var conn = await GetAdminConnectionAsync();
            using var cmd = new OracleCommand("SP_GET_STUDENT_REGISTRATIONS", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = userId;
            cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor).Direction = ParameterDirection.Output;
            
            var list = new List<Registration>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapToObject<Registration>(reader));
            }
            return list;
        }

        public async Task<List<InvoiceBrief>> GetStudentInvoicesAsync(int userId)
        {
            using var conn = await GetAdminConnectionAsync();
            using var cmd = new OracleCommand("SP_GET_STUDENT_INVOICES", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = userId;
            cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor).Direction = ParameterDirection.Output;
            
            var list = new List<InvoiceBrief>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapToObject<InvoiceBrief>(reader));
            }
            return list;
        }
    }

    public class StudentBrief
    {
        public int StudentId { get; set; }
        public string? StudentCode { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; }
        public int IdHocVien { set { StudentId = value; } } // Map
        public string MaHocVien { set { StudentCode = value; } } // Map
        public string HoTen { set { FullName = value; } } // Map
        public string SoDienThoai { set { PhoneNumber = value; } } // Map
        public int Active { set { IsActive = value == 1; } } // Map
    }

    public class InvoiceBrief
    {
        public int InvoiceId { get; set; }
        public string? InvoiceCode { get; set; }
        public string? Status { get; set; }
        public int RegistrationId { get; set; }
        public string? CourseName { get; set; }
        public int IdHoaDon { set { InvoiceId = value; } } // Map
        public string MaHoaDon { set { InvoiceCode = value; } } // Map
        public string TrangThai { set { Status = value; } } // Map
        public int IdDangKy { set { RegistrationId = value; } } // Map
        public string TenKhoaHoc { set { CourseName = value; } } // Map
    }
}
