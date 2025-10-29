using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;

namespace QLTTTA_API.Services
{
    public interface IPaymentService
    {
        Task<ApiResponse<Payment>> CreateAsync(PaymentCreateDto dto);
        Task<List<Payment>> GetByInvoiceAsync(int invoiceId);
    }

    public class PaymentService : BaseService, IPaymentService
    {
        public PaymentService(IConfiguration configuration, ILogger<PaymentService> logger, IOracleConnectionProvider userConnProvider)
            : base(configuration, logger, userConnProvider) { }

        public async Task<ApiResponse<Payment>> CreateAsync(PaymentCreateDto dto)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                // Validate invoice exists
                using (var chkInv = new OracleCommand("SELECT TRANG_THAI FROM HOA_DON WHERE ID_HOA_DON = :id", conn) { BindByName = true })
                {
                    chkInv.Parameters.Add(":id", OracleDbType.Int32).Value = dto.InvoiceId;
                    var st = (await chkInv.ExecuteScalarAsync())?.ToString();
                    if (st == null)
                        return new ApiResponse<Payment> { Success = false, Message = "Không tìm thấy hóa đơn" };
                }

                // Insert payment (support IDENTITY PK)
                using var cmd = new OracleCommand(@"INSERT INTO PAYMENTS (PAYMENT_DATE, AMOUNT, PAYMENT_METHOD, INVOICE_ID, ACCOUNTANT_ID)
                                                   VALUES (SYSDATE, :amt, :mtd, :inv, :acc)
                                                   RETURNING PAYMENT_ID INTO :out_id", conn) { BindByName = true };
                cmd.Parameters.Add(":amt", OracleDbType.Decimal).Value = dto.Amount;
                cmd.Parameters.Add(":mtd", OracleDbType.NVarchar2).Value = dto.PaymentMethod;
                cmd.Parameters.Add(":inv", OracleDbType.Int32).Value = dto.InvoiceId;
                cmd.Parameters.Add(":acc", OracleDbType.Int32).Value = dto.AccountantId;
                var outId = new OracleParameter(":out_id", OracleDbType.Int32) { Direction = System.Data.ParameterDirection.Output };
                cmd.Parameters.Add(outId);
                await cmd.ExecuteNonQueryAsync();
                var paymentId = Convert.ToInt32(outId.Value?.ToString());

                // Update invoice status to paid
                using (var upInv = new OracleCommand("UPDATE HOA_DON SET TRANG_THAI = N'Đã thanh toán' WHERE ID_HOA_DON = :id", conn) { BindByName = true })
                {
                    upInv.Parameters.Add(":id", OracleDbType.Int32).Value = dto.InvoiceId;
                    await upInv.ExecuteNonQueryAsync();
                }

                // Fetch created payment
                using var fetch = new OracleCommand(@"SELECT PAYMENT_ID, PAYMENT_DATE, AMOUNT, PAYMENT_METHOD, INVOICE_ID, ACCOUNTANT_ID
                                                      FROM PAYMENTS WHERE PAYMENT_ID = :id", conn) { BindByName = true };
                fetch.Parameters.Add(":id", OracleDbType.Int32).Value = paymentId;
                using var r = await fetch.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    var p = new Payment
                    {
                        PaymentId = r.GetInt32(0),
                        PaymentDate = r.IsDBNull(1) ? null : r.GetDateTime(1),
                        Amount = Convert.ToInt32(r.GetValue(2)),
                        PaymentMethod = r.IsDBNull(3) ? null : r.GetString(3),
                        InvoiceId = r.GetInt32(4),
                        AccountantId = r.GetInt32(5)
                    };
                    return new ApiResponse<Payment> { Success = true, Message = "Tạo thanh toán thành công", Data = p };
                }
                return new ApiResponse<Payment> { Success = false, Message = "Không thể tạo thanh toán" };
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                return new ApiResponse<Payment> { Success = false, Message = "Bạn không có quyền tạo thanh toán" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create payment failed for invoice {InvoiceId}", dto.InvoiceId);
                return new ApiResponse<Payment> { Success = false, Message = "Lỗi tạo thanh toán" };
            }
        }

        public async Task<List<Payment>> GetByInvoiceAsync(int invoiceId)
        {
            var list = new List<Payment>();
            using var conn = await GetConnectionAsync();
            var sql = @"SELECT PAYMENT_ID, PAYMENT_DATE, AMOUNT, PAYMENT_METHOD, INVOICE_ID, ACCOUNTANT_ID
                        FROM PAYMENTS WHERE INVOICE_ID = :id ORDER BY PAYMENT_ID DESC";
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new Payment
                {
                    PaymentId = r.GetInt32(0),
                    PaymentDate = r.IsDBNull(1) ? null : r.GetDateTime(1),
                    Amount = Convert.ToInt32(r.GetValue(2)),
                    PaymentMethod = r.IsDBNull(3) ? null : r.GetString(3),
                    InvoiceId = r.GetInt32(4),
                    AccountantId = r.GetInt32(5)
                });
            }
            return list;
        }
    }
}
