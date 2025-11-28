using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;

namespace QLTTTA_API.Services
{
    public interface IPaymentService
    {
        Task<ApiResponse<Payment>> CreateAsync(PaymentCreateDto dto);
        Task<List<Payment>> GetByInvoiceAsync(int invoiceId);
        Task<ApiResponse<bool>> StudentRequestPaymentAsync(int invoiceId);
        Task<List<PendingPaymentDto>> GetPendingPaymentsAsync();
        Task<ApiResponse<bool>> AccountantConfirmPaymentAsync(int invoiceId, int accountantId);
    }

    public class PaymentService : BaseService, IPaymentService
    {
        public PaymentService(IConfiguration configuration, ILogger<PaymentService> logger, IOracleConnectionProvider userConnProvider)
            : base(configuration, logger, userConnProvider) { }

        // Học viên gửi yêu cầu xác nhận thanh toán
        public async Task<ApiResponse<bool>> StudentRequestPaymentAsync(int invoiceId)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                // Kiểm tra hóa đơn tồn tại và chưa thanh toán
                using var chkCmd = new OracleCommand(@"SELECT TRANG_THAI FROM QLTT_ADMIN.HOA_DON WHERE ID_HOA_DON = :id", conn) { BindByName = true };
                chkCmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
                var status = (await chkCmd.ExecuteScalarAsync())?.ToString();

                if (status == null)
                    return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy hóa đơn" };

                if (status == "Đã thanh toán")
                    return new ApiResponse<bool> { Success = false, Message = "Hóa đơn đã được thanh toán" };

                // Cập nhật trạng thái hóa đơn sang "Chờ xác nhận thanh toán"
                using var updateCmd = new OracleCommand(@"UPDATE QLTT_ADMIN.HOA_DON SET TRANG_THAI = N'Chờ xác nhận thanh toán' WHERE ID_HOA_DON = :id", conn) { BindByName = true };
                updateCmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
                await updateCmd.ExecuteNonQueryAsync();

                return new ApiResponse<bool> { Success = true, Message = "Đã gửi yêu cầu xác nhận thanh toán. Vui lòng chờ kế toán xác nhận." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StudentRequestPaymentAsync failed for invoice {InvoiceId}", invoiceId);
                return new ApiResponse<bool> { Success = false, Message = "Lỗi: " + ex.Message };
            }
        }

