using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;
using Microsoft.AspNetCore.Http;

namespace QLTTTA_API.Services
{
    public interface IRegistrationService
    {
        Task<List<Registration>> GetRegistrationsAsync(string? status = null, int? classId = null);
        Task<Registration?> GetByIdAsync(int id);
        Task<ApiResponse<bool>> ApproveAsync(int registrationId, int? newClassId = null);
        Task<ApiResponse<bool>> RejectAsync(int registrationId);
        Task<ApiResponse<bool>> CreateRegistrationAsync(int classId, string? note = null);
        Task<List<Registration>> GetMyRegistrationsAsync();
        Task<List<AccountantRegDetail>> GetAccountantRegistrationsAsync(int? courseId = null, int? classId = null);
        Task<AccountantRegDetail?> GetAccountantRegistrationByIdAsync(int registrationId);
        Task<DebugRegistrationCheckResult> DebugCheckAsync(int classId);
    }

    public class DebugRegistrationCheckResult
    {
        public string? SessionId { get; set; }
        public string DeviceType { get; set; } = "pc";
        public int? ResolvedAccountUserId { get; set; }
        public bool HasHocVienRow { get; set; }
        public bool HasStudentsRow { get; set; }
        public bool AutoCreatedHocVien { get; set; }
        public bool ClassExists { get; set; }
        public string? ClassStatus { get; set; }
        public int? ClassMaxSize { get; set; }
        public int? ApprovedCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class RegistrationService : BaseService, IRegistrationService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public RegistrationService(IConfiguration configuration, ILogger<RegistrationService> logger, IOracleConnectionProvider userConnProvider, IHttpContextAccessor httpContextAccessor)
            : base(configuration, logger, userConnProvider, httpContextAccessor) { _httpContextAccessor = httpContextAccessor; }

