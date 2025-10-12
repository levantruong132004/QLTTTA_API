using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;

namespace QLTTTA_API.Services
{
    public interface IProfileService
    {
        Task<StudentProfileDto?> GetMyProfileAsync();
        Task<bool> UpdateMyProfileAsync(StudentProfileUpdateDto dto);
        Task<List<Course>> GetAllCoursesAsync();
    }

    public class ProfileService : BaseService, IProfileService
    {
        public ProfileService(IConfiguration configuration, ILogger<ProfileService> logger, IOracleConnectionProvider userConnProvider)
            : base(configuration, logger, userConnProvider) { }

        public async Task<StudentProfileDto?> GetMyProfileAsync()
        {
            const string sql = "SELECT HO_TEN, MA_HOC_VIEN, GIOI_TINH, NGAY_SINH, SO_DIEN_THOAI, DIA_CHI, EMAIL FROM QLTT_ADMIN.V_THONGTIN_CANHAN_HV";
            using var conn = await GetConnectionAsync();
            using var cmd = new OracleCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new StudentProfileDto
                {
                    HoTen = reader.IsDBNull(0) ? null : reader.GetString(0),
                    MaHocVien = reader.IsDBNull(1) ? null : reader.GetString(1),
                    GioiTinh = reader.IsDBNull(2) ? null : reader.GetString(2),
                    NgaySinh = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                    SoDienThoai = reader.IsDBNull(4) ? null : reader.GetString(4),
                    DiaChi = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Email = reader.IsDBNull(6) ? null : reader.GetString(6)
                };
            }
            return null;
        }

        public async Task<bool> UpdateMyProfileAsync(StudentProfileUpdateDto dto)
        {
            // Theo yêu cầu: chỉ cho phép HV tự cập nhật SỐ ĐIỆN THOẠI và ĐỊA CHỈ thông qua quyền UPDATE trên view của mình
            const string sql = @"UPDATE QLTT_ADMIN.V_THONGTIN_CANHAN_HV
SET SO_DIEN_THOAI = :p_sdt, DIA_CHI = :p_diachi
WHERE UPPER(TEN_DANG_NHAP) = USER";
            using var conn = await GetConnectionAsync();
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":p_sdt", OracleDbType.Varchar2).Value = (object?)dto.SoDienThoai ?? DBNull.Value;
            cmd.Parameters.Add(":p_diachi", OracleDbType.NVarchar2).Value = (object?)dto.DiaChi ?? DBNull.Value;
            var affected = await cmd.ExecuteNonQueryAsync();
            return affected >= 1;
        }

        public async Task<List<Course>> GetAllCoursesAsync()
        {
            // Dùng view công khai theo yêu cầu
            const string sql = "SELECT MA_KHOA_HOC, TEN_KHOA_HOC, MO_TA, HOC_PHI_TIEU_CHUAN FROM QLTT_ADMIN.V_DANHSACH_KHOAHOC ORDER BY TEN_KHOA_HOC";
            // theo yêu cầu, hiển thị không lộ ID ra web; nhưng API vẫn có thể lấy đầy đủ
            async Task<List<Course>> ReadAsync(Oracle.ManagedDataAccess.Client.OracleConnection conn)
            {
                using var cmd = new OracleCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                var list = new List<Course>();
                while (await reader.ReadAsync())
                {
                    var feeObj = reader.IsDBNull(3) ? null : reader.GetValue(3);
                    int fee = 0;
                    if (feeObj != null)
                    {
                        if (feeObj is decimal dec) fee = (int)dec;
                        else if (feeObj is int i) fee = i;
                        else if (int.TryParse(feeObj.ToString(), out var parsed)) fee = parsed;
                    }
                    list.Add(new Course
                    {
                        CourseId = 0, // ẩn ID
                        CourseCode = reader.IsDBNull(0) ? null : reader.GetString(0),
                        CourseName = reader.IsDBNull(1) ? null : reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        StandardFee = fee
                    });
                }
                return list;
            }

            try
            {
                using var conn = await GetConnectionAsync();
                return await ReadAsync(conn);
            }
            catch (UnauthorizedAccessException)
            {
                // Khi API restart, cache phiên user mất -> fallback admin cho danh sách công khai này
                using var conn = await GetAdminConnectionAsync();
                return await ReadAsync(conn);
            }
        }
    }
}
