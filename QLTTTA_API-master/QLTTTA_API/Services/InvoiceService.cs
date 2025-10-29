using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;

namespace QLTTTA_API.Services
{
    public interface IInvoiceService
    {
        Task<ApiResponse<Invoice>> CreateAsync(int registrationId, DateTime dueDate, int amount);
        Task<Invoice?> GetByRegistrationAsync(int registrationId);
    }

    public class InvoiceService : BaseService, IInvoiceService
    {
        public InvoiceService(IConfiguration configuration, ILogger<InvoiceService> logger, IOracleConnectionProvider userConnProvider)
            : base(configuration, logger, userConnProvider) { }

        public async Task<ApiResponse<Invoice>> CreateAsync(int registrationId, DateTime dueDate, int amount)
        {
            try
            {
                // Dùng kết nối per-user (kế toán) để DB kiểm soát quyền
                using var conn = await GetConnectionAsync();

                // Kiểm tra đơn đã duyệt và chưa có hóa đơn
                using (var chk = new OracleCommand(@"SELECT TRANG_THAI FROM DON_DANG_KY WHERE ID_DANG_KY=:id", conn) { BindByName = true })
                {
                    chk.Parameters.Add(":id", OracleDbType.Int32).Value = registrationId;
                    var st = (await chk.ExecuteScalarAsync())?.ToString();
                    if (!string.Equals(st, "Đã duyệt", StringComparison.OrdinalIgnoreCase))
                        return new ApiResponse<Invoice> { Success = false, Message = "Đơn đăng ký chưa được duyệt" };
                }
                using (var chkInv = new OracleCommand(@"SELECT COUNT(*) FROM HOA_DON WHERE ID_DANG_KY=:id", conn) { BindByName = true })
                {
                    chkInv.Parameters.Add(":id", OracleDbType.Int32).Value = registrationId;
                    var cnt = Convert.ToInt32(await chkInv.ExecuteScalarAsync());
                    if (cnt > 0) return new ApiResponse<Invoice> { Success = false, Message = "Đơn đã có hóa đơn" };
                }

                var code = $"HD_{DateTime.UtcNow:yyyyMMddHHmmss}_{Random.Shared.Next(1000, 9999)}";
                using (var cmd = new OracleCommand(@"INSERT INTO HOA_DON (MA_HOA_DON, NGAY_TAO, NGAY_HET_HAN, SO_TIEN, TRANG_THAI, ID_DANG_KY)
                                                     VALUES (:code, SYSDATE, :due, :amt, N'Chưa thanh toán', :rid)
                                                     RETURNING ID_HOA_DON INTO :out_id", conn) { BindByName = true })
                {
                    cmd.Parameters.Add(":code", OracleDbType.Varchar2).Value = code;
                    cmd.Parameters.Add(":due", OracleDbType.Date).Value = dueDate;
                    cmd.Parameters.Add(":amt", OracleDbType.Decimal).Value = amount;
                    cmd.Parameters.Add(":rid", OracleDbType.Int32).Value = registrationId;
                    var outId = new OracleParameter(":out_id", OracleDbType.Int32) { Direction = System.Data.ParameterDirection.Output };
                    cmd.Parameters.Add(outId);
                    await cmd.ExecuteNonQueryAsync();

                    var invId = Convert.ToInt32(outId.Value?.ToString());
                    using var fetch = new OracleCommand(@"SELECT ID_HOA_DON, MA_HOA_DON, NGAY_TAO, NGAY_HET_HAN, SO_TIEN, TRANG_THAI, ID_DANG_KY FROM HOA_DON WHERE ID_HOA_DON=:id", conn) { BindByName = true };
                    fetch.Parameters.Add(":id", OracleDbType.Int32).Value = invId;
                    using var r = await fetch.ExecuteReaderAsync();
                    if (await r.ReadAsync())
                    {
                        var inv = new Invoice
                        {
                            InvoiceId = r.GetInt32(0),
                            InvoiceCode = r.GetString(1),
                            CreatedDate = r.GetDateTime(2),
                            DueDate = r.GetDateTime(3),
                            Amount = Convert.ToInt32(r.GetValue(4)),
                            Status = r.GetString(5),
                            RegistrationId = r.GetInt32(6)
                        };
                        return new ApiResponse<Invoice> { Success = true, Message = "Tạo hóa đơn thành công", Data = inv };
                    }
                }
                return new ApiResponse<Invoice> { Success = false, Message = "Không thể tạo hóa đơn" };
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                return new ApiResponse<Invoice> { Success = false, Message = "Bạn không có quyền tạo hóa đơn" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create invoice failed for registration {RegId}", registrationId);
                return new ApiResponse<Invoice> { Success = false, Message = "Lỗi tạo hóa đơn" };
            }
        }

        public async Task<Invoice?> GetByRegistrationAsync(int registrationId)
        {
            var sql = @"SELECT ID_HOA_DON, MA_HOA_DON, NGAY_TAO, NGAY_HET_HAN, SO_TIEN, TRANG_THAI, ID_DANG_KY
                        FROM HOA_DON WHERE ID_DANG_KY=:rid";
            using var conn = await GetConnectionAsync();
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":rid", OracleDbType.Int32).Value = registrationId;
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return new Invoice
                {
                    InvoiceId = r.GetInt32(0),
                    InvoiceCode = r.GetString(1),
                    CreatedDate = r.GetDateTime(2),
                    DueDate = r.GetDateTime(3),
                    Amount = Convert.ToInt32(r.GetValue(4)),
                    Status = r.GetString(5),
                    RegistrationId = r.GetInt32(6)
                };
            }
            return null;
        }
    }
}
