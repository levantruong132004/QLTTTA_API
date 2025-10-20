using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;

namespace QLTTTA_API.Services
{
    public interface IRegistrationService
    {
        Task<List<Registration>> GetRegistrationsAsync(string? status = null, int? classId = null);
        Task<Registration?> GetByIdAsync(int id);
        Task<ApiResponse<bool>> ApproveAsync(int registrationId, int? newClassId = null);
        Task<ApiResponse<bool>> RejectAsync(int registrationId);
        Task<List<Registration>> GetMyRegistrationsAsync();
    }

    public class RegistrationService : BaseService, IRegistrationService
    {
        public RegistrationService(IConfiguration configuration, ILogger<RegistrationService> logger, IOracleConnectionProvider userConnProvider)
            : base(configuration, logger, userConnProvider) { }

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
                using var conn = await GetConnectionAsync();
                // Lấy mã đơn và lớp hiện tại
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
                    using var upClass = new OracleCommand("UPDATE DON_DANG_KY SET ID_LOP_HOC = :cid WHERE ID_DANG_KY = :id", conn);
                    upClass.Parameters.Add(":cid", OracleDbType.Int32).Value = newClassId.Value;
                    upClass.Parameters.Add(":id", OracleDbType.Int32).Value = registrationId;
                    await upClass.ExecuteNonQueryAsync();
                    classIdToUse = newClassId.Value;
                }
                // Kiểm tra sĩ số trước khi duyệt
                int approvedCount = 0, maxSize = 0;
                using (var cmdCnt = new OracleCommand("SELECT (SELECT COUNT(*) FROM DON_DANG_KY WHERE ID_LOP_HOC=:cid AND TRANG_THAI = 'Đã duyệt') AS CNT, (SELECT SI_SO_TOI_DA FROM LOP_HOC WHERE ID_LOP_HOC=:cid) AS MAXS FROM DUAL", conn))
                {
                    cmdCnt.Parameters.Add(":cid", OracleDbType.Int32).Value = classIdToUse;
                    using var rdr = await cmdCnt.ExecuteReaderAsync();
                    if (await rdr.ReadAsync())
                    {
                        approvedCount = Convert.ToInt32(rdr["CNT"]);
                        maxSize = Convert.ToInt32(rdr["MAXS"]);
                    }
                }
                if (approvedCount >= maxSize)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Lớp đã đủ sĩ số" };
                }
                // Gọi SP_DUYET_DON_DANG_KY để duyệt
                using (var cmd = new OracleCommand("SP_DUYET_DON_DANG_KY", conn))
                {
                    cmd.BindByName = true;
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_ma_dang_ky", OracleDbType.Varchar2).Value = regCode;
                    cmd.Parameters.Add("p_hanh_dong", OracleDbType.Varchar2).Value = "DUYET";
                    var outMsg = new OracleParameter("p_ket_qua", OracleDbType.NVarchar2, 4000) { Direction = System.Data.ParameterDirection.Output };
                    cmd.Parameters.Add(outMsg);
                    await cmd.ExecuteNonQueryAsync();
                    var msg = outMsg.Value?.ToString() ?? string.Empty;
                    // Sau khi duyệt, nếu vừa đủ sĩ số, cập nhật trạng thái lớp
                    using (var cmdRecheck = new OracleCommand("SELECT (SELECT COUNT(*) FROM DON_DANG_KY WHERE ID_LOP_HOC=:cid AND TRANG_THAI = 'Đã duyệt') AS CNT, (SELECT SI_SO_TOI_DA FROM LOP_HOC WHERE ID_LOP_HOC=:cid) AS MAXS FROM DUAL", conn))
                    {
                        cmdRecheck.Parameters.Add(":cid", OracleDbType.Int32).Value = classIdToUse;
                        using var rdr2 = await cmdRecheck.ExecuteReaderAsync();
                        if (await rdr2.ReadAsync())
                        {
                            var cnt2 = Convert.ToInt32(rdr2["CNT"]);
                            var max2 = Convert.ToInt32(rdr2["MAXS"]);
                            if (cnt2 >= max2)
                            {
                                using var upClassStatus = new OracleCommand("UPDATE LOP_HOC SET TRANG_THAI = 'Đã đủ sĩ số' WHERE ID_LOP_HOC = :cid AND TRANG_THAI <> 'Đã đủ sĩ số'", conn);
                                upClassStatus.Parameters.Add(":cid", OracleDbType.Int32).Value = classIdToUse;
                                await upClassStatus.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    var success = msg.Contains("duyệt", StringComparison.OrdinalIgnoreCase) || msg.Contains("thành công", StringComparison.OrdinalIgnoreCase);
                    return new ApiResponse<bool> { Success = success, Message = string.IsNullOrWhiteSpace(msg) ? (success ? "Duyệt đơn thành công" : "Duyệt đơn thất bại") : msg, Data = success };
                }
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
                using var conn = await GetConnectionAsync();
                // Lấy mã đơn theo ID
                string regCode = string.Empty;
                using (var getReg = new OracleCommand("SELECT MA_DANG_KY FROM DON_DANG_KY WHERE ID_DANG_KY = :id", conn))
                {
                    getReg.Parameters.Add(":id", OracleDbType.Int32).Value = registrationId;
                    var o = await getReg.ExecuteScalarAsync();
                    if (o == null) return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy đơn đăng ký" };
                    regCode = o.ToString() ?? string.Empty;
                }
                using (var cmd = new OracleCommand("SP_DUYET_DON_DANG_KY", conn))
                {
                    cmd.BindByName = true;
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_ma_dang_ky", OracleDbType.Varchar2).Value = regCode;
                    cmd.Parameters.Add("p_hanh_dong", OracleDbType.Varchar2).Value = "TUCHOI";
                    var outMsg = new OracleParameter("p_ket_qua", OracleDbType.NVarchar2, 4000) { Direction = System.Data.ParameterDirection.Output };
                    cmd.Parameters.Add(outMsg);
                    await cmd.ExecuteNonQueryAsync();
                    var msg = outMsg.Value?.ToString() ?? string.Empty;
                    var success = msg.Contains("từ chối", StringComparison.OrdinalIgnoreCase) || msg.Contains("thành công", StringComparison.OrdinalIgnoreCase);
                    return new ApiResponse<bool> { Success = success, Message = string.IsNullOrWhiteSpace(msg) ? (success ? "Từ chối đơn thành công" : "Từ chối đơn thất bại") : msg, Data = success };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reject registration failed");
                return new ApiResponse<bool> { Success = false, Message = "Có lỗi xảy ra khi từ chối đơn" };
            }
        }

        public async Task<List<Registration>> GetMyRegistrationsAsync()
        {
            // Lấy ID_HOC_VIEN theo USER hiện tại, sau đó trả danh sách đơn của học viên này
            using var conn = await GetConnectionAsync();
            int hvId = 0;
            using (var cmd = new OracleCommand("SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE UPPER(TEN_DANG_NHAP)=USER", conn))
            {
                hvId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
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
                    RegistrationDate = reader.IsDBNull(reader.GetOrdinal("REGISTRATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("REGISTRATION_DATE")),
                    Status = reader.IsDBNull(reader.GetOrdinal("STATUS")) ? null : reader.GetString(reader.GetOrdinal("STATUS")),
                    StudyDate = null,
                    StudentId = reader.GetInt32(reader.GetOrdinal("STUDENT_ID")),
                    ClassId = reader.GetInt32(reader.GetOrdinal("CLASS_ID")),
                    StaffId = reader.GetInt32(reader.GetOrdinal("STAFF_ID"))
                });
            }
            return list;
        }
    }
}
