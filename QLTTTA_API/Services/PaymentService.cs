using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;
using System.Data;

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

        public async Task<ApiResponse<bool>> StudentRequestPaymentAsync(int invoiceId)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                
                using var chkCmd = new OracleCommand("SELECT TRANG_THAI FROM QLTT_ADMIN.HOA_DON WHERE ID_HOA_DON = :id", conn) { BindByName = true };
                chkCmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
                var status = (await chkCmd.ExecuteScalarAsync())?.ToString();

                if (status == null) return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy hóa đơn" };
                if (status == "Đã thanh toán") return new ApiResponse<bool> { Success = false, Message = "Hóa đơn đã được thanh toán" };

                using var cmd = new OracleCommand("SP_UPDATE_INVOICE_STATUS", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = invoiceId;
                cmd.Parameters.Add("p_status", OracleDbType.NVarchar2).Value = "Chờ xác nhận thanh toán";
                var pRows = cmd.Parameters.Add("p_rows", OracleDbType.Int32);
                pRows.Direction = ParameterDirection.Output;
                await cmd.ExecuteNonQueryAsync();

                return new ApiResponse<bool> { Success = true, Message = "Đã gửi yêu cầu xác nhận thanh toán. Vui lòng chờ kế toán xác nhận." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StudentRequestPaymentAsync failed");
                return new ApiResponse<bool> { Success = false, Message = "Lỗi: " + ex.Message };
            }
        }

        public async Task<List<PendingPaymentDto>> GetPendingPaymentsAsync()
        {
            try
            {
                using var conn = await GetAdminConnectionAsync();
                using var cmd = new OracleCommand("SP_GET_PENDING_PAYMENTS", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor).Direction = ParameterDirection.Output;
                
                var list = new List<PendingPaymentDto>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(MapToObject<PendingPaymentDto>(reader));
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPendingPaymentsAsync failed");
                return new List<PendingPaymentDto>();
            }
        }

        public async Task<ApiResponse<bool>> AccountantConfirmPaymentAsync(int invoiceId, int accountantId)
        {
            try
            {
                using var conn = await GetAdminConnectionAsync();
                
                using var getInvCmd = new OracleCommand("SELECT SO_TIEN, TRANG_THAI FROM QLTT_ADMIN.HOA_DON WHERE ID_HOA_DON = :id", conn) { BindByName = true };
                getInvCmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
                using var reader = await getInvCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy hóa đơn" };
                
                var amount = Convert.ToInt32(reader.GetValue(0));
                var status = reader.GetString(1);
                if (status != "Chờ xác nhận thanh toán") return new ApiResponse<bool> { Success = false, Message = "Hóa đơn không ở trạng thái chờ xác nhận" };

                using var cmd = new OracleCommand("SP_CREATE_PAYMENT_RECEIPT", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("p_invoice_id", OracleDbType.Int32).Value = invoiceId;
                cmd.Parameters.Add("p_amount", OracleDbType.Decimal).Value = amount;
                cmd.Parameters.Add("p_acc_id", OracleDbType.Int32).Value = accountantId;
                await cmd.ExecuteNonQueryAsync();

                return new ApiResponse<bool> { Success = true, Message = "Đã xác nhận thanh toán thành công" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AccountantConfirmPaymentAsync failed");
                return new ApiResponse<bool> { Success = false, Message = "Lỗi: " + ex.Message };
            }
        }

        public async Task<ApiResponse<Payment>> CreateAsync(PaymentCreateDto dto)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                
                using (var chkInv = new OracleCommand("SELECT TRANG_THAI FROM QLTT_ADMIN.HOA_DON WHERE ID_HOA_DON = :id", conn) { BindByName = true })
                {
                    chkInv.Parameters.Add(":id", OracleDbType.Int32).Value = dto.InvoiceId;
                    var st = (await chkInv.ExecuteScalarAsync())?.ToString();
                    if (st == null) return new ApiResponse<Payment> { Success = false, Message = "Không tìm thấy hóa đơn" };
                }

                using var cmd = new OracleCommand("SP_CREATE_PAYMENT", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("p_inv_id", OracleDbType.Int32).Value = dto.InvoiceId;
                cmd.Parameters.Add("p_amt", OracleDbType.Decimal).Value = dto.Amount;
                cmd.Parameters.Add("p_method", OracleDbType.NVarchar2).Value = dto.PaymentMethod;
                cmd.Parameters.Add("p_acc_id", OracleDbType.Int32).Value = dto.AccountantId;
                var pOutId = cmd.Parameters.Add("p_out_id", OracleDbType.Int32);
                pOutId.Direction = ParameterDirection.Output;
                await cmd.ExecuteNonQueryAsync();

                int newId = 0;
                if (pOutId.Value != null && int.TryParse(pOutId.Value.ToString(), out var i)) newId = i;

                if (newId > 0)
                {
                     var payments = await ExecuteStoredProcedureQueryAsync<Payment>("SP_GET_PAYMENTS_BY_INVOICE", new { p_inv_id = dto.InvoiceId });
                     var p = payments.FirstOrDefault(x => x.PaymentId == newId) ?? payments.FirstOrDefault();
                     
                     return new ApiResponse<Payment> { Success = true, Message = "Tạo thanh toán thành công", Data = p };
                }
                return new ApiResponse<Payment> { Success = false, Message = "Không thể tạo thanh toán" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create payment failed");
                return new ApiResponse<Payment> { Success = false, Message = "Lỗi tạo thanh toán" };
            }
        }

        public async Task<List<Payment>> GetByInvoiceAsync(int invoiceId)
        {
            return await ExecuteStoredProcedureQueryAsync<Payment>("SP_GET_PAYMENTS_BY_INVOICE", new { p_inv_id = invoiceId });
        }
    }

    public class PendingPaymentDto
    {
        public int InvoiceId { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public int Amount { get; set; }
        public DateTime CreatedDate { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string RegistrationCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string TenHocVien { set { StudentName = value; } } // Map from SP result
        public string MaDangKy { set { RegistrationCode = value; } } // Map from SP result
        public string TenKhoaHoc { set { CourseName = value; } } // Map from SP result
        public string MaHoaDon { set { InvoiceCode = value; } } // Map from SP result
        public int SoTien { set { Amount = value; } } // Map from SP result
        public DateTime NgayTao { set { CreatedDate = value; } } // Map from SP result
    }
}
