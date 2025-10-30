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
        Task<List<Registration>> GetMyRegistrationsAsync();
        Task<List<AccountantRegDetail>> GetAccountantRegistrationsAsync(int? courseId = null, int? classId = null);
        Task<AccountantRegDetail?> GetAccountantRegistrationByIdAsync(int registrationId);
    }

    public class RegistrationService : BaseService, IRegistrationService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public RegistrationService(IConfiguration configuration, ILogger<RegistrationService> logger, IOracleConnectionProvider userConnProvider, IHttpContextAccessor httpContextAccessor)
            : base(configuration, logger, userConnProvider) { _httpContextAccessor = httpContextAccessor; }

        public async Task<List<Registration>> GetRegistrationsAsync(string? status = null, int? classId = null)
        {
            var where = new List<string>();
            if (!string.IsNullOrWhiteSpace(status)) where.Add("TRANG_THAI = :st");
            if (classId.HasValue) where.Add("ID_LOP_HOC = :cid");
            var whereSql = where.Count > 0 ? (" WHERE " + string.Join(" AND ", where)) : string.Empty;
            var sql = $@"SELECT dk.ID_DANG_KY AS REGISTRATION_ID,
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
                         JOIN KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC{whereSql}
                         ORDER BY dk.ID_DANG_KY DESC";
            object? p = null;
            if (!string.IsNullOrWhiteSpace(status) && classId.HasValue) p = new { st = status, cid = classId.Value };
            else if (!string.IsNullOrWhiteSpace(status)) p = new { st = status };
            else if (classId.HasValue) p = new { cid = classId.Value };
            return await ExecuteQueryAsync<Registration>(sql, p);
        }

        public async Task<Registration?> GetByIdAsync(int id)
        {
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
               WHERE dk.ID_DANG_KY = :id";
            return await ExecuteQuerySingleAsync<Registration>(sql, new { id });
        }

        public async Task<ApiResponse<bool>> ApproveAsync(int registrationId, int? newClassId = null)
        {
            try
            {
                using var conn = await GetAdminConnectionAsync();
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
                            // Enforce role: only role ID 4 can approve
                            var sidHeader = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
                            if (string.IsNullOrWhiteSpace(sidHeader))
                            {
                                return new ApiResponse<bool> { Success = false, Message = "Bạn không có quyền duyệt đơn" };
                            }
                            int roleId = 0;
                            using (var roleCmd = new OracleCommand("SELECT ID_VAI_TRO FROM TAI_KHOAN WHERE SESSION_ID_HIENTAI = :sid", conn) { BindByName = true })
                            {
                                roleCmd.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sidHeader;
                                var objRole = await roleCmd.ExecuteScalarAsync();
                                if (objRole == null || objRole == DBNull.Value || !int.TryParse(objRole.ToString(), out roleId) || roleId != 4)
                                {
                                    return new ApiResponse<bool> { Success = false, Message = "Bạn không có quyền duyệt đơn" };
                                }
                            }
                    }
                    else
                    {
                        return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy đơn đăng ký" };
                    }
                }
                if (newClassId.HasValue)
                {
                    using var upClass = new OracleCommand("UPDATE DON_DANG_KY SET ID_LOP_HOC = :cid WHERE ID_DANG_KY = :id", conn) { BindByName = true };
                    upClass.Parameters.Add(":cid", OracleDbType.Int32).Value = newClassId.Value;
                    upClass.Parameters.Add(":id", OracleDbType.Int32).Value = registrationId;
                    await upClass.ExecuteNonQueryAsync();
                    classIdToUse = newClassId.Value;
                }
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

                var sid = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
                int staffId = 0;
                int? staffIdForUpdate = null;
                if (!string.IsNullOrWhiteSpace(sid))
                {
                    using var findStaff = new OracleCommand("SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE SESSION_ID_HIENTAI = :sid", conn) { BindByName = true };
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
                var sid = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
                int staffId = 0;
                int? staffIdForUpdate = null;
                if (!string.IsNullOrWhiteSpace(sid))
                {
                    using var findStaff = new OracleCommand("SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE SESSION_ID_HIENTAI = :sid", conn) { BindByName = true };
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

        public async Task<List<Registration>> GetMyRegistrationsAsync()
        {
            using var conn = await GetAdminConnectionAsync();
            int hvId = 0;
            var sid = _httpContextAccessor.HttpContext?.Request?.Headers["X-Session-Id"].FirstOrDefault();
            using (var cmd = new OracleCommand("SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE SESSION_ID_HIENTAI = :sid", conn) { BindByName = true })
            {
                cmd.Parameters.Add(":sid", OracleDbType.Varchar2).Value = sid ?? string.Empty;
                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar == null || scalar == DBNull.Value)
                {
                    return new List<Registration>();
                }
                hvId = Convert.ToInt32(scalar);
            }

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
            return list;
        }

        public async Task<List<AccountantRegDetail>> GetAccountantRegistrationsAsync(int? courseId = null, int? classId = null)
        {
            var where = new List<string>();
            if (courseId.HasValue) where.Add("kh.ID_KHOA_HOC = :cid");
            if (classId.HasValue) where.Add("lh.ID_LOP_HOC = :lid");
            var whereSql = where.Count > 0 ? (" WHERE " + string.Join(" AND ", where)) : string.Empty;
            var sql = $@"SELECT dk.ID_DANG_KY,
                                 dk.NGAY_DANG_KY,
                                 dk.TRANG_THAI,
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
                          JOIN KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC{whereSql}
                          ORDER BY dk.ID_DANG_KY DESC";
            object? p = null;
            if (courseId.HasValue && classId.HasValue) p = new { cid = courseId.Value, lid = classId.Value };
            else if (courseId.HasValue) p = new { cid = courseId.Value };
            else if (classId.HasValue) p = new { lid = classId.Value };

            var list = new List<AccountantRegDetail>();
            using var conn = await GetAdminConnectionAsync();
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            if (p != null)
            {
                foreach (var prop in p.GetType().GetProperties())
                    cmd.Parameters.Add($":{prop.Name}", OracleDbType.Int32).Value = (int)prop.GetValue(p)!;
            }
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new AccountantRegDetail
                {
                    RegistrationId = r.GetInt32(0),
                    RegistrationDate = r.IsDBNull(1) ? null : r.GetDateTime(1),
                    Status = r.IsDBNull(2) ? null : r.GetString(2),
                    StudentId = r.GetInt32(3),
                    StudentName = r.IsDBNull(4) ? null : r.GetString(4),
                    Email = r.IsDBNull(5) ? null : r.GetString(5),
                    PhoneNumber = r.IsDBNull(6) ? null : r.GetString(6),
                    ClassId = r.GetInt32(7),
                    ClassName = r.IsDBNull(8) ? null : r.GetString(8),
                    CourseName = r.IsDBNull(9) ? null : r.GetString(9),
                    StandardFee = r.IsDBNull(10) ? 0 : Convert.ToInt32(r.GetValue(10))
                });
            }
            return list;
        }

        public async Task<AccountantRegDetail?> GetAccountantRegistrationByIdAsync(int registrationId)
        {
            var sql = @"SELECT dk.ID_DANG_KY,
                                 dk.NGAY_DANG_KY,
                                 dk.TRANG_THAI,
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
                    Status = r.IsDBNull(2) ? null : r.GetString(2),
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
    }
}
