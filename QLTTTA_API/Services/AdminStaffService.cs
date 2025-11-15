using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models.DTOs;
using System.Data;

namespace QLTTTA_API.Services
{
    public interface IAdminStaffService
    {
        Task<List<StaffListItemDto>> GetStaffAsync();
        Task<ApiResponse<StaffListItemDto>> CreateStaffAsync(StaffCreateDto dto);
        Task<ApiResponse<StaffListItemDto>> UpdateStaffAsync(int id, StaffUpdateDto dto);
        Task<ApiResponse<bool>> LockStaffAsync(int id);
        Task<ApiResponse<bool>> UnlockStaffAsync(int id);
    }

    public class AdminStaffService : BaseService, IAdminStaffService
    {
        public AdminStaffService(IConfiguration configuration, ILogger<AdminStaffService> logger, IOracleConnectionProvider userConnProvider)
            : base(configuration, logger, userConnProvider) { }

        public async Task<List<StaffListItemDto>> GetStaffAsync()
        {
            var result = new List<StaffListItemDto>();
            try
            {
                using var conn = await GetConnectionAsync();
                var sql = @"SELECT tk.ID_NGUOI_DUNG, tk.TEN_DANG_NHAP, tk.EMAIL, tk.ID_VAI_TRO, vt.TEN_VAI_TRO,
                                   tk.TRANG_THAI_KICH_HOAT,
                                   COALESCE(nv.HO_TEN, kt.HO_TEN) AS HO_TEN,
                                   nv.GIOI_TINH AS NV_GIOI_TINH,
                                   nv.SO_DIEN_THOAI AS NV_SO_DIEN_THOAI,
                                   kt.GIOI_TINH AS KT_GIOI_TINH,
                                   kt.SO_DIEN_THOAI AS KT_SO_DIEN_THOAI
                        FROM QLTT_ADMIN.TAI_KHOAN tk
                        LEFT JOIN QLTT_ADMIN.VAI_TRO vt ON vt.ID_VAI_TRO = tk.ID_VAI_TRO
                        LEFT JOIN QLTT_ADMIN.NHAN_VIEN_HOC_VU nv ON nv.ID_NHAN_VIEN = tk.ID_NGUOI_DUNG
                        LEFT JOIN QLTT_ADMIN.KE_TOAN kt ON kt.ID_KE_TOAN = tk.ID_NGUOI_DUNG
                            WHERE tk.ID_VAI_TRO IN (3,4,5)
                            ORDER BY tk.ID_NGUOI_DUNG";
                using var cmd = new OracleCommand(sql, conn);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var dto = new StaffListItemDto
                    {
                        UserId = rdr.GetInt32(rdr.GetOrdinal("ID_NGUOI_DUNG")),
                        Username = rdr.GetString(rdr.GetOrdinal("TEN_DANG_NHAP")),
                        Email = rdr.GetString(rdr.GetOrdinal("EMAIL")),
                        RoleId = rdr.GetInt32(rdr.GetOrdinal("ID_VAI_TRO")),
                        RoleName = rdr.IsDBNull(rdr.GetOrdinal("TEN_VAI_TRO")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("TEN_VAI_TRO")),
                        IsActive = rdr.GetInt32(rdr.GetOrdinal("TRANG_THAI_KICH_HOAT")) == 1,
                        FullName = rdr.IsDBNull(rdr.GetOrdinal("HO_TEN")) ? null : rdr.GetString(rdr.GetOrdinal("HO_TEN")),
                        Sex = !rdr.IsDBNull(rdr.GetOrdinal("NV_GIOI_TINH")) ? rdr.GetString(rdr.GetOrdinal("NV_GIOI_TINH")) : (!rdr.IsDBNull(rdr.GetOrdinal("KT_GIOI_TINH")) ? rdr.GetString(rdr.GetOrdinal("KT_GIOI_TINH")) : null),
                        Phone = !rdr.IsDBNull(rdr.GetOrdinal("NV_SO_DIEN_THOAI")) ? rdr.GetString(rdr.GetOrdinal("NV_SO_DIEN_THOAI")) : (!rdr.IsDBNull(rdr.GetOrdinal("KT_SO_DIEN_THOAI")) ? rdr.GetString(rdr.GetOrdinal("KT_SO_DIEN_THOAI")) : null)
                    };
                    result.Add(dto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetStaffAsync error");
            }
            return result;
        }

        public async Task<ApiResponse<StaffListItemDto>> CreateStaffAsync(StaffCreateDto dto)
        {
            if (dto.RoleId != 3 && dto.RoleId != 4)
            {
                return new ApiResponse<StaffListItemDto> { Success = false, Message = "Chỉ được tạo nhân viên vai trò Kế toán (3) hoặc Nhân viên học vụ (4)" };
            }
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(dto.Email))
            {
                return new ApiResponse<StaffListItemDto> { Success = false, Message = "Thiếu thông tin bắt buộc" };
            }

            try
            {
                using var conn = await GetConnectionAsync();
                using var cmd = new OracleCommand
                {
                    Connection = conn,
                    CommandType = CommandType.StoredProcedure,
                    CommandText = dto.RoleId == 4 ? "QLTT_ADMIN.SP_DANG_KY_NHAN_VIEN_HOC_VU" : "QLTT_ADMIN.SP_DANG_KY_KE_TOAN",
                    BindByName = true
                };

                cmd.Parameters.Add("p_ten_dang_nhap", OracleDbType.Varchar2).Value = dto.Username.Trim();
                cmd.Parameters.Add("p_mat_khau", OracleDbType.Varchar2).Value = dto.Password; // Proc sẽ hash và tạo user Oracle
                cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = dto.Email.Trim();
                cmd.Parameters.Add("p_ho_ten", OracleDbType.NVarchar2).Value = (object?)dto.FullName ?? DBNull.Value;
                cmd.Parameters.Add("p_gioi_tinh", OracleDbType.NVarchar2).Value = (object?)dto.Sex ?? DBNull.Value;
                cmd.Parameters.Add("p_sdt", OracleDbType.Varchar2).Value = (object?)dto.Phone ?? DBNull.Value;

                var outMsg = new OracleParameter("p_ket_qua", OracleDbType.NVarchar2, 4000)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(outMsg);

                await cmd.ExecuteNonQueryAsync();

                var msg = outMsg.Value?.ToString() ?? string.Empty;
                var isSuccess = msg.Contains("thành công", StringComparison.OrdinalIgnoreCase);
                if (!isSuccess)
                {
                    return new ApiResponse<StaffListItemDto> { Success = false, Message = string.IsNullOrWhiteSpace(msg) ? "Tạo nhân viên thất bại" : msg };
                }

                // Bảo đảm quyền đăng nhập và role sau khi SP chạy (đề phòng môi trường chưa cấp đủ trong SP)
                try
                {
                    var uname = dto.Username.Trim();
                    var upper = uname.ToUpperInvariant();
                    using (var g1 = new OracleCommand($"ALTER USER \"{upper}\" ACCOUNT UNLOCK", conn))
                    { await g1.ExecuteNonQueryAsync(); }
                    using (var g2 = new OracleCommand($"GRANT CREATE SESSION TO \"{upper}\"", conn))
                    { await g2.ExecuteNonQueryAsync(); }
                    var roleName = dto.RoleId == 4 ? "role_nhanvienhocvu" : "role_ketoan";
                    using (var g3 = new OracleCommand($"GRANT {roleName} TO \"{upper}\"", conn))
                    { await g3.ExecuteNonQueryAsync(); }
                    try
                    {
                        using var prof = new OracleCommand($"ALTER USER \"{upper}\" PROFILE TTTA_USER_PROFILE", conn);
                        await prof.ExecuteNonQueryAsync();
                    }
                    catch { /* profile optional */ }
                }
                catch (Exception exGrant)
                {
                    _logger.LogWarning(exGrant, "Post-SP grants for staff user failed");
                }

                // Nếu có mã nhân viên, cập nhật vào bảng tương ứng sau khi proc tạo xong bản ghi
                if (!string.IsNullOrWhiteSpace(dto.EmployeeCode))
                {
                    if (dto.RoleId == 4)
                    {
                        using var upNv = new OracleCommand(@"UPDATE QLTT_ADMIN.NHAN_VIEN_HOC_VU
                                                              SET MA_NHAN_VIEN = :ma
                                                              WHERE ID_NHAN_VIEN = (SELECT ID_NGUOI_DUNG FROM QLTT_ADMIN.TAI_KHOAN WHERE TEN_DANG_NHAP = :u)", conn)
                        { BindByName = true };
                        upNv.Parameters.Add(":ma", OracleDbType.Varchar2).Value = dto.EmployeeCode.Trim();
                        upNv.Parameters.Add(":u", OracleDbType.Varchar2).Value = dto.Username.Trim();
                        await upNv.ExecuteNonQueryAsync();
                    }
                    else if (dto.RoleId == 3)
                    {
                        using var upKt = new OracleCommand(@"UPDATE QLTT_ADMIN.KE_TOAN
                                                              SET MA_NHAN_VIEN = :ma
                                                              WHERE ID_KE_TOAN = (SELECT ID_NGUOI_DUNG FROM QLTT_ADMIN.TAI_KHOAN WHERE TEN_DANG_NHAP = :u)", conn)
                        { BindByName = true };
                        upKt.Parameters.Add(":ma", OracleDbType.Varchar2).Value = dto.EmployeeCode.Trim();
                        upKt.Parameters.Add(":u", OracleDbType.Varchar2).Value = dto.Username.Trim();
                        await upKt.ExecuteNonQueryAsync();
                    }
                }

                // Lấy lại thông tin nhân viên vừa tạo để trả về
                using var getCmd = new OracleCommand(@"SELECT tk.ID_NGUOI_DUNG, tk.TEN_DANG_NHAP, tk.EMAIL, tk.ID_VAI_TRO, vt.TEN_VAI_TRO,
                                   tk.TRANG_THAI_KICH_HOAT,
                                   COALESCE(nv.HO_TEN, kt.HO_TEN) AS HO_TEN,
                                   nv.GIOI_TINH AS NV_GIOI_TINH,
                                   nv.SO_DIEN_THOAI AS NV_SO_DIEN_THOAI,
                                   kt.GIOI_TINH AS KT_GIOI_TINH,
                                   kt.SO_DIEN_THOAI AS KT_SO_DIEN_THOAI
                        FROM QLTT_ADMIN.TAI_KHOAN tk
                        LEFT JOIN QLTT_ADMIN.VAI_TRO vt ON vt.ID_VAI_TRO = tk.ID_VAI_TRO
                        LEFT JOIN QLTT_ADMIN.NHAN_VIEN_HOC_VU nv ON nv.ID_NHAN_VIEN = tk.ID_NGUOI_DUNG
                        LEFT JOIN QLTT_ADMIN.KE_TOAN kt ON kt.ID_KE_TOAN = tk.ID_NGUOI_DUNG
                        WHERE tk.TEN_DANG_NHAP = :u", conn)
                { BindByName = true };
                getCmd.Parameters.Add(":u", OracleDbType.Varchar2).Value = dto.Username.Trim();
                using var rdr = await getCmd.ExecuteReaderAsync();
                StaffListItemDto? created = null;
                if (await rdr.ReadAsync())
                {
                    created = new StaffListItemDto
                    {
                        UserId = rdr.GetInt32(rdr.GetOrdinal("ID_NGUOI_DUNG")),
                        Username = rdr.GetString(rdr.GetOrdinal("TEN_DANG_NHAP")),
                        Email = rdr.GetString(rdr.GetOrdinal("EMAIL")),
                        RoleId = rdr.GetInt32(rdr.GetOrdinal("ID_VAI_TRO")),
                        RoleName = rdr.IsDBNull(rdr.GetOrdinal("TEN_VAI_TRO")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("TEN_VAI_TRO")),
                        IsActive = rdr.GetInt32(rdr.GetOrdinal("TRANG_THAI_KICH_HOAT")) == 1,
                        FullName = rdr.IsDBNull(rdr.GetOrdinal("HO_TEN")) ? null : rdr.GetString(rdr.GetOrdinal("HO_TEN")),
                        Sex = !rdr.IsDBNull(rdr.GetOrdinal("NV_GIOI_TINH")) ? rdr.GetString(rdr.GetOrdinal("NV_GIOI_TINH")) : (!rdr.IsDBNull(rdr.GetOrdinal("KT_GIOI_TINH")) ? rdr.GetString(rdr.GetOrdinal("KT_GIOI_TINH")) : null),
                        Phone = !rdr.IsDBNull(rdr.GetOrdinal("NV_SO_DIEN_THOAI")) ? rdr.GetString(rdr.GetOrdinal("NV_SO_DIEN_THOAI")) : (!rdr.IsDBNull(rdr.GetOrdinal("KT_SO_DIEN_THOAI")) ? rdr.GetString(rdr.GetOrdinal("KT_SO_DIEN_THOAI")) : null)
                    };
                }

                return new ApiResponse<StaffListItemDto>
                {
                    Success = true,
                    Message = string.IsNullOrWhiteSpace(msg) ? "Thêm nhân viên thành công" : msg,
                    Data = created ?? new StaffListItemDto
                    {
                        Username = dto.Username.Trim(),
                        Email = dto.Email.Trim(),
                        RoleId = dto.RoleId,
                        RoleName = dto.RoleId == 3 ? "KeToan" : "NhanVienHocVu",
                        IsActive = true,
                        FullName = dto.FullName,
                        Sex = dto.Sex,
                        Phone = dto.Phone
                    }
                };
            }
            catch (OracleException oex)
            {
                _logger.LogError(oex, "CreateStaffAsync Oracle error");
                return new ApiResponse<StaffListItemDto> { Success = false, Message = oex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateStaffAsync error");
                return new ApiResponse<StaffListItemDto> { Success = false, Message = "Có lỗi xảy ra" };
            }
        }

        public async Task<ApiResponse<StaffListItemDto>> UpdateStaffAsync(int id, StaffUpdateDto dto)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                using var tx = conn.BeginTransaction();
                try
                {
                    using (var chk = new OracleCommand("SELECT ID_VAI_TRO FROM QLTT_ADMIN.TAI_KHOAN WHERE ID_NGUOI_DUNG = :id", conn) { BindByName = true, Transaction = tx })
                    {
                        chk.Parameters.Add(":id", OracleDbType.Int32).Value = id;
                        var obj = await chk.ExecuteScalarAsync();
                        if (obj == null)
                        {
                            return new ApiResponse<StaffListItemDto> { Success = false, Message = "Không tìm thấy nhân viên" };
                        }
                    }

                    using (var up = new OracleCommand("UPDATE QLTT_ADMIN.TAI_KHOAN SET EMAIL = :e, ID_VAI_TRO = :r WHERE ID_NGUOI_DUNG = :id", conn) { BindByName = true, Transaction = tx })
                    {
                        up.Parameters.Add(":e", OracleDbType.Varchar2).Value = dto.Email?.Trim();
                        up.Parameters.Add(":r", OracleDbType.Int32).Value = dto.RoleId;
                        up.Parameters.Add(":id", OracleDbType.Int32).Value = id;
                        await up.ExecuteNonQueryAsync();
                    }

                    if (dto.RoleId == 4)
                    {
                        using var delKt = new OracleCommand("DELETE FROM QLTT_ADMIN.KE_TOAN WHERE ID_KE_TOAN = :id", conn) { BindByName = true, Transaction = tx }; delKt.Parameters.Add(":id", OracleDbType.Int32).Value = id; await delKt.ExecuteNonQueryAsync();
                        using var mergeNv = new OracleCommand(@"MERGE INTO QLTT_ADMIN.NHAN_VIEN_HOC_VU t USING (SELECT :id AS ID_NHAN_VIEN FROM DUAL) s
                                                               ON (t.ID_NHAN_VIEN = s.ID_NHAN_VIEN)
                                                               WHEN MATCHED THEN UPDATE SET HO_TEN=:ht, GIOI_TINH=:sex, SO_DIEN_THOAI=:phone
                                                               WHEN NOT MATCHED THEN INSERT (ID_NHAN_VIEN,HO_TEN,GIOI_TINH,SO_DIEN_THOAI) VALUES (:id,:ht,:sex,:phone)", conn) { BindByName = true, Transaction = tx };
                        mergeNv.Parameters.Add(":id", OracleDbType.Int32).Value = id;
                        mergeNv.Parameters.Add(":ht", OracleDbType.NVarchar2).Value = (object?)dto.FullName ?? DBNull.Value;
                        mergeNv.Parameters.Add(":sex", OracleDbType.NVarchar2).Value = (object?)dto.Sex ?? DBNull.Value;
                        mergeNv.Parameters.Add(":phone", OracleDbType.Varchar2).Value = (object?)dto.Phone ?? DBNull.Value;
                        await mergeNv.ExecuteNonQueryAsync();
                    }
                    else if (dto.RoleId == 3)
                    {
                        using var delNv = new OracleCommand("DELETE FROM QLTT_ADMIN.NHAN_VIEN_HOC_VU WHERE ID_NHAN_VIEN = :id", conn) { BindByName = true, Transaction = tx }; delNv.Parameters.Add(":id", OracleDbType.Int32).Value = id; await delNv.ExecuteNonQueryAsync();
                        using var mergeKt = new OracleCommand(@"MERGE INTO QLTT_ADMIN.KE_TOAN t USING (SELECT :id AS ID_KE_TOAN FROM DUAL) s
                                                               ON (t.ID_KE_TOAN = s.ID_KE_TOAN)
                                                               WHEN MATCHED THEN UPDATE SET HO_TEN=:ht, GIOI_TINH=:sex, SO_DIEN_THOAI=:phone
                                                               WHEN NOT MATCHED THEN INSERT (ID_KE_TOAN,HO_TEN,GIOI_TINH,SO_DIEN_THOAI) VALUES (:id,:ht,:sex,:phone)", conn) { BindByName = true, Transaction = tx };
                        mergeKt.Parameters.Add(":id", OracleDbType.Int32).Value = id;
                        mergeKt.Parameters.Add(":ht", OracleDbType.NVarchar2).Value = (object?)dto.FullName ?? DBNull.Value;
                        mergeKt.Parameters.Add(":sex", OracleDbType.NVarchar2).Value = (object?)dto.Sex ?? DBNull.Value;
                        mergeKt.Parameters.Add(":phone", OracleDbType.Varchar2).Value = (object?)dto.Phone ?? DBNull.Value;
                        await mergeKt.ExecuteNonQueryAsync();
                    }

                    tx.Commit();

                    return new ApiResponse<StaffListItemDto>
                    {
                        Success = true,
                        Message = "Cập nhật nhân viên thành công",
                        Data = new StaffListItemDto
                        {
                            UserId = id,
                            Username = dto.Username ?? string.Empty,
                            Email = dto.Email ?? string.Empty,
                            RoleId = dto.RoleId,
                            RoleName = dto.RoleId == 3 ? "KeToan" : (dto.RoleId == 4 ? "NhanVienHocVu" : string.Empty),
                            IsActive = true,
                            FullName = dto.FullName,
                            Sex = dto.Sex,
                            Phone = dto.Phone
                        }
                    };
                }
                catch (Exception innerEx)
                {
                    try { tx.Rollback(); } catch { }
                    _logger.LogError(innerEx, "UpdateStaffAsync rollback");
                    return new ApiResponse<StaffListItemDto> { Success = false, Message = "Lỗi cập nhật: " + innerEx.Message };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateStaffAsync error");
                return new ApiResponse<StaffListItemDto> { Success = false, Message = "Có lỗi xảy ra" };
            }
        }

        public async Task<ApiResponse<bool>> LockStaffAsync(int id) => await SetActiveAsync(id, false);
        public async Task<ApiResponse<bool>> UnlockStaffAsync(int id) => await SetActiveAsync(id, true);

        private async Task<ApiResponse<bool>> SetActiveAsync(int id, bool active)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                using var cmd = new OracleCommand("UPDATE QLTT_ADMIN.TAI_KHOAN SET TRANG_THAI_KICH_HOAT = :v WHERE ID_NGUOI_DUNG = :id", conn) { BindByName = true };
                cmd.Parameters.Add(":v", OracleDbType.Int32).Value = active ? 1 : 0;
                cmd.Parameters.Add(":id", OracleDbType.Int32).Value = id;
                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy nhân viên" };
                }
                return new ApiResponse<bool> { Success = true, Message = active ? "Đã mở khóa" : "Đã khóa", Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SetActiveAsync error");
                return new ApiResponse<bool> { Success = false, Message = "Có lỗi xảy ra" };
            }
        }
    }
}
