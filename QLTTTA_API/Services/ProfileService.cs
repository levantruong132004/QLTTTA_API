using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;

namespace QLTTTA_API.Services
{
    /// <summary>
    /// Interface dịch vụ hồ sơ học viên: xem thông tin cá nhân, cập nhật cho phép và lấy danh sách khóa học công khai.
    /// </summary>
    public interface IProfileService
    {
        /// <summary>Lấy thông tin cá nhân của học viên đang đăng nhập từ view V_THONGTIN_CANHAN_HV (đã lọc theo USER).</summary>
        Task<StudentProfileDto?> GetMyProfileAsync();
        /// <summary>Cập nhật SỐ ĐIỆN THOẠI và ĐỊA CHỈ cho học viên đang đăng nhập qua view (quyền UPDATE đã cấp).</summary>
        Task<bool> UpdateMyProfileAsync(StudentProfileUpdateDto dto);
        /// <summary>Lấy danh sách khóa học công khai từ view V_DANHSACH_KHOAHOC (ẩn ID khi trả ra web).</summary>
        Task<List<Course>> GetAllCoursesAsync();
        /// <summary>Gửi yêu cầu đăng ký khóa học (hệ thống/sinh viên không tự chọn lớp; nhân viên sẽ sắp xếp lớp sau).</summary>
        Task<(bool Success, string Message)> SubmitCourseRegistrationRequestAsync(string courseCode);
        Task<List<QLTTTA_API.Models.DTOs.OpenClassItem>> GetOpenClassesByCourseAsync(string courseCode);
        Task<(bool Success, string Message)> RegisterToClassAsync(int classId, int? studentId = null);
    }

    /// <summary>
    /// Triển khai IProfileService sử dụng cơ chế per-user connection của BaseService để bảo đảm truy vấn chạy dưới quyền học viên.
    /// </summary>
    public class ProfileService : BaseService, IProfileService
    {
        public ProfileService(IConfiguration configuration, ILogger<ProfileService> logger, IOracleConnectionProvider userConnProvider)
            : base(configuration, logger, userConnProvider) { }

        /// <summary>
        /// Truy vấn thông tin cá nhân của học viên đang đăng nhập. View tự giới hạn theo USER nên không cần WHERE bổ sung.
        /// </summary>
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

        /// <summary>
        /// Cập nhật 2 trường: SỐ ĐIỆN THOẠI và ĐỊA CHỈ thông qua quyền UPDATE đã cấp trên view.
        /// Các trường khác bị cố tình bỏ qua để tránh chỉnh sửa ngoài phạm vi cho phép.
        /// </summary>
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

        /// <summary>
        /// Lấy danh sách khóa học công khai. Nếu phiên user hết hạn (cache mất) vẫn fallback dùng kết nối admin để không gián đoạn.
        /// Trả về Course với CourseId = 0 (ẩn ID) cho phía web.
        /// </summary>
        public async Task<List<Course>> GetAllCoursesAsync()
        {
            // Dùng VIEW đã được cấp quyền cho Học viên (role_hocvien)
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
            catch (UnauthorizedAccessException) // Mất session user -> fallback admin để vẫn trả dữ liệu công khai
            {
                // Khi API restart, cache phiên user mất -> fallback admin cho danh sách công khai này
                using var conn = await GetAdminConnectionAsync();
                return await ReadAsync(conn);
            }
            catch (Oracle.ManagedDataAccess.Client.OracleException oex) when (oex.Number == 1031)
            {
                // ORA-01031: thiếu quyền xem bảng/góc nhìn -> fallback admin để không chặn hiển thị
                using var conn = await GetAdminConnectionAsync();
                return await ReadAsync(conn);
            }
        }

        /// <summary>
        /// Gọi thủ tục SP_GUI_YEU_CAU_DANG_KY_KHOA dưới quyền học viên hiện tại để tạo yêu cầu đăng ký khóa học.
        /// Thủ tục dự kiến: nhận COURSE_CODE và trả OUT NVARCHAR2 kết quả.
        /// </summary>
        public async Task<(bool Success, string Message)> SubmitCourseRegistrationRequestAsync(string courseCode)
        {
            if (string.IsNullOrWhiteSpace(courseCode))
                return (false, "Mã khóa học không hợp lệ");

            try
            {
                using var conn = await GetConnectionAsync();
                using var cmd = new OracleCommand("SP_GUI_YEU_CAU_DANG_KY_KHOA", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    BindByName = true
                };
                cmd.Parameters.Add("p_course_code", OracleDbType.Varchar2).Value = courseCode.Trim();
                var outMsg = new OracleParameter("p_ket_qua", OracleDbType.NVarchar2, 4000)
                {
                    Direction = System.Data.ParameterDirection.Output
                };
                cmd.Parameters.Add(outMsg);

                await cmd.ExecuteNonQueryAsync();
                var msg = outMsg.Value?.ToString() ?? "";
                var ok = !string.IsNullOrWhiteSpace(msg) ? msg.Contains("thành công", StringComparison.OrdinalIgnoreCase) : true;
                return (ok, string.IsNullOrWhiteSpace(msg) ? (ok ? "Gửi yêu cầu thành công" : "Gửi yêu cầu thất bại") : msg);
            }
            catch (Oracle.ManagedDataAccess.Client.OracleException oex) when (oex.Number == 1031)
            {
                // ORA-01031: thiếu quyền -> không phải nhân viên (nhưng ở đây là học viên) hoặc thiếu EXECUTE thủ tục
                _logger.LogWarning(oex, "Insufficient privileges executing SP_GUI_YEU_CAU_DANG_KY_KHOA");
                return (false, "Bạn không có quyền gửi yêu cầu đăng ký. Liên hệ quản trị.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting course registration request for {CourseCode}", courseCode);
                return (false, "Có lỗi xảy ra khi gửi yêu cầu đăng ký");
            }
        }

