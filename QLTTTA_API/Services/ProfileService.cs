using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;
using Microsoft.AspNetCore.Http;

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
        Task<List<StudentRegistrationWithInvoiceDto>> GetMyRegistrationsWithInvoiceAsync();
        Task<StudentRegistrationDetailDto?> GetRegistrationDetailAsync(int registrationId);
    }

    /// <summary>
    /// Triển khai IProfileService sử dụng cơ chế per-user connection của BaseService để bảo đảm truy vấn chạy dưới quyền học viên.
    /// </summary>
    public class ProfileService : BaseService, IProfileService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public ProfileService(IConfiguration configuration, ILogger<ProfileService> logger, IOracleConnectionProvider userConnProvider, IHttpContextAccessor httpContextAccessor)
            : base(configuration, logger, userConnProvider)
        {
            _httpContextAccessor = httpContextAccessor;
        }

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
            const string sql = "SELECT MA_KHOA_HOC, TEN_KHOA_HOC, MO_TA, HOC_PHI_TIEU_CHUAN FROM QLTT_ADMIN.V_DANHSACH_KHOAHOC ORDER BY TEN_KHOA_HOC";
            using var conn = await GetConnectionAsync();
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
                    CourseId = 0,
                    CourseCode = reader.IsDBNull(0) ? null : reader.GetString(0),
                    CourseName = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    StandardFee = fee
                });
            }
            return list;
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
                                                                 FROM QLTT_ADMIN.LICH_HOC l2 WHERE l2.ID_LOP_HOC = lh.ID_LOP_HOC
                                                             ) AS SCHEDULE_TEXT
                        FROM QLTT_ADMIN.LOP_HOC lh
                        JOIN QLTT_ADMIN.KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
                        WHERE kh.MA_KHOA_HOC = :code AND lh.TRANG_THAI = 'Đang tuyển sinh'
                        ORDER BY lh.ID_LOP_HOC DESC";
            return await ExecuteQueryAsync<QLTTTA_API.Models.DTOs.OpenClassItem>(sql, new { code = courseCode });
        }

        public async Task<(bool Success, string Message)> RegisterToClassAsync(int classId, int? studentId = null)
        {
            try
            {
                int hvId;
                using var userConn = await GetConnectionAsync();
                if (studentId.HasValue && studentId.Value > 0)
                {
                    hvId = studentId.Value;
                }
                else
                {
                    using var cmdUser = new OracleCommand("SELECT ID_NGUOI_DUNG FROM QLTT_ADMIN.TAI_KHOAN WHERE UPPER(TEN_DANG_NHAP)=USER", userConn);
                    var obj = await cmdUser.ExecuteScalarAsync();
                    if (obj != null && obj != DBNull.Value)
                    {
                        hvId = Convert.ToInt32(obj);
                    }
                    else
                    {
                        return (false, "Không xác định được học viên hiện tại");
                    }
                }
                var sql = @"INSERT INTO QLTT_ADMIN.DON_DANG_KY (MA_DANG_KY, NGAY_DANG_KY, TRANG_THAI, ID_HOC_VIEN, ID_LOP_HOC)
                            VALUES ('DGK_'||TO_CHAR(SYSDATE,'YYYYMMDDHH24MISS')||'_'||TRUNC(DBMS_RANDOM.VALUE(1000,9999)), SYSDATE, N'Chờ duyệt', :hv, :cid)";
                using var cmd = new OracleCommand(sql, userConn) { BindByName = true };
                cmd.Parameters.Add(":hv", OracleDbType.Int32).Value = hvId;
                cmd.Parameters.Add(":cid", OracleDbType.Int32).Value = classId;
                await cmd.ExecuteNonQueryAsync();
                return (true, "Đăng ký thành công, vui lòng chờ duyệt");
            }
            catch (OracleException oex) when (oex.Number == 1)
            {
                return (false, "Bạn đã có đơn cho lớp này");
            }
            catch (OracleException oex) when (oex.Number == 2291)
            {
                return (false, "Không tìm thấy học viên hoặc lớp học (ràng buộc khóa ngoại)");
            }
            catch (OracleException oex) when (oex.Number == 12899)
            {
                return (false, "Mã đơn đăng ký vượt quá độ dài cho phép. Vui lòng thử lại");
            }
            catch (OracleException oex)
            {
                _logger.LogError(oex, "RegisterToClass failed for class {ClassId}", classId);
                return (false, $"Lỗi Oracle {oex.Number}: {oex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RegisterToClass failed for class {ClassId}", classId);
                return (false, "Có lỗi xảy ra khi đăng ký lớp");
            }
        }

        // Lấy danh sách đăng ký của học viên kèm thông tin hóa đơn và chữ ký số
        public async Task<List<StudentRegistrationWithInvoiceDto>> GetMyRegistrationsWithInvoiceAsync()
        {
            try
            {
                var sql = @"SELECT dk.ID_DANG_KY, dk.MA_DANG_KY, dk.NGAY_DANG_KY, dk.TRANG_THAI,
                                   kh.TEN_KHOA_HOC, lh.TEN_LOP_HOC, lh.NGAY_BAT_DAU,
                                   hd.ID_HOA_DON, hd.MA_HOA_DON, hd.TRANG_THAI as HOA_DON_TRANG_THAI,
                                   CASE WHEN hd.CHU_KY_BASE64 IS NOT NULL THEN 1 ELSE 0 END as CO_CHU_KY
                        FROM QLTT_ADMIN.DON_DANG_KY dk
                        JOIN QLTT_ADMIN.LOP_HOC lh ON dk.ID_LOP_HOC = lh.ID_LOP_HOC
                        JOIN QLTT_ADMIN.KHOA_HOC kh ON lh.ID_KHOA_HOC = kh.ID_KHOA_HOC
                        LEFT JOIN QLTT_ADMIN.HOA_DON hd ON dk.ID_DANG_KY = hd.ID_DANG_KY
                        WHERE dk.ID_HOC_VIEN = (SELECT ID_NGUOI_DUNG FROM QLTT_ADMIN.TAI_KHOAN WHERE UPPER(TEN_DANG_NHAP) = USER)
                            ORDER BY dk.NGAY_DANG_KY DESC";

                using var conn = await GetConnectionAsync();
                using var cmd = new OracleCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                var registrations = new List<StudentRegistrationWithInvoiceDto>();
                while (await reader.ReadAsync())
                {
                    registrations.Add(new StudentRegistrationWithInvoiceDto
                    {
                        RegistrationId = reader.GetInt32(0),
                        RegistrationCode = reader.IsDBNull(1) ? null : reader.GetString(1),
                        RegistrationDate = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                        Status = reader.IsDBNull(3) ? null : reader.GetString(3),
                        CourseName = reader.IsDBNull(4) ? null : reader.GetString(4),
                        ClassName = reader.IsDBNull(5) ? null : reader.GetString(5),
                        StudyDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                        HasInvoice = !reader.IsDBNull(7),
                        InvoiceCode = reader.IsDBNull(8) ? null : reader.GetString(8),
                        InvoiceStatus = reader.IsDBNull(9) ? null : reader.GetString(9),
                        HasDigitalSignature = reader.IsDBNull(10) ? false : reader.GetInt32(10) == 1
                    });
                }

                return registrations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetMyRegistrationsWithInvoiceAsync failed");
                throw;
            }
        }

        // Lấy thông tin chi tiết một đăng ký
        public async Task<StudentRegistrationDetailDto?> GetRegistrationDetailAsync(int registrationId)
        {
            try
            {
                // Lấy studentId theo X-Session-Id để không phụ thuộc USER của kết nối
                int? hvId = null;
                try
                {
                    var sessionId = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(sessionId))
                    {
                        using var adminConn = await GetAdminConnectionAsync();
                        var deviceType3 = _httpContextAccessor.HttpContext?.Request?.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
                        if (deviceType3 != "pc" && deviceType3 != "mobile") deviceType3 = "pc";
                        var columnName3 = deviceType3 == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                        using var findCmd = new Oracle.ManagedDataAccess.Client.OracleCommand($"SELECT ID_NGUOI_DUNG FROM QLTT_ADMIN.TAI_KHOAN WHERE {columnName3} = :sid", adminConn) { BindByName = true };
                        findCmd.Parameters.Add(":sid", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2).Value = sessionId;
                        var obj = await findCmd.ExecuteScalarAsync();
                        if (obj != null && obj != DBNull.Value)
                        {
                            hvId = Convert.ToInt32(obj);
                        }
                    }
                }
                catch
                {
                    // ignore – sẽ thử tiếp bằng USER nếu không tìm được hvId
                }

                // Xây câu SQL dùng hvId nếu có, fallback dùng USER nếu không có hvId
                string sql;
                if (hvId.HasValue)
                {
                    sql = @"SELECT dk.ID_DANG_KY, dk.MA_DANG_KY, dk.NGAY_DANG_KY, dk.TRANG_THAI,
                                   kh.TEN_KHOA_HOC, lh.TEN_LOP_HOC, lh.NGAY_BAT_DAU, lh.NGAY_KET_THUC,
                                   hv.HO_TEN, hv.EMAIL, hv.SO_DIEN_THOAI
                            FROM QLTT_ADMIN.DON_DANG_KY dk
                            JOIN QLTT_ADMIN.LOP_HOC lh ON dk.ID_LOP_HOC = lh.ID_LOP_HOC
                            JOIN QLTT_ADMIN.KHOA_HOC kh ON lh.ID_KHOA_HOC = kh.ID_KHOA_HOC
                            JOIN QLTT_ADMIN.HOC_VIEN hv ON dk.ID_HOC_VIEN = hv.ID_HOC_VIEN
                           WHERE dk.ID_DANG_KY = :regId AND dk.ID_HOC_VIEN = :hvId";
                }
                else
                {
                    sql = @"SELECT dk.ID_DANG_KY, dk.MA_DANG_KY, dk.NGAY_DANG_KY, dk.TRANG_THAI,
                                   kh.TEN_KHOA_HOC, lh.TEN_LOP_HOC, lh.NGAY_BAT_DAU, lh.NGAY_KET_THUC,
                                   hv.HO_TEN, hv.EMAIL, hv.SO_DIEN_THOAI
                            FROM QLTT_ADMIN.DON_DANG_KY dk
                            JOIN QLTT_ADMIN.LOP_HOC lh ON dk.ID_LOP_HOC = lh.ID_LOP_HOC
                            JOIN QLTT_ADMIN.KHOA_HOC kh ON lh.ID_KHOA_HOC = kh.ID_KHOA_HOC
                            JOIN QLTT_ADMIN.HOC_VIEN hv ON dk.ID_HOC_VIEN = hv.ID_HOC_VIEN
                           WHERE dk.ID_DANG_KY = :regId
                             AND dk.ID_HOC_VIEN = (SELECT ID_NGUOI_DUNG FROM QLTT_ADMIN.TAI_KHOAN WHERE UPPER(TEN_DANG_NHAP) = USER)";
                }

                using var conn = await GetConnectionAsync();
                using var cmd = new Oracle.ManagedDataAccess.Client.OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(":regId", Oracle.ManagedDataAccess.Client.OracleDbType.Int32).Value = registrationId;
                if (hvId.HasValue)
                    cmd.Parameters.Add(":hvId", Oracle.ManagedDataAccess.Client.OracleDbType.Int32).Value = hvId.Value;
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new StudentRegistrationDetailDto
                    {
                        RegistrationId = reader.GetInt32(0),
                        RegistrationCode = reader.IsDBNull(1) ? null : reader.GetString(1),
                        RegistrationDate = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                        Status = reader.IsDBNull(3) ? null : reader.GetString(3),
                        CourseName = reader.IsDBNull(4) ? null : reader.GetString(4),
                        ClassName = reader.IsDBNull(5) ? null : reader.GetString(5),
                        StudyStartDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                        StudyEndDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                        StudentName = reader.IsDBNull(8) ? null : reader.GetString(8),
                        Email = reader.IsDBNull(9) ? null : reader.GetString(9),
                        PhoneNumber = reader.IsDBNull(10) ? null : reader.GetString(10)
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetRegistrationDetailAsync failed for registration {RegistrationId}", registrationId);
                throw;
            }
        }
    }
}