        public async Task<List<Registration>> GetRegistrationsAsync(string? status = null, int? classId = null)
        {
            var where = new List<string>();
            
            // DEBUG: Log để kiểm tra status value
            _logger.LogInformation("GetRegistrationsAsync called with status: '{Status}', classId: {ClassId}", status ?? "NULL", classId);
            
            if (!string.IsNullOrWhiteSpace(status)) 
            {
                // Sử dụng UPPER và TRIM để tránh vấn đề case sensitivity và khoảng trắng
                where.Add("UPPER(TRIM(dk.TRANG_THAI)) = UPPER(TRIM(:st))");
            }
            if (classId.HasValue) where.Add("dk.ID_LOP_HOC = :cid");
            
            var whereSql = where.Count > 0 ? (" WHERE " + string.Join(" AND ", where)) : string.Empty;
            
            var sql = $@"SELECT dk.ID_DANG_KY,
                                dk.MA_DANG_KY,
                                dk.NGAY_DANG_KY,
                                TRIM(dk.TRANG_THAI) AS TRANG_THAI,
                                NULL AS STUDY_DATE,
                                dk.ID_HOC_VIEN,
                                dk.ID_LOP_HOC,
                                NVL(dk.ID_NHAN_VIEN_DUYET,0) AS ID_NHAN_VIEN_DUYET,
                                lh.TEN_LOP_HOC,
                                kh.TEN_KHOA_HOC
                         FROM DON_DANG_KY dk
                         JOIN LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
                         JOIN KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC{whereSql}
                         ORDER BY dk.ID_DANG_KY DESC";

            object? p = null;
            if (!string.IsNullOrWhiteSpace(status) && classId.HasValue) p = new { st = status.Trim(), cid = classId.Value };
            else if (!string.IsNullOrWhiteSpace(status)) p = new { st = status.Trim() };
            else if (classId.HasValue) p = new { cid = classId.Value };
            
            _logger.LogInformation("Executing SQL: {SQL} with params: {Params}", sql, p?.ToString() ?? "null");
            
            // Sử dụng admin connection để đảm bảo có quyền truy cập
            var result = new List<Registration>();
            try
            {
                using var conn = await GetAdminConnectionAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                
                if (p != null)
                {
                    foreach (var prop in p.GetType().GetProperties())
                    {
                        var value = prop.GetValue(p);
                        cmd.Parameters.Add($":{prop.Name}", value ?? DBNull.Value);
                    }
                }
                
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new Registration
                    {
                        RegistrationId = reader.GetInt32(reader.GetOrdinal("ID_DANG_KY")),
                        RegistrationCode = reader.IsDBNull(reader.GetOrdinal("MA_DANG_KY")) ? null : reader.GetString(reader.GetOrdinal("MA_DANG_KY")),
                        RegistrationDate = reader.IsDBNull(reader.GetOrdinal("NGAY_DANG_KY")) ? null : reader.GetDateTime(reader.GetOrdinal("NGAY_DANG_KY")),
                        Status = reader.IsDBNull(reader.GetOrdinal("TRANG_THAI")) ? null : reader.GetString(reader.GetOrdinal("TRANG_THAI")),  
                        StudyDate = null,
                        StudentId = reader.GetInt32(reader.GetOrdinal("ID_HOC_VIEN")),
                        ClassId = reader.GetInt32(reader.GetOrdinal("ID_LOP_HOC")),
                        StaffId = reader.GetInt32(reader.GetOrdinal("ID_NHAN_VIEN_DUYET")),
                        ClassName = reader.IsDBNull(reader.GetOrdinal("TEN_LOP_HOC")) ? null : reader.GetString(reader.GetOrdinal("TEN_LOP_HOC")),
                        CourseName = reader.IsDBNull(reader.GetOrdinal("TEN_KHOA_HOC")) ? null : reader.GetString(reader.GetOrdinal("TEN_KHOA_HOC"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing GetRegistrationsAsync");
                throw;
            }
            
            _logger.LogInformation("Query returned {Count} registrations", result.Count);
            
            return result;
        }

        public async Task<Registration?> GetByIdAsync(int id)
        {
            var sql = @"SELECT dk.ID_DANG_KY,
                               dk.MA_DANG_KY,
                               dk.NGAY_DANG_KY,
                               dk.TRANG_THAI,
                               NULL AS STUDY_DATE,
                               dk.ID_HOC_VIEN,
                               dk.ID_LOP_HOC,
                               NVL(dk.ID_NHAN_VIEN_DUYET,0) AS ID_NHAN_VIEN_DUYET,
                               lh.TEN_LOP_HOC,
                               kh.TEN_KHOA_HOC
                        FROM DON_DANG_KY dk
                        JOIN LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
                        JOIN KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
                        WHERE dk.ID_DANG_KY = :id";
            
            using var conn = await GetAdminConnectionAsync();
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = id;
            using var reader = await cmd.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                return new Registration
                {
                    RegistrationId = reader.GetInt32(reader.GetOrdinal("ID_DANG_KY")),
                    RegistrationCode = reader.IsDBNull(reader.GetOrdinal("MA_DANG_KY")) ? null : reader.GetString(reader.GetOrdinal("MA_DANG_KY")),
                    RegistrationDate = reader.IsDBNull(reader.GetOrdinal("NGAY_DANG_KY")) ? null : reader.GetDateTime(reader.GetOrdinal("NGAY_DANG_KY")),
                    Status = reader.IsDBNull(reader.GetOrdinal("TRANG_THAI")) ? null : reader.GetString(reader.GetOrdinal("TRANG_THAI")),
                    StudyDate = null,
                    StudentId = reader.GetInt32(reader.GetOrdinal("ID_HOC_VIEN")),
                    ClassId = reader.GetInt32(reader.GetOrdinal("ID_LOP_HOC")),
                    StaffId = reader.GetInt32(reader.GetOrdinal("ID_NHAN_VIEN_DUYET")),
                    ClassName = reader.IsDBNull(reader.GetOrdinal("TEN_LOP_HOC")) ? null : reader.GetString(reader.GetOrdinal("TEN_LOP_HOC")),
                    CourseName = reader.IsDBNull(reader.GetOrdinal("TEN_KHOA_HOC")) ? null : reader.GetString(reader.GetOrdinal("TEN_KHOA_HOC"))
                };
            }
            return null;
        }

        public async Task<ApiResponse<bool>> ApproveAsync(int registrationId, int? newClassId = null)
        {
            try
            {
                using var conn = await GetAdminConnectionAsync();
                // Lấy thông tin đơn + lớp hiện tại
                string regCode = string.Empty;
                int classIdToUse = 0;
                using (var getReg = new OracleCommand("SELECT MA_DANG_KY, ID_LOP_HOC FROM DON_DANG_KY WHERE ID_DANG_KY = :id", conn))
                {
                    getReg.Parameters.Add(":id", OracleDbType.Int32).Value = registrationId;
                    using var r = await getReg.ExecuteReaderAsync();
                    if (await r.ReadAsync())
                    {
                        regCode = r.IsDBNull(0) ? string.Empty : r.GetString(0);
                        classIdToUse = r.IsDBNull(1) ? 0 : r.GetInt32(1);
                    }
                    else
                    {
                        return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy đơn đăng ký" };
                    }
                }
                // Cập nhật lớp nếu truyền newClassId
                if (newClassId.HasValue)
                {
                    using var upClass = new OracleCommand("UPDATE DON_DANG_KY SET ID_LOP_HOC = :cid WHERE ID_DANG_KY = :id", conn) { BindByName = true };
                    upClass.Parameters.Add(":cid", OracleDbType.Int32).Value = newClassId.Value;
                    upClass.Parameters.Add(":id", OracleDbType.Int32).Value = registrationId;
                    await upClass.ExecuteNonQueryAsync();
                    classIdToUse = newClassId.Value;
                }
                // Kiểm tra sĩ số
                int approvedCount = 0, maxSize = 0;
                using (var cmdCnt = new OracleCommand("SELECT (SELECT COUNT(*) FROM DON_DANG_KY WHERE ID_LOP_HOC=:cid AND TRANG_THAI = N'Đã duyệt') AS CNT, (SELECT SI_SO_TOI_DA FROM LOP_HOC WHERE ID_LOP_HOC=:cid) AS MAXS FROM DUAL", conn))
                {
                    cmdCnt.Parameters.Add(":cid", OracleDbType.Int32).Value = classIdToUse;
                    using var rdr = await cmdCnt.ExecuteReaderAsync();
                    if (await rdr.ReadAsync())
                    {
                        approvedCount = Convert.ToInt32(rdr[0]);
                        maxSize = Convert.ToInt32(rdr[1]);
                    }
                }
                if (approvedCount >= maxSize)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Lớp đã đủ sĩ số" };
                }

                // Lấy ID nhân viên theo session per-device
                var sid = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
                var deviceType = _httpContextAccessor.HttpContext?.Request?.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
                if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
                int staffId = 0;
                int? staffIdForUpdate = null;
                if (!string.IsNullOrWhiteSpace(sid))
                {
                    var columnName = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                    using var findStaff = new OracleCommand($"SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE {columnName} = :sid", conn) { BindByName = true };
                    findStaff.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sid;
                    var obj = await findStaff.ExecuteScalarAsync();
                    if (obj != null && obj != DBNull.Value)
                    {
                        staffId = Convert.ToInt32(obj);
                        // Chỉ set ID_NHAN_VIEN_DUYET nếu id này tồn tại trong NHAN_VIEN_HOC_VU để tránh ORA-02291
                        using var chk = new OracleCommand("SELECT COUNT(*) FROM NHAN_VIEN_HOC_VU WHERE ID_NHAN_VIEN = :id", conn) { BindByName = true };
                        chk.Parameters.Add(":id", OracleDbType.Int32).Value = staffId;
                        var cntNV = Convert.ToInt32(await chk.ExecuteScalarAsync());
                        if (cntNV > 0) staffIdForUpdate = staffId; // hợp lệ
                    }
                }

                // Duyệt đơn trực tiếp (không gọi SP) để tránh thiếu quyền EXECUTE
                using (var up = new OracleCommand(@"UPDATE DON_DANG_KY
                                                     SET TRANG_THAI = N'Đã duyệt', NGAY_DUYET = SYSDATE, ID_NHAN_VIEN_DUYET = :st
                                                   WHERE ID_DANG_KY = :id", conn) { BindByName = true })
                {
                    up.Parameters.Add(":st", OracleDbType.Int32).Value = (object?)staffIdForUpdate ?? DBNull.Value;
                    up.Parameters.Add(":id", OracleDbType.Int32).Value = registrationId;
                    var affected = await up.ExecuteNonQueryAsync();
                    if (affected != 1)
                    {
                        return new ApiResponse<bool> { Success = false, Message = "Không thể cập nhật đơn đăng ký" };
                    }
                }

                // Nếu vừa đủ sĩ số, cập nhật trạng thái lớp
                using (var cmdRecheck = new OracleCommand("SELECT (SELECT COUNT(*) FROM DON_DANG_KY WHERE ID_LOP_HOC=:cid AND TRANG_THAI = N'Đã duyệt') AS CNT, (SELECT SI_SO_TOI_DA FROM LOP_HOC WHERE ID_LOP_HOC=:cid) AS MAXS FROM DUAL", conn))
                {
                    cmdRecheck.Parameters.Add(":cid", OracleDbType.Int32).Value = classIdToUse;
                    using var rdr2 = await cmdRecheck.ExecuteReaderAsync();
                    if (await rdr2.ReadAsync())
                    {
                        var cnt2 = Convert.ToInt32(rdr2[0]);
                        var max2 = Convert.ToInt32(rdr2[1]);
                        if (cnt2 >= max2)
                        {
                            using var upClassStatus = new OracleCommand("UPDATE LOP_HOC SET TRANG_THAI = N'Đã đủ sĩ số' WHERE ID_LOP_HOC = :cid AND TRANG_THAI <> N'Đã đủ sĩ số'", conn);
                            upClassStatus.Parameters.Add(":cid", OracleDbType.Int32).Value = classIdToUse;
                            await upClassStatus.ExecuteNonQueryAsync();
                        }
                    }
                }

                return new ApiResponse<bool> { Success = true, Message = "Duyệt đơn thành công", Data = true };
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                _logger.LogWarning(oex, "Insufficient privileges approving registration {Id}", registrationId);
                return new ApiResponse<bool> { Success = false, Message = "Bạn không có quyền duyệt đơn" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Approve registration failed");
                return new ApiResponse<bool> { Success = false, Message = "Có lỗi xảy ra khi duyệt đơn" };
            }
        }

        public async Task<ApiResponse<bool>> RejectAsync(int registrationId)
        {
            try
            {
                using var conn = await GetAdminConnectionAsync();
                // Lấy ID nhân viên theo session per-device
                var sid = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
                var deviceType = _httpContextAccessor.HttpContext?.Request?.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
                if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
                int staffId = 0;
                int? staffIdForUpdate = null;
                if (!string.IsNullOrWhiteSpace(sid))
                {
                    var columnName = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                    using var findStaff = new OracleCommand($"SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE {columnName} = :sid", conn) { BindByName = true };
                    findStaff.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sid;
                    var obj = await findStaff.ExecuteScalarAsync();
                    if (obj != null && obj != DBNull.Value)
                    {
                        staffId = Convert.ToInt32(obj);
                        using var chk = new OracleCommand("SELECT COUNT(*) FROM NHAN_VIEN_HOC_VU WHERE ID_NHAN_VIEN = :id", conn) { BindByName = true };
                        chk.Parameters.Add(":id", OracleDbType.Int32).Value = staffId;
                        var cntNV = Convert.ToInt32(await chk.ExecuteScalarAsync());
                        if (cntNV > 0) staffIdForUpdate = staffId;
                    }
                }

                using var up = new OracleCommand(@"UPDATE DON_DANG_KY
                                                    SET TRANG_THAI = N'Đã từ chối', NGAY_DUYET = SYSDATE, ID_NHAN_VIEN_DUYET = :st
                                                  WHERE ID_DANG_KY = :id", conn) { BindByName = true };
                up.Parameters.Add(":st", OracleDbType.Int32).Value = (object?)staffIdForUpdate ?? DBNull.Value;
                up.Parameters.Add(":id", OracleDbType.Int32).Value = registrationId;
                var affected = await up.ExecuteNonQueryAsync();
                if (affected == 1)
                {
                    return new ApiResponse<bool> { Success = true, Message = "Từ chối đơn thành công", Data = true };
                }
                return new ApiResponse<bool> { Success = false, Message = "Không thể cập nhật đơn đăng ký" };
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                _logger.LogWarning(oex, "Insufficient privileges rejecting registration {Id}", registrationId);
                return new ApiResponse<bool> { Success = false, Message = "Bạn không có quyền từ chối đơn" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reject registration failed");
                return new ApiResponse<bool> { Success = false, Message = "Có lỗi xảy ra khi từ chối đơn" };
            }
        }

        public async Task<ApiResponse<bool>> CreateRegistrationAsync(int classId, string? note = null)
        {
            try
            {
                using var conn = await GetAdminConnectionAsync();
                
                // Xác định học viên theo session per-device từ header
                int studentId = 0;
                var sid = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
                var deviceType = _httpContextAccessor.HttpContext?.Request?.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
                if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
                
                _logger.LogInformation("CreateRegistrationAsync - SessionId: {SessionId}, DeviceType: {DeviceType}, ClassId: {ClassId}", sid ?? "NULL", deviceType, classId);
                
                var columnName = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                using (var cmd = new OracleCommand($"SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE {columnName} = :sid", conn) { BindByName = true })
                {
                    cmd.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sid ?? string.Empty;
                    var scalar = await cmd.ExecuteScalarAsync();
                    if (scalar == null || scalar == DBNull.Value)
                    {
                        _logger.LogWarning("CreateRegistrationAsync - No user found for session {SessionId} on device {DeviceType}", sid ?? "NULL", deviceType);
                        throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn");
                    }
                    studentId = Convert.ToInt32(scalar);
                }
                
                // Kiểm tra xem học viên có tồn tại trong bảng HOC_VIEN không
                bool autoCreated = false;
                using (var checkStudent = new OracleCommand("SELECT COUNT(*) FROM HOC_VIEN WHERE ID_HOC_VIEN = :id", conn) { BindByName = true })
                {
                    checkStudent.Parameters.Add(":id", OracleDbType.Int32).Value = studentId;
                    var count = Convert.ToInt32(await checkStudent.ExecuteScalarAsync());
                    if (count == 0)
                    {
                        // Thử lấy từ bảng STUDENTS (mô hình mới)
                        using var chkStu = new OracleCommand("SELECT FULL_NAME, STUDENT_CODE, SEX, DATE_OF_BIRTH, PHONE_NUMBER, ADDRESS FROM QLTT_ADMIN.STUDENTS WHERE STUDENT_ID = :id", conn) { BindByName = true };
                        chkStu.Parameters.Add(":id", OracleDbType.Int32).Value = studentId;
                        using var rStu = await chkStu.ExecuteReaderAsync();
                        if (await rStu.ReadAsync())
                        {
                            var fullName = rStu.IsDBNull(0) ? null : rStu.GetString(0);
                            var studentCode = rStu.IsDBNull(1) ? null : rStu.GetString(1);
                            var sex = rStu.IsDBNull(2) ? null : rStu.GetString(2);
                            var dob = rStu.IsDBNull(3) ? (DateTime?)null : rStu.GetDateTime(3);
                            var phone = rStu.IsDBNull(4) ? null : rStu.GetString(4);
                            var address = rStu.IsDBNull(5) ? null : rStu.GetString(5);

                            // Tạo bản ghi HOC_VIEN tương ứng
                            using var insHV = new OracleCommand(@"INSERT INTO HOC_VIEN (ID_HOC_VIEN, HO_TEN, MA_HOC_VIEN, GIOI_TINH, NGAY_SINH, SO_DIEN_THOAI, DIA_CHI)
                                                                   VALUES (:id, :hoten, :ma, :sex, :dob, :phone, :addr)", conn) { BindByName = true };
                            insHV.Parameters.Add(":id", OracleDbType.Int32).Value = studentId;
                            insHV.Parameters.Add(":hoten", OracleDbType.NVarchar2).Value = (object?)fullName ?? DBNull.Value;
                            insHV.Parameters.Add(":ma", OracleDbType.Varchar2).Value = (object?)studentCode ?? $"STU_{studentId}";
                            insHV.Parameters.Add(":sex", OracleDbType.NVarchar2).Value = (object?)sex ?? DBNull.Value;
                            insHV.Parameters.Add(":dob", OracleDbType.Date).Value = (object?)dob ?? DBNull.Value;
                            insHV.Parameters.Add(":phone", OracleDbType.Varchar2).Value = (object?)phone ?? DBNull.Value;
                            insHV.Parameters.Add(":addr", OracleDbType.NVarchar2).Value = (object?)address ?? DBNull.Value;
                            await insHV.ExecuteNonQueryAsync();
                            autoCreated = true;
                        }
                        else
                        {
                            return new ApiResponse<bool> { Success = false, Message = "Tài khoản chưa có hồ sơ học viên (không tồn tại HOC_VIEN/STUDENTS)" };
                        }
                    }
                }
                
                // Kiểm tra lớp học có tồn tại và còn mở đăng ký không
                string className = string.Empty;
                string classStatus = string.Empty;
                int maxSize = 0;
                using (var checkClass = new OracleCommand("SELECT TEN_LOP_HOC, TRANG_THAI, SI_SO_TOI_DA FROM LOP_HOC WHERE ID_LOP_HOC = :id", conn) { BindByName = true })
                {
                    checkClass.Parameters.Add(":id", OracleDbType.Int32).Value = classId;
                    using var reader = await checkClass.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        className = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                        classStatus = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
                        maxSize = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    }
                    else
                    {
                        return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy lớp học" };
                    }
                }
                
                // Kiểm tra trạng thái lớp
                if (classStatus.Equals("Đã đủ sĩ số", StringComparison.OrdinalIgnoreCase))
                {
                    return new ApiResponse<bool> { Success = false, Message = "Lớp học đã đủ sĩ số" };
                }
                
                // Kiểm tra xem học viên đã đăng ký lớp này chưa
                using (var checkExisting = new OracleCommand("SELECT COUNT(*) FROM DON_DANG_KY WHERE ID_HOC_VIEN = :studentId AND ID_LOP_HOC = :classId", conn) { BindByName = true })
                {
                    checkExisting.Parameters.Add(":studentId", OracleDbType.Int32).Value = studentId;
                    checkExisting.Parameters.Add(":classId", OracleDbType.Int32).Value = classId;
                    var existingCount = Convert.ToInt32(await checkExisting.ExecuteScalarAsync());
                    if (existingCount > 0)
                    {
                        return new ApiResponse<bool> { Success = false, Message = "Bạn đã đăng ký lớp học này rồi" };
                    }
                }
                
                // Kiểm tra sĩ số hiện tại
                using (var checkCurrentSize = new OracleCommand("SELECT COUNT(*) FROM DON_DANG_KY WHERE ID_LOP_HOC = :classId AND UPPER(TRIM(TRANG_THAI)) = UPPER('Đã duyệt')", conn) { BindByName = true })
                {
                    checkCurrentSize.Parameters.Add(":classId", OracleDbType.Int32).Value = classId;
                    var currentSize = Convert.ToInt32(await checkCurrentSize.ExecuteScalarAsync());
                    if (currentSize >= maxSize)
                    {
                        return new ApiResponse<bool> { Success = false, Message = "Lớp học đã đủ sĩ số" };
                    }
                }
                
                // Tạo mã đăng ký tự động
                string registrationCode = $"REG_{DateTime.Now:yyyyMMdd}_{studentId}_{classId}";
                
                // Tạo đơn đăng ký mới
                var insertSql = @"INSERT INTO DON_DANG_KY (MA_DANG_KY, NGAY_DANG_KY, TRANG_THAI, ID_HOC_VIEN, ID_LOP_HOC, GHI_CHU)
                                  VALUES (:regCode, SYSDATE, :status, :studentId, :classId, :note)";
                
                using (var insertCmd = new OracleCommand(insertSql, conn) { BindByName = true })
                {
                    insertCmd.Parameters.Add(":regCode", OracleDbType.Varchar2).Value = registrationCode;
                    insertCmd.Parameters.Add(":status", OracleDbType.NVarchar2).Value = "Đang chờ";
                    insertCmd.Parameters.Add(":studentId", OracleDbType.Int32).Value = studentId;
                    insertCmd.Parameters.Add(":classId", OracleDbType.Int32).Value = classId;
                    insertCmd.Parameters.Add(":note", OracleDbType.NVarchar2).Value = note ?? (object)DBNull.Value;
                    
                    var affected = await insertCmd.ExecuteNonQueryAsync();
                    if (affected == 1)
                    {
                        _logger.LogInformation("Successfully created registration {RegCode} for student {StudentId} in class {ClassId}", registrationCode, studentId, classId);
                        return new ApiResponse<bool> 
                        { 
                            Success = true, 
                            Message = $"Đăng ký lớp '{className}' thành công. Đơn đăng ký của bạn đang chờ được duyệt.", 
                            Data = true 
                        };
                    }
                    else
                    {
                        return new ApiResponse<bool> { Success = false, Message = "Không thể tạo đơn đăng ký" };
                    }
                }
                if (autoCreated)
                {
                    _logger.LogInformation("Auto-created HOC_VIEN row for user {StudentId} from STUDENTS", studentId);
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                _logger.LogWarning(oex, "Insufficient privileges creating registration for class {ClassId}", classId);
                return new ApiResponse<bool> { Success = false, Message = "Bạn không có quyền đăng ký lớp học" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating registration for class {ClassId}", classId);
                return new ApiResponse<bool> { Success = false, Message = "Có lỗi xảy ra khi đăng ký lớp học" };
            }
        }

        public async Task<List<Registration>> GetMyRegistrationsAsync()
        {
            using var conn = await GetAdminConnectionAsync();
            // Xác định học viên theo session per-device từ header
            int hvId = 0;
            var sid = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
            var deviceType = _httpContextAccessor.HttpContext?.Request?.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
            if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
            
            _logger.LogInformation("GetMyRegistrationsAsync - SessionId: {SessionId}, DeviceType: {DeviceType}", sid ?? "NULL", deviceType);
            
            var columnName = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
            using (var cmd = new OracleCommand($"SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE {columnName} = :sid", conn) { BindByName = true })
            {
                cmd.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sid ?? string.Empty;
                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar == null || scalar == DBNull.Value)
                {
                    _logger.LogWarning("GetMyRegistrationsAsync - No user found for session {SessionId} on device {DeviceType}", sid ?? "NULL", deviceType);
                    throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn");
                }
                hvId = Convert.ToInt32(scalar);
            }
            
            _logger.LogInformation("GetMyRegistrationsAsync - Found user ID: {UserId}", hvId);

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
               FROM DON_DANG_KY dk
               JOIN LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
               JOIN KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
               WHERE dk.ID_HOC_VIEN = :id ORDER BY dk.ID_DANG_KY DESC";
            using var cmd2 = new OracleCommand(sql, conn) { BindByName = true };
            cmd2.Parameters.Add(":id", OracleDbType.Int32).Value = hvId;
            var list = new List<Registration>();
            using var reader = await cmd2.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new Registration
                {
                    RegistrationId = reader.GetInt32(reader.GetOrdinal("REGISTRATION_ID")),
                    RegistrationCode = reader.IsDBNull(reader.GetOrdinal("REGISTRATION_CODE")) ? null : reader.GetString(reader.GetOrdinal("REGISTRATION_CODE")),
                    RegistrationDate = reader.IsDBNull(reader.GetOrdinal("REGISTRATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("REGISTRATION_DATE")),
                    Status = reader.IsDBNull(reader.GetOrdinal("STATUS")) ? null : reader.GetString(reader.GetOrdinal("STATUS")),
                    StudyDate = null,
                    StudentId = reader.GetInt32(reader.GetOrdinal("STUDENT_ID")),
                    ClassId = reader.GetInt32(reader.GetOrdinal("CLASS_ID")),
                    StaffId = reader.GetInt32(reader.GetOrdinal("STAFF_ID")),
                    ClassName = reader.IsDBNull(reader.GetOrdinal("TEN_LOP_HOC")) ? null : reader.GetString(reader.GetOrdinal("TEN_LOP_HOC")),
                    CourseName = reader.IsDBNull(reader.GetOrdinal("TEN_KHOA_HOC")) ? null : reader.GetString(reader.GetOrdinal("TEN_KHOA_HOC"))
                });
            }
            
            _logger.LogInformation("GetMyRegistrationsAsync - Found {Count} registrations for user {UserId}", list.Count, hvId);
            
            return list;
        }

        public async Task<List<AccountantRegDetail>> GetAccountantRegistrationsAsync(int? courseId = null, int? classId = null)
        {
            var where = new List<string>();
            // Chỉ lấy các đơn đã được phê duyệt cho màn kế toán, xử lý khoảng trắng bằng TRIM và không phân biệt hoa/thường
            where.Add("UPPER(TRIM(dk.TRANG_THAI)) = UPPER(:st)");
            if (courseId.HasValue) where.Add("kh.ID_KHOA_HOC = :cid");
            if (classId.HasValue) where.Add("lh.ID_LOP_HOC = :lid");
            var whereSql = where.Count > 0 ? (" WHERE " + string.Join(" AND ", where)) : string.Empty;
            var sql = $@"SELECT dk.ID_DANG_KY,
                                 dk.NGAY_DANG_KY,
                                 TRIM(dk.TRANG_THAI) AS TRANG_THAI,
                                 hv.ID_HOC_VIEN,
                                 hv.HO_TEN,
                                 tk.EMAIL,
                                 hv.SO_DIEN_THOAI,
                                 lh.ID_LOP_HOC,
                                 lh.TEN_LOP_HOC,
                                 kh.TEN_KHOA_HOC,
                                 NVL(kh.HOC_PHI_TIEU_CHUAN,0) AS HOC_PHI_TIEU_CHUAN,
                                 hd.ID_HOA_DON
                          FROM DON_DANG_KY dk
                          JOIN HOC_VIEN hv ON hv.ID_HOC_VIEN = dk.ID_HOC_VIEN
                          LEFT JOIN TAI_KHOAN tk ON tk.ID_NGUOI_DUNG = hv.ID_HOC_VIEN
                          JOIN LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
                          JOIN KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
                          LEFT JOIN HOA_DON hd ON hd.ID_DANG_KY = dk.ID_DANG_KY{whereSql}
                          ORDER BY dk.ID_DANG_KY DESC";
            // Build params dynamically (always include status)
            var paramDict = new Dictionary<string, object> { ["st"] = "Đã duyệt" };
            if (courseId.HasValue) paramDict["cid"] = courseId.Value;
            if (classId.HasValue) paramDict["lid"] = classId.Value;

            var list = new List<AccountantRegDetail>();
            using var conn = await GetAdminConnectionAsync();
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            foreach (var kv in paramDict)
            {
                var name = kv.Key;
                var val = kv.Value;
                if (val is int iv)
                    cmd.Parameters.Add($":{name}", OracleDbType.Int32).Value = iv;
                else
                    cmd.Parameters.Add($":{name}", OracleDbType.Varchar2).Value = val?.ToString();
            }
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new AccountantRegDetail
                {
                    RegistrationId = r.GetInt32(0),
                    RegistrationDate = r.IsDBNull(1) ? null : r.GetDateTime(1),
                    Status = r.IsDBNull(2) ? null : r.GetString(2).Trim(),
                    StudentId = r.GetInt32(3),
                    StudentName = r.IsDBNull(4) ? null : r.GetString(4),
                    Email = r.IsDBNull(5) ? null : r.GetString(5),
                    PhoneNumber = r.IsDBNull(6) ? null : r.GetString(6),
                    ClassId = r.GetInt32(7),
                    ClassName = r.IsDBNull(8) ? null : r.GetString(8),
                    CourseName = r.IsDBNull(9) ? null : r.GetString(9),
                    StandardFee = r.IsDBNull(10) ? 0 : Convert.ToInt32(r.GetValue(10)),
                    InvoiceId = r.IsDBNull(11) ? null : (int?)r.GetInt32(11)
                });
            }
            return list;
        }

        public async Task<AccountantRegDetail?> GetAccountantRegistrationByIdAsync(int registrationId)
        {
            var sql = @"SELECT dk.ID_DANG_KY,
                                 dk.NGAY_DANG_KY,
                                 TRIM(dk.TRANG_THAI) AS TRANG_THAI,
                                 hv.ID_HOC_VIEN,
                                 hv.HO_TEN,
                                 tk.EMAIL,
                                 hv.SO_DIEN_THOAI,
                                 lh.ID_LOP_HOC,
                                 lh.TEN_LOP_HOC,
                                 kh.TEN_KHOA_HOC,
                                 NVL(kh.HOC_PHI_TIEU_CHUAN,0) AS HOC_PHI_TIEU_CHUAN
                          FROM DON_DANG_KY dk
                          JOIN HOC_VIEN hv ON hv.ID_HOC_VIEN = dk.ID_HOC_VIEN
                          LEFT JOIN TAI_KHOAN tk ON tk.ID_NGUOI_DUNG = hv.ID_HOC_VIEN
                          JOIN LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
                          JOIN KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
                          WHERE dk.ID_DANG_KY = :id";
            using var conn = await GetAdminConnectionAsync();
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = registrationId;
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return new AccountantRegDetail
                {
                    RegistrationId = r.GetInt32(0),
                    RegistrationDate = r.IsDBNull(1) ? null : r.GetDateTime(1),
                    Status = r.IsDBNull(2) ? null : r.GetString(2).Trim(),
                    StudentId = r.GetInt32(3),
                    StudentName = r.IsDBNull(4) ? null : r.GetString(4),
                    Email = r.IsDBNull(5) ? null : r.GetString(5),
                    PhoneNumber = r.IsDBNull(6) ? null : r.GetString(6),
                    ClassId = r.GetInt32(7),
                    ClassName = r.IsDBNull(8) ? null : r.GetString(8),
                    CourseName = r.IsDBNull(9) ? null : r.GetString(9),
                    StandardFee = r.IsDBNull(10) ? 0 : Convert.ToInt32(r.GetValue(10))
                };
            }
            return null;
        }

        public async Task<DebugRegistrationCheckResult> DebugCheckAsync(int classId)
        {
            var result = new DebugRegistrationCheckResult();
            using var conn = await GetAdminConnectionAsync();
            var sid = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
            var deviceType = _httpContextAccessor.HttpContext?.Request?.Headers["X-Device-Type"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "pc";
            if (deviceType != "pc" && deviceType != "mobile") deviceType = "pc";
            result.SessionId = sid; result.DeviceType = deviceType;
            try
            {
                if (!string.IsNullOrWhiteSpace(sid))
                {
                    var columnName = deviceType == "mobile" ? "SESSION_ID_MOBILE" : "SESSION_ID_PC";
                    using var cmdUser = new OracleCommand($"SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE {columnName} = :sid", conn) { BindByName = true };
                    cmdUser.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sid;
                    var obj = await cmdUser.ExecuteScalarAsync();
                    if (obj != null && obj != DBNull.Value) result.ResolvedAccountUserId = Convert.ToInt32(obj);
                }
                if (result.ResolvedAccountUserId.HasValue)
                {
                    using var chkHV = new OracleCommand("SELECT COUNT(*) FROM HOC_VIEN WHERE ID_HOC_VIEN=:id", conn) { BindByName = true };
                    chkHV.Parameters.Add(":id", OracleDbType.Int32).Value = result.ResolvedAccountUserId.Value;
                    result.HasHocVienRow = Convert.ToInt32(await chkHV.ExecuteScalarAsync()) > 0;
                    using var chkStu = new OracleCommand("SELECT COUNT(*) FROM QLTT_ADMIN.STUDENTS WHERE STUDENT_ID=:id", conn) { BindByName = true };
                    chkStu.Parameters.Add(":id", OracleDbType.Int32).Value = result.ResolvedAccountUserId.Value;
                    result.HasStudentsRow = Convert.ToInt32(await chkStu.ExecuteScalarAsync()) > 0;
                }
                using (var chkClass = new OracleCommand("SELECT TRANG_THAI, SI_SO_TOI_DA FROM LOP_HOC WHERE ID_LOP_HOC = :cid", conn) { BindByName = true })
                {
                    chkClass.Parameters.Add(":cid", OracleDbType.Int32).Value = classId;
                    using var rc = await chkClass.ExecuteReaderAsync();
                    if (await rc.ReadAsync())
                    {
                        result.ClassExists = true;
                        result.ClassStatus = rc.IsDBNull(0) ? null : rc.GetString(0).Trim();
                        result.ClassMaxSize = rc.IsDBNull(1) ? null : (int?)rc.GetInt32(1);
                    }
                }
                if (result.ClassExists)
                {
                    using var cntApproved = new OracleCommand("SELECT COUNT(*) FROM DON_DANG_KY WHERE ID_LOP_HOC = :cid AND UPPER(TRIM(TRANG_THAI)) = UPPER('Đã duyệt')", conn) { BindByName = true };
                    cntApproved.Parameters.Add(":cid", OracleDbType.Int32).Value = classId;
                    result.ApprovedCount = Convert.ToInt32(await cntApproved.ExecuteScalarAsync());
                }
                result.Message = "OK";
            }
            catch (Exception ex)
            {
                result.Message = "Lỗi debug: " + ex.Message;
            }
            return result;
        }
    }
}
