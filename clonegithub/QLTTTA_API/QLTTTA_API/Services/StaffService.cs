using Microsoft.AspNetCore.Http;
using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;

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

        private async Task TagClientAsync(OracleConnection conn, string roleLabel)
        {
            try
            {
                using var setId = new OracleCommand("BEGIN DBMS_SESSION.SET_IDENTIFIER(USER); DBMS_APPLICATION_INFO.SET_CLIENT_INFO(:i); END;", conn) { BindByName = true };
                setId.Parameters.Add(":i", OracleDbType.Varchar2).Value = $"role={roleLabel};user=" + (await new OracleCommand("SELECT USER FROM DUAL", conn).ExecuteScalarAsync())?.ToString();
                await setId.ExecuteNonQueryAsync();
            }
            catch { }
        }

        public async Task<List<StudentBrief>> SearchStudentsAsync(string? keyword)
        {
            using var conn = await GetConnectionAsync();
            await TagClientAsync(conn, "staff");
            var where = string.Empty;
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                where = " WHERE UPPER(hv.HO_TEN) LIKE UPPER(:kw) OR UPPER(hv.MA_HOC_VIEN) LIKE UPPER(:kw) OR UPPER(tk.EMAIL) LIKE UPPER(:kw) OR UPPER(tk.TEN_DANG_NHAP) LIKE UPPER(:kw) OR hv.SO_DIEN_THOAI LIKE :kw";
            }
            var sql = $@"SELECT hv.ID_HOC_VIEN, hv.MA_HOC_VIEN, hv.HO_TEN, tk.EMAIL, hv.SO_DIEN_THOAI, NVL(tk.TRANG_THAI_KICH_HOAT,0) AS ACTIVE
                          FROM QLTT_ADMIN.HOC_VIEN hv
                          LEFT JOIN QLTT_ADMIN.TAI_KHOAN tk ON tk.ID_NGUOI_DUNG = hv.ID_HOC_VIEN{where}
                          ORDER BY hv.ID_HOC_VIEN DESC";
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            if (!string.IsNullOrWhiteSpace(keyword)) cmd.Parameters.Add(":kw", OracleDbType.Varchar2).Value = $"%{keyword.Trim()}%";
            var list = new List<StudentBrief>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new StudentBrief
                {
                    StudentId = r.GetInt32(0),
                    StudentCode = r.IsDBNull(1) ? null : r.GetString(1),
                    FullName = r.IsDBNull(2) ? null : r.GetString(2),
                    Email = r.IsDBNull(3) ? null : r.GetString(3),
                    PhoneNumber = r.IsDBNull(4) ? null : r.GetString(4),
                    IsActive = !r.IsDBNull(5) && r.GetInt32(5) == 1
                });
            }
            return list;
        }

        public async Task<ApiResponse<bool>> LockStudentAsync(int userId)
        {
            using var conn = await GetConnectionAsync();
            await TagClientAsync(conn, "staff");
            using var cmd = new OracleCommand("UPDATE QLTT_ADMIN.TAI_KHOAN SET TRANG_THAI_KICH_HOAT = 0 WHERE ID_NGUOI_DUNG = :id", conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = userId;
            var affected = await cmd.ExecuteNonQueryAsync();
            return new ApiResponse<bool> { Success = affected > 0, Message = affected > 0 ? "Đã khóa tài khoản" : "Không tìm thấy tài khoản", Data = affected > 0 };
        }

        public async Task<ApiResponse<bool>> UnlockStudentAsync(int userId)
        {
            using var conn = await GetConnectionAsync();
            await TagClientAsync(conn, "staff");
            using var cmd = new OracleCommand("UPDATE QLTT_ADMIN.TAI_KHOAN SET TRANG_THAI_KICH_HOAT = 1 WHERE ID_NGUOI_DUNG = :id", conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = userId;
            var affected = await cmd.ExecuteNonQueryAsync();
            return new ApiResponse<bool> { Success = affected > 0, Message = affected > 0 ? "Đã mở khóa tài khoản" : "Không tìm thấy tài khoản", Data = affected > 0 };
        }

        public async Task<List<Registration>> GetStudentRegistrationsAsync(int userId)
        {
            using var conn = await GetConnectionAsync();
            await TagClientAsync(conn, "staff");
            var sql = @"SELECT dk.ID_DANG_KY AS REGISTRATION_ID,
                                dk.MA_DANG_KY AS REGISTRATION_CODE,
                                dk.NGAY_DANG_KY AS REGISTRATION_DATE,
                                dk.TRANG_THAI   AS STATUS,
                                NULL            AS STUDY_DATE,
                                dk.ID_HOC_VIEN  AS STUDENT_ID,
                                dk.ID_LOP_HOC   AS CLASS_ID,
                                NVL(dk.ID_NHAN_VIEN_DUYET,0) AS STAFF_ID,
                                lh.TEN_LOP_HOC  AS TEN_LOP_HOC,
                                kh.TEN_KHOA_HOC AS TEN_KHOA_HOC
                        FROM QLTT_ADMIN.DON_DANG_KY dk
                        JOIN QLTT_ADMIN.LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
                        JOIN QLTT_ADMIN.KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
                         WHERE dk.ID_HOC_VIEN = :id ORDER BY dk.ID_DANG_KY DESC";
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = userId;
            var list = new List<Registration>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new Registration
                {
                    RegistrationId = r.GetInt32(0),
                    RegistrationCode = r.IsDBNull(1) ? null : r.GetString(1),
                    RegistrationDate = r.IsDBNull(2) ? null : r.GetDateTime(2),
                    Status = r.IsDBNull(3) ? null : r.GetString(3),
                    StudyDate = null,
                    StudentId = r.GetInt32(5),
                    ClassId = r.GetInt32(6),
                    StaffId = r.GetInt32(7),
                    ClassName = r.IsDBNull(8) ? null : r.GetString(8),
                    CourseName = r.IsDBNull(9) ? null : r.GetString(9)
                });
            }
            return list;
        }

        public async Task<List<InvoiceBrief>> GetStudentInvoicesAsync(int userId)
        {
            using var conn = await GetConnectionAsync();
            await TagClientAsync(conn, "staff");
            var sql = @"SELECT hd.ID_HOA_DON, hd.MA_HOA_DON, hd.TRANG_THAI, dk.ID_DANG_KY, kh.TEN_KHOA_HOC
                         FROM QLTT_ADMIN.HOA_DON hd
                         JOIN QLTT_ADMIN.DON_DANG_KY dk ON dk.ID_DANG_KY = hd.ID_DANG_KY
                         JOIN QLTT_ADMIN.LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
                         JOIN QLTT_ADMIN.KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
                        WHERE dk.ID_HOC_VIEN = :id ORDER BY hd.ID_HOA_DON DESC";
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = userId;
            var list = new List<InvoiceBrief>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new InvoiceBrief
                {
                    InvoiceId = r.GetInt32(0),
                    InvoiceCode = r.IsDBNull(1) ? null : r.GetString(1),
                    Status = r.IsDBNull(2) ? null : r.GetString(2),
                    RegistrationId = r.GetInt32(3),
                    CourseName = r.IsDBNull(4) ? null : r.GetString(4)
                });
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
    }

    public class InvoiceBrief
    {
        public int InvoiceId { get; set; }
        public string? InvoiceCode { get; set; }
        public string? Status { get; set; }
        public int RegistrationId { get; set; }
        public string? CourseName { get; set; }
    }
}