        public async Task<List<QLTTTA_API.Models.DTOs.OpenClassItem>> GetOpenClassesByCourseAsync(string courseCode)
        {
            var sql = @"SELECT lh.ID_LOP_HOC,
                               lh.MA_LOP_HOC,
                               lh.TEN_LOP_HOC,
                               lh.SI_SO_TOI_DA,
                               kh.TEN_KHOA_HOC,
                                                             (
                                                                 SELECT LISTAGG(
                                                                                        TO_NCHAR('Thứ ') || TO_NCHAR(l2.THU_TRONG_TUAN) || TO_NCHAR(' ')
                                                                                        || TO_NCHAR(l2.GIO_BAT_DAU) || TO_NCHAR('-') || TO_NCHAR(l2.GIO_KET_THUC),
                                                                                        TO_NCHAR('; ')
                                                                                ) WITHIN GROUP (ORDER BY l2.THU_TRONG_TUAN, l2.GIO_BAT_DAU)
                                                                 FROM LICH_HOC l2 WHERE l2.ID_LOP_HOC = lh.ID_LOP_HOC
                                                             ) AS SCHEDULE_TEXT
                        FROM LOP_HOC lh
                        JOIN KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
                        WHERE kh.MA_KHOA_HOC = :code AND lh.TRANG_THAI = 'Đang tuyển sinh'
                        ORDER BY lh.ID_LOP_HOC DESC";
            try
            {
                return await ExecuteQueryAsync<QLTTTA_API.Models.DTOs.OpenClassItem>(sql, new { code = courseCode });
            }
            catch (UnauthorizedAccessException)
            {
                using var conn = await GetAdminConnectionAsync();
                using var cmd = new Oracle.ManagedDataAccess.Client.OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(":code", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2).Value = courseCode;
                using var reader = await cmd.ExecuteReaderAsync();
                var list = new List<QLTTTA_API.Models.DTOs.OpenClassItem>();
                while (await reader.ReadAsync())
                {
                    list.Add(new QLTTTA_API.Models.DTOs.OpenClassItem
                    {
                        ClassId = reader.GetInt32(0),
                        ClassCode = reader.GetString(1),
                        ClassName = reader.GetString(2),
                        MaxSize = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                        CourseName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        ScheduleText = reader.IsDBNull(5) ? null : reader.GetString(5)
                    });
                }
                return list;
            }
            catch (Oracle.ManagedDataAccess.Client.OracleException oex) when (oex.Number == 1031 || oex.Number == 942)
            {
                using var conn = await GetAdminConnectionAsync();
                using var cmd = new Oracle.ManagedDataAccess.Client.OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(":code", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2).Value = courseCode;
                using var reader = await cmd.ExecuteReaderAsync();
                var list = new List<QLTTTA_API.Models.DTOs.OpenClassItem>();
                while (await reader.ReadAsync())
                {
                    list.Add(new QLTTTA_API.Models.DTOs.OpenClassItem
                    {
                        ClassId = reader.GetInt32(0),
                        ClassCode = reader.GetString(1),
                        ClassName = reader.GetString(2),
                        MaxSize = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                        CourseName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        ScheduleText = reader.IsDBNull(5) ? null : reader.GetString(5)
                    });
                }
                return list;
            }
        }

        public async Task<(bool Success, string Message)> RegisterToClassAsync(int classId, int? studentId = null)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                // Xác định ID học viên
                int hvId;
                if (studentId.HasValue && studentId.Value > 0)
                {
                    hvId = studentId.Value;
                }
                else
                {
                    using var cmd = new Oracle.ManagedDataAccess.Client.OracleCommand("SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE UPPER(TEN_DANG_NHAP)=USER", conn);
                    var obj = await cmd.ExecuteScalarAsync();
                    if (obj == null || obj == DBNull.Value)
                        return (false, "Không xác định được tài khoản học viên");
                    hvId = Convert.ToInt32(obj);
                }
                var sql = @"INSERT INTO DON_DANG_KY (MA_DANG_KY, NGAY_DANG_KY, TRANG_THAI, ID_HOC_VIEN, ID_LOP_HOC)
                            VALUES ('DGK_'||TO_CHAR(SYSDATE,'YYYYMMDDHH24MISS')||'_'||TRUNC(DBMS_RANDOM.VALUE(1000,9999)), SYSDATE, 'Chờ duyệt', :hv, :cid)";
                await ExecuteNonQueryAsync(sql, new { hv = hvId, cid = classId });
                return (true, "Đăng ký thành công, vui lòng chờ duyệt");
            }
            catch (Oracle.ManagedDataAccess.Client.OracleException oex) when (oex.Number == 1)
            {
                return (false, "Bạn đã có đơn cho lớp này");
            }
            catch (Oracle.ManagedDataAccess.Client.OracleException oex) when (oex.Number == 2291)
            {
                // ORA-02291: integrity constraint violated - parent key not found
                return (false, "Không tìm thấy học viên hoặc lớp học (ràng buộc khóa ngoại)");
            }
            catch (Oracle.ManagedDataAccess.Client.OracleException oex) when (oex.Number == 1031)
            {
                // ORA-01031: insufficient privileges
                return (false, "Bạn không có quyền đăng ký lớp. Vui lòng liên hệ quản trị.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RegisterToClass failed for class {ClassId}", classId);
                return (false, "Có lỗi xảy ra khi đăng ký lớp");
            }
        }
    }
}