        // Lấy danh sách hóa đơn chờ xác nhận thanh toán (cho kế toán)
        public async Task<List<PendingPaymentDto>> GetPendingPaymentsAsync()
        {
            var list = new List<PendingPaymentDto>();
            try
            {
                // Use Admin connection to see all pending payments (bypass VPD if any)
                using var conn = await GetAdminConnectionAsync();
                var sql = @"SELECT hd.ID_HOA_DON, hd.MA_HOA_DON, hd.SO_TIEN, hd.NGAY_TAO,
                                   hv.HO_TEN as TEN_HOC_VIEN, dk.MA_DANG_KY,
                                   kh.TEN_KHOA_HOC
                        FROM QLTT_ADMIN.HOA_DON hd
                        JOIN QLTT_ADMIN.DON_DANG_KY dk ON dk.ID_DANG_KY = hd.ID_DANG_KY
                        JOIN QLTT_ADMIN.HOC_VIEN hv ON hv.ID_HOC_VIEN = dk.ID_HOC_VIEN
                        JOIN QLTT_ADMIN.LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
                        JOIN QLTT_ADMIN.KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
                            WHERE hd.TRANG_THAI = N'Chờ xác nhận thanh toán'
                            ORDER BY hd.NGAY_TAO DESC";

                using var cmd = new OracleCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new PendingPaymentDto
                    {
                        InvoiceId = reader.GetInt32(0),
                        InvoiceCode = reader.GetString(1),
                        Amount = Convert.ToInt32(reader.GetValue(2)),
                        CreatedDate = reader.GetDateTime(3),
                        StudentName = reader.GetString(4),
                        RegistrationCode = reader.GetString(5),
                        CourseName = reader.GetString(6)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPendingPaymentsAsync failed");
            }
            return list;
        }

        // Kế toán xác nhận thanh toán
        public async Task<ApiResponse<bool>> AccountantConfirmPaymentAsync(int invoiceId, int accountantId)
        {
            try
            {
                // Use Admin connection for confirmation
                using var conn = await GetAdminConnectionAsync();

                // Lấy thông tin hóa đơn
                using var getInvCmd = new OracleCommand(@"SELECT SO_TIEN, TRANG_THAI FROM QLTT_ADMIN.HOA_DON WHERE ID_HOA_DON = :id", conn) { BindByName = true };
                getInvCmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
                using var reader = await getInvCmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy hóa đơn" };

                var amount = Convert.ToInt32(reader.GetValue(0));
                var status = reader.GetString(1);

                if (status != "Chờ xác nhận thanh toán")
                    return new ApiResponse<bool> { Success = false, Message = "Hóa đơn không ở trạng thái chờ xác nhận" };

                // Tạo phiếu thanh toán
                using var insertCmd = new OracleCommand(@"INSERT INTO QLTT_ADMIN.PHIEU_THANH_TOAN 
                    (NGAY_THANH_TOAN, SO_TIEN_DA_TRA, PHUONG_THUC_THANH_TOAN, ID_HOA_DON, ID_KE_TOAN_XAC_NHAN)
                    VALUES (SYSDATE, :amount, N'Chuyển khoản', :invoiceId, :accountantId)", conn) { BindByName = true };
                insertCmd.Parameters.Add(":amount", OracleDbType.Decimal).Value = amount;
                insertCmd.Parameters.Add(":invoiceId", OracleDbType.Int32).Value = invoiceId;
                insertCmd.Parameters.Add(":accountantId", OracleDbType.Int32).Value = accountantId;
                await insertCmd.ExecuteNonQueryAsync();

                // Cập nhật trạng thái hóa đơn
                using var updateCmd = new OracleCommand(@"UPDATE QLTT_ADMIN.HOA_DON SET TRANG_THAI = N'Đã thanh toán' WHERE ID_HOA_DON = :id", conn) { BindByName = true };
                updateCmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
                await updateCmd.ExecuteNonQueryAsync();

                return new ApiResponse<bool> { Success = true, Message = "Đã xác nhận thanh toán thành công" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AccountantConfirmPaymentAsync failed for invoice {InvoiceId}", invoiceId);
                return new ApiResponse<bool> { Success = false, Message = "Lỗi: " + ex.Message };
            }
        }

        public async Task<ApiResponse<Payment>> CreateAsync(PaymentCreateDto dto)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                // Validate invoice exists
                using (var chkInv = new OracleCommand("SELECT TRANG_THAI FROM QLTT_ADMIN.HOA_DON WHERE ID_HOA_DON = :id", conn) { BindByName = true })
                {
                    chkInv.Parameters.Add(":id", OracleDbType.Int32).Value = dto.InvoiceId;
                    var st = (await chkInv.ExecuteScalarAsync())?.ToString();
                    if (st == null)
                        return new ApiResponse<Payment> { Success = false, Message = "Không tìm thấy hóa đơn" };
                }

                // Insert payment (support IDENTITY PK)
                using var cmd = new OracleCommand(@"INSERT INTO QLTT_ADMIN.PAYMENTS (PAYMENT_DATE, AMOUNT, PAYMENT_METHOD, INVOICE_ID, ACCOUNTANT_ID)
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
                using (var upInv = new OracleCommand("UPDATE QLTT_ADMIN.HOA_DON SET TRANG_THAI = 'Đã thanh toán' WHERE ID_HOA_DON = :id", conn) { BindByName = true })
                {
                    upInv.Parameters.Add(":id", OracleDbType.Int32).Value = dto.InvoiceId;
                    await upInv.ExecuteNonQueryAsync();
                }

                // Fetch created payment
                using var fetch = new OracleCommand(@"SELECT PAYMENT_ID, PAYMENT_DATE, AMOUNT, PAYMENT_METHOD, INVOICE_ID, ACCOUNTANT_ID
                                                      FROM QLTT_ADMIN.PAYMENTS WHERE PAYMENT_ID = :id", conn) { BindByName = true };
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
                        FROM QLTT_ADMIN.PAYMENTS WHERE INVOICE_ID = :id ORDER BY PAYMENT_ID DESC";
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

    // DTO cho danh sách chờ xác nhận
    public class PendingPaymentDto
    {
        public int InvoiceId { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public int Amount { get; set; }
        public DateTime CreatedDate { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string RegistrationCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
    }
}
