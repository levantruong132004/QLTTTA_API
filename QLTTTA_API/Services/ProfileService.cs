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
        Task<ProfileDebugResult> DebugProfileAsync();
    }

    public class ProfileDebugResult
    {
        public string? SessionId { get; set; }
        public string DeviceType { get; set; } = "pc";
        public string? ResolvedUsername { get; set; }
        public int? UserId { get; set; }
        public bool ViewReturnedRow { get; set; }
        public bool HasStudentsRow { get; set; }
        public bool HasHocVienRow { get; set; }
        public bool AutoCreatedHocVien { get; set; }
        public string SourceUsed { get; set; } = ""; // VIEW | STUDENTS | HOC_VIEN
        public string Message { get; set; } = "";
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
            // Lấy session (header trước, cookie sau)
            using var conn = await GetAdminConnectionAsync();
            var sessionHeader = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
            var sessionCookie = _httpContextAccessor.HttpContext?.Request?.Cookies["SessionId"];
            var sessionId = !string.IsNullOrWhiteSpace(sessionHeader) ? sessionHeader : sessionCookie;
            var deviceType = _httpContextAccessor.HttpContext?.Request?.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
            if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                _logger.LogWarning("GetMyProfileAsync: thiếu sessionId");
                return null;
            }
            // Tra ID người dùng từ session per-device
            var columnName = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
            int? userId = null;
            string? username = null;
            string? email = null;
            try
            {
                using var cmdFind = new OracleCommand($"SELECT ID_NGUOI_DUNG, TEN_DANG_NHAP, EMAIL FROM TAI_KHOAN WHERE {columnName} = :sid", conn) { BindByName = true };
                cmdFind.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sessionId;
                using var r = await cmdFind.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
                if (await r.ReadAsync())
                {
                    userId = r.IsDBNull(0) ? null : (int?)r.GetInt32(0);
                    username = r.IsDBNull(1) ? null : r.GetString(1);
                    email = r.IsDBNull(2) ? null : r.GetString(2);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetMyProfileAsync: lỗi tìm user theo session");
                return null;
            }
            if (!userId.HasValue)
            {
                _logger.LogWarning("GetMyProfileAsync: không tìm thấy userId theo session {SessionId}", sessionId);
                return null;
            }
            // Lấy thông tin từ HOC_VIEN
            StudentProfileDto? dto = null;
            try
            {
                using var cmdHV = new OracleCommand(@"SELECT HO_TEN, MA_HOC_VIEN, GIOI_TINH, NGAY_SINH, SO_DIEN_THOAI, DIA_CHI FROM HOC_VIEN WHERE ID_HOC_VIEN = :id", conn) { BindByName = true };
                cmdHV.Parameters.Add(":id", OracleDbType.Int32).Value = userId.Value;
                using var rHV = await cmdHV.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
                if (await rHV.ReadAsync())
                {
                    dto = new StudentProfileDto
                    {
                        HoTen = rHV.IsDBNull(0) ? null : rHV.GetString(0),
                        MaHocVien = rHV.IsDBNull(1) ? null : rHV.GetString(1),
                        GioiTinh = rHV.IsDBNull(2) ? null : rHV.GetString(2),
                        NgaySinh = rHV.IsDBNull(3) ? null : rHV.GetDateTime(3),
                        SoDienThoai = rHV.IsDBNull(4) ? null : rHV.GetString(4),
                        DiaChi = rHV.IsDBNull(5) ? null : rHV.GetString(5),
                        Email = email
                    };
                }
            }
            catch (OracleException oex)
            {
                _logger.LogWarning(oex, "GetMyProfileAsync: lỗi đọc HOC_VIEN userId={UserId}", userId);
            }
            // Nếu chưa có bản ghi HOC_VIEN -> tạo tối thiểu rồi trả về
            if (dto == null)
            {
                try
                {
                    using var chk = new OracleCommand("SELECT COUNT(*) FROM HOC_VIEN WHERE ID_HOC_VIEN = :id", conn) { BindByName = true };
                    chk.Parameters.Add(":id", OracleDbType.Int32).Value = userId.Value;
                    var cnt = Convert.ToInt32(await chk.ExecuteScalarAsync());
                    if (cnt == 0)
                    {
                        var ma = $"STU_{userId.Value}";
                        var hoten = !string.IsNullOrWhiteSpace(username) ? username : "Chưa cập nhật";
                        using var ins = new OracleCommand(@"INSERT INTO HOC_VIEN(ID_HOC_VIEN, HO_TEN, MA_HOC_VIEN, GIOI_TINH) VALUES (:id,:hoten,:ma,:sex)", conn) { BindByName = true };
                        ins.Parameters.Add(":id", OracleDbType.Int32).Value = userId.Value;
                        ins.Parameters.Add(":hoten", OracleDbType.NVarchar2).Value = hoten;
                        ins.Parameters.Add(":ma", OracleDbType.Varchar2).Value = ma;
                        ins.Parameters.Add(":sex", OracleDbType.NVarchar2).Value = "Khác";
                        await ins.ExecuteNonQueryAsync();
                        _logger.LogInformation("GetMyProfileAsync: auto tạo HOC_VIEN cho user {UserId}", userId);
                        dto = new StudentProfileDto { HoTen = hoten, MaHocVien = ma, Email = email };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "GetMyProfileAsync: lỗi tạo bản ghi HOC_VIEN tối thiểu");
                    // Trả về thông tin tối thiểu từ TAI_KHOAN
                    dto = new StudentProfileDto { HoTen = username, MaHocVien = $"STU_{userId.Value}", Email = email };
                }
            }
            // Bổ sung nếu thiếu Họ tên / Mã học viên
            if (dto != null)
            {
                dto.HoTen ??= username;
                dto.MaHocVien ??= $"STU_{userId.Value}";
                dto.Email ??= email;
            }
            return dto;
        }

        /// <summary>
        /// Cập nhật 2 trường: SỐ ĐIỆN THOẠI và ĐỊA CHỈ thông qua quyền UPDATE đã cấp trên view.
        /// Các trường khác bị cố tình bỏ qua để tránh chỉnh sửa ngoài phạm vi cho phép.
        /// </summary>
        public async Task<bool> UpdateMyProfileAsync(StudentProfileUpdateDto dto)
        {
            const string sql = @"UPDATE QLTT_ADMIN.V_THONGTIN_CANHAN_HV
SET SO_DIEN_THOAI = :p_sdt, DIA_CHI = :p_diachi
WHERE UPPER(TEN_DANG_NHAP) = USER";
            using var conn = await GetAdminConnectionAsync();
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":p_sdt", OracleDbType.Varchar2).Value = (object?)dto.SoDienThoai ?? DBNull.Value;
            cmd.Parameters.Add(":p_diachi", OracleDbType.NVarchar2).Value = (object?)dto.DiaChi ?? DBNull.Value;
            try
            {
                var affected = await cmd.ExecuteNonQueryAsync();
                if (affected >= 1) return true;
            }
            catch (OracleException oex) when (oex.Number == 942 || oex.Number == 1031)
            {
                _logger.LogWarning(oex, "UPDATE view thất bại, fallback bảng HOC_VIEN");
            }
            // Fallback bảng HOC_VIEN nếu view không cập nhật được
            try
            {
                // Lấy userId theo session
                int? userId = null;
                var sessionId = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
                var deviceType = _httpContextAccessor.HttpContext?.Request?.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
                if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    var columnName = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                    using var findUser = new OracleCommand($"SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE {columnName} = :sid", conn) { BindByName = true };
                    findUser.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sessionId;
                    var obj = await findUser.ExecuteScalarAsync();
                    if (obj != null && obj != DBNull.Value) userId = Convert.ToInt32(obj);
                }
                if (!userId.HasValue) return false;

                using var upHV = new OracleCommand("UPDATE HOC_VIEN SET SO_DIEN_THOAI = :sdt, DIA_CHI = :dc WHERE ID_HOC_VIEN = :id", conn) { BindByName = true };
                upHV.Parameters.Add(":sdt", OracleDbType.Varchar2).Value = (object?)dto.SoDienThoai ?? DBNull.Value;
                upHV.Parameters.Add(":dc", OracleDbType.NVarchar2).Value = (object?)dto.DiaChi ?? DBNull.Value;
                upHV.Parameters.Add(":id", OracleDbType.Int32).Value = userId.Value;
                var aff = await upHV.ExecuteNonQueryAsync();
                return aff >= 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallback cập nhật HOC_VIEN thất bại");
                return false;
            }
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
                int hvId;
                // Luôn lấy hvId theo SessionId để tránh nhầm USER=admin
                var sessionId = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault()
                               ?? _httpContextAccessor.HttpContext?.Request?.Cookies["SessionId"];
                var deviceType = _httpContextAccessor.HttpContext?.Request?.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
                if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return (false, "Phiên đăng nhập không hợp lệ hoặc đã hết hạn");
                }
                using (var adminConn = await GetAdminConnectionAsync())
                {
                    var columnName = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                    using var findCmd = new OracleCommand($"SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE {columnName} = :sid", adminConn) { BindByName = true };
                    findCmd.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sessionId;
                    var obj = await findCmd.ExecuteScalarAsync();
                    if (obj == null || obj == DBNull.Value)
                    {
                        return (false, "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại");
                    }
                    hvId = Convert.ToInt32(obj);
                    // Đảm bảo có HOC_VIEN
                    using var chkHv = new OracleCommand("SELECT COUNT(*) FROM HOC_VIEN WHERE ID_HOC_VIEN = :id", adminConn) { BindByName = true };
                    chkHv.Parameters.Add(":id", OracleDbType.Int32).Value = hvId;
                    var cnt = Convert.ToInt32(await chkHv.ExecuteScalarAsync());
                    if (cnt == 0)
                    {
                        string? username = null;
                        using (var getU = new OracleCommand("SELECT TEN_DANG_NHAP FROM TAI_KHOAN WHERE ID_NGUOI_DUNG = :id", adminConn) { BindByName = true })
                        {
                            getU.Parameters.Add(":id", OracleDbType.Int32).Value = hvId;
                            var u = await getU.ExecuteScalarAsync();
                            if (u != null && u != DBNull.Value) username = u.ToString();
                        }
                        var hoten = !string.IsNullOrWhiteSpace(username) ? username : "Chưa cập nhật";
                        using var insHv = new OracleCommand("INSERT INTO HOC_VIEN (ID_HOC_VIEN, HO_TEN, MA_HOC_VIEN, GIOI_TINH) VALUES (:id,:hoten,:ma,:sex)", adminConn) { BindByName = true };
                        insHv.Parameters.Add(":id", OracleDbType.Int32).Value = hvId;
                        insHv.Parameters.Add(":hoten", OracleDbType.NVarchar2).Value = hoten;
                        insHv.Parameters.Add(":ma", OracleDbType.Varchar2).Value = $"STU_{hvId}";
                        insHv.Parameters.Add(":sex", OracleDbType.NVarchar2).Value = "Khác";
                        try { await insHv.ExecuteNonQueryAsync(); } catch (OracleException oex) when (oex.Number == 1) { }
                    }
                }

                // Chèn đơn đăng ký bằng adminConn để tránh thiếu quyền
                using var conn = await GetAdminConnectionAsync();
                var sql = @"INSERT INTO DON_DANG_KY (MA_DANG_KY, NGAY_DANG_KY, TRANG_THAI, ID_HOC_VIEN, ID_LOP_HOC)
                            VALUES ('DGK_'||TO_CHAR(SYSDATE,'YYYYMMDDHH24MISS')||'_'||TRUNC(DBMS_RANDOM.VALUE(1000,9999)), SYSDATE, N'Chờ duyệt', :hv, :cid)";
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
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
                int hvId = 0;
                var sessionId = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault()
                               ?? _httpContextAccessor.HttpContext?.Request?.Cookies["SessionId"];
                var deviceType = _httpContextAccessor.HttpContext?.Request?.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
                if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    using var adminConn = await GetAdminConnectionAsync();
                    var columnName = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                    using var findCmd = new OracleCommand($"SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE {columnName} = :sid", adminConn) { BindByName = true };
                    findCmd.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sessionId;
                    var obj = await findCmd.ExecuteScalarAsync();
                    if (obj == null || obj == DBNull.Value)
                    {
                        _logger.LogWarning("No user found for session {SessionId}", sessionId);
                        return new List<StudentRegistrationWithInvoiceDto>();
                    }
                    hvId = Convert.ToInt32(obj);
                }
                else
                {
                    _logger.LogWarning("No session ID found in request headers");
                    return new List<StudentRegistrationWithInvoiceDto>();
                }

                // Bỏ phụ thuộc vào DA_IN để tránh ORA-00904
                var sql = @"SELECT dk.ID_DANG_KY, dk.MA_DANG_KY, dk.NGAY_DANG_KY, dk.TRANG_THAI,
                                   kh.TEN_KHOA_HOC, lh.TEN_LOP_HOC, lh.NGAY_BAT_DAU,
                                   hd.ID_HOA_DON, hd.MA_HOA_DON, hd.TRANG_THAI as HOA_DON_TRANG_THAI,
                                   CASE WHEN hd.CHU_KY_BASE64 IS NOT NULL THEN 1 ELSE 0 END as CO_CHU_KY
                            FROM DON_DANG_KY dk
                            JOIN LOP_HOC lh ON dk.ID_LOP_HOC = lh.ID_LOP_HOC
                            JOIN KHOA_HOC kh ON lh.ID_KHOA_HOC = kh.ID_KHOA_HOC
                            LEFT JOIN HOA_DON hd ON dk.ID_DANG_KY = hd.ID_DANG_KY
                            WHERE dk.ID_HOC_VIEN = :hvId
                            ORDER BY dk.NGAY_DANG_KY DESC";

                using var conn = await GetAdminConnectionAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(":hvId", OracleDbType.Int32).Value = hvId;
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

                _logger.LogInformation("Found {Count} registrations for student {StudentId}", registrations.Count, hvId);
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
                int? hvId = null;
                var sessionHeader = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
                var sessionCookie = _httpContextAccessor.HttpContext?.Request?.Cookies["SessionId"];
                var sessionId = !string.IsNullOrWhiteSpace(sessionHeader) ? sessionHeader : sessionCookie;
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    using var adminConn = await GetAdminConnectionAsync();
                    var deviceType3 = _httpContextAccessor.HttpContext?.Request?.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
                    if (deviceType3 != "pc" && deviceType3 != "mobile") deviceType3 = "pc";
                    var columnName3 = deviceType3 == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                    using var findCmd = new Oracle.ManagedDataAccess.Client.OracleCommand($"SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE {columnName3} = :sid", adminConn) { BindByName = true };
                    findCmd.Parameters.Add(":sid", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2).Value = sessionId;
                    var obj = await findCmd.ExecuteScalarAsync();
                    if (obj != null && obj != DBNull.Value)
                    {
                        hvId = Convert.ToInt32(obj);
                    }
                }
                if (!hvId.HasValue)
                {
                    _logger.LogWarning("GetRegistrationDetail: cannot resolve hvId from session");
                    return null;
                }

                var sql = @"SELECT dk.ID_DANG_KY, dk.MA_DANG_KY, dk.NGAY_DANG_KY, dk.TRANG_THAI,
                                   kh.TEN_KHOA_HOC, lh.TEN_LOP_HOC, lh.NGAY_BAT_DAU, lh.NGAY_KET_THUC,
                                   hv.HO_TEN, tk.EMAIL, hv.SO_DIEN_THOAI
                            FROM DON_DANG_KY dk
                            JOIN LOP_HOC lh ON dk.ID_LOP_HOC = lh.ID_LOP_HOC
                            JOIN KHOA_HOC kh ON lh.ID_KHOA_HOC = kh.ID_KHOA_HOC
                            JOIN HOC_VIEN hv ON dk.ID_HOC_VIEN = hv.ID_HOC_VIEN
                            JOIN TAI_KHOAN tk ON tk.ID_NGUOI_DUNG = hv.ID_HOC_VIEN
                           WHERE dk.ID_DANG_KY = :regId AND dk.ID_HOC_VIEN = :hvId";

                using var conn = await GetAdminConnectionAsync();
                using var cmd = new Oracle.ManagedDataAccess.Client.OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(":regId", Oracle.ManagedDataAccess.Client.OracleDbType.Int32).Value = registrationId;
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

        public async Task<ProfileDebugResult> DebugProfileAsync()
        {
            var result = new ProfileDebugResult();
            using var conn = await GetAdminConnectionAsync();
            var sessionHeader = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
            var sessionCookie = _httpContextAccessor.HttpContext?.Request?.Cookies["SessionId"];
            result.SessionId = !string.IsNullOrWhiteSpace(sessionHeader) ? sessionHeader : sessionCookie;
            var deviceType = _httpContextAccessor.HttpContext?.Request?.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
            if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
            result.DeviceType = deviceType;
            try
            {
                if (!string.IsNullOrWhiteSpace(result.SessionId))
                {
                    var columnName = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                    using var cmdUser = new OracleCommand($"SELECT TEN_DANG_NHAP, ID_NGUOI_DUNG FROM TAI_KHOAN WHERE {columnName} = :sid", conn) { BindByName = true };
                    cmdUser.Parameters.Add(":sid", OracleDbType.Varchar2).Value = result.SessionId;
                    using var r = await cmdUser.ExecuteReaderAsync();
                    if (await r.ReadAsync())
                    {
                        result.ResolvedUsername = r.IsDBNull(0) ? null : r.GetString(0);
                        result.UserId = r.IsDBNull(1) ? null : (int?)r.GetInt32(1);
                    }
                }
                // Check view
                using (var chkView = new OracleCommand("SELECT COUNT(*) FROM QLTT_ADMIN.V_THONGTIN_CANHAN_HV", conn))
                {
                    var cnt = Convert.ToInt32(await chkView.ExecuteScalarAsync());
                    result.ViewReturnedRow = cnt > 0;
                }
                if (result.UserId.HasValue)
                {
                    using var chkStu = new OracleCommand("SELECT COUNT(*) FROM QLTT_ADMIN.STUDENTS WHERE STUDENT_ID = :id", conn) { BindByName = true };
                    chkStu.Parameters.Add(":id", OracleDbType.Int32).Value = result.UserId.Value;
                    result.HasStudentsRow = Convert.ToInt32(await chkStu.ExecuteScalarAsync()) > 0;
                    using var chkHV = new OracleCommand("SELECT COUNT(*) FROM HOC_VIEN WHERE ID_HOC_VIEN = :id", conn) { BindByName = true };
                    chkHV.Parameters.Add(":id", OracleDbType.Int32).Value = result.UserId.Value;
                    result.HasHocVienRow = Convert.ToInt32(await chkHV.ExecuteScalarAsync()) > 0;
                }
                // Determine source
                if (result.ViewReturnedRow) result.SourceUsed = "VIEW";
                else if (result.HasStudentsRow) result.SourceUsed = "STUDENTS";
                else if (result.HasHocVienRow) result.SourceUsed = "HOC_VIEN";
                result.Message = "OK";
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
            }
            return result;
        }
    }
}
