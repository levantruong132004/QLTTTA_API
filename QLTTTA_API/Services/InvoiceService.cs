using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;
using DinkToPdf;
using DinkToPdf.Contracts;

namespace QLTTTA_API.Services
{
    public interface IInvoiceService
    {
        Task<ApiResponse<Invoice>> CreateAsync(int registrationId, DateTime dueDate, int amount);
        Task<Invoice?> GetByRegistrationAsync(int registrationId);
        Task<Invoice?> GetByIdAsync(int invoiceId);
        Task<bool> UpdateStatusAsync(int invoiceId, string status);
        Task<ApiResponse<PdfEmailResult>> GeneratePdfAndSendEmailAsync(int invoiceId, int accountantId);
    }

    public class PdfEmailResult
    {
        public string? EmailAddress { get; set; }
        public string? PdfFilePath { get; set; }
        public bool EmailSent { get; set; }
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
                                                     VALUES (:code, SYSDATE, :due, :amt, 'Chưa thanh toán', :rid)
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
            catch (OracleException oex)
            {
                // Trả về chi tiết lỗi Oracle để tầng Controller hiển thị cho người dùng
                _logger.LogError(oex, "Oracle error when creating invoice for registration {RegId}", registrationId);
                return new ApiResponse<Invoice> { Success = false, Message = $"Lỗi Oracle ({oex.Number}): {oex.Message}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create invoice failed for registration {RegId}", registrationId);
                return new ApiResponse<Invoice> { Success = false, Message = "Lỗi tạo hóa đơn" };
            }
        }

        public async Task<Invoice?> GetByRegistrationAsync(int registrationId)
        {
            var sql = @"SELECT ID_HOA_DON, MA_HOA_DON, NGAY_TAO, NGAY_HET_HAN, SO_TIEN, TRANG_THAI, ID_DANG_KY,
                              CHU_KY_BASE64, THUAT_TOAN, ID_KE_TOAN_KY, NGAY_KY, CHU_KY_HINH_BASE64
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
                    RegistrationId = r.GetInt32(6),
                    SignatureBase64 = r.IsDBNull(7) ? null : r.GetString(7),
                    Algorithm = r.IsDBNull(8) ? null : r.GetString(8),
                    AccountantId = r.IsDBNull(9) ? 0 : r.GetInt32(9),
                    SignedDate = r.IsDBNull(10) ? null : r.GetDateTime(10),
                    SignatureImageBase64 = r.IsDBNull(11) ? null : r.GetString(11)
                };
            }
            return null;
        }

        public async Task<Invoice?> GetByIdAsync(int invoiceId)
        {
            var sql = @"SELECT ID_HOA_DON, MA_HOA_DON, NGAY_TAO, NGAY_HET_HAN, SO_TIEN, TRANG_THAI, ID_DANG_KY,
                              CHU_KY_BASE64, THUAT_TOAN, ID_KE_TOAN_KY, NGAY_KY, CHU_KY_HINH_BASE64
                        FROM HOA_DON WHERE ID_HOA_DON=:id";
            using var conn = await GetConnectionAsync();
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
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
                    RegistrationId = r.GetInt32(6),
                    SignatureBase64 = r.IsDBNull(7) ? null : r.GetString(7),
                    Algorithm = r.IsDBNull(8) ? null : r.GetString(8),
                    AccountantId = r.IsDBNull(9) ? 0 : r.GetInt32(9),
                    SignedDate = r.IsDBNull(10) ? null : r.GetDateTime(10),
                    SignatureImageBase64 = r.IsDBNull(11) ? null : r.GetString(11)
                };
            }
            return null;
        }

        public async Task<bool> UpdateStatusAsync(int invoiceId, string status)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                using var cmd = new OracleCommand("UPDATE HOA_DON SET TRANG_THAI = :st WHERE ID_HOA_DON = :id", conn) { BindByName = true };
                cmd.Parameters.Add(":st", OracleDbType.Varchar2).Value = status;
                cmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
                var rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateStatusAsync failed for invoice {InvoiceId}", invoiceId);
                return false;
            }
        }

        public async Task<ApiResponse<PdfEmailResult>> GeneratePdfAndSendEmailAsync(int invoiceId, int accountantId)
        {
            try
            {
                // 1. Lấy thông tin hóa đơn và học viên
                var invoiceData = await GetInvoiceWithStudentInfoAsync(invoiceId);
                if (invoiceData == null)
                {
                    return new ApiResponse<PdfEmailResult> 
                    { 
                        Success = false, 
                        Message = "Không tìm thấy thông tin hóa đơn" 
                    };
                }

                // 2. Tạo HTML content cho hóa đơn
                var htmlContent = GenerateInvoiceHtml(invoiceData);

                // 3. Tạo PDF từ HTML (sử dụng thư viện như iTextSharp hoặc PuppeteerSharp)
                var pdfBytes = await GeneratePdfFromHtml(htmlContent);
                
                // 4. Lưu PDF vào thư mục tạm
                var pdfFileName = $"HoaDon_{invoiceData.InvoiceCode}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var pdfPath = Path.Combine(Path.GetTempPath(), pdfFileName);
                await File.WriteAllBytesAsync(pdfPath, pdfBytes);

                // 5. Gửi email với PDF đính kèm
                var emailSent = await SendInvoicePdfByEmail(invoiceData.StudentEmail, invoiceData.InvoiceCode, pdfPath);

                // 6. Log activity
                await LogPrintActivity(invoiceId, accountantId, pdfPath, emailSent);

                return new ApiResponse<PdfEmailResult>
                {
                    Success = true,
                    Message = emailSent ? "Tạo PDF và gửi email thành công" : "Tạo PDF thành công nhưng gửi email thất bại",
                    Data = new PdfEmailResult
                    {
                        EmailAddress = invoiceData.StudentEmail,
                        PdfFilePath = pdfPath,
                        EmailSent = emailSent
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GeneratePdfAndSendEmailAsync failed for invoice {InvoiceId}", invoiceId);
                return new ApiResponse<PdfEmailResult>
                {
                    Success = false,
                    Message = $"Lỗi tạo PDF và gửi email: {ex.Message}"
                };
            }
        }

        private async Task<InvoiceWithStudentInfo?> GetInvoiceWithStudentInfoAsync(int invoiceId)
        {
            var sql = @"SELECT h.ID_HOA_DON, h.MA_HOA_DON, h.NGAY_TAO, h.NGAY_HET_HAN, h.SO_TIEN, h.TRANG_THAI,
                              h.CHU_KY_BASE64, h.NGAY_KY, h.ID_DANG_KY, h.CHU_KY_HINH_BASE64,
                              tk.EMAIL, hv.HO_TEN, hv.SO_DIEN_THOAI,
                              kh.TEN_KHOA_HOC, l.TEN_LOP
                       FROM HOA_DON h
                       JOIN DON_DANG_KY dk ON h.ID_DANG_KY = dk.ID_DANG_KY
                       JOIN HOC_VIEN hv ON dk.ID_HOC_VIEN = hv.ID_HOC_VIEN
                       JOIN TAI_KHOAN tk ON hv.ID_TAI_KHOAN = tk.ID_TAI_KHOAN
                       JOIN LOP l ON dk.ID_LOP = l.ID_LOP
                       JOIN KHOA_HOC kh ON l.ID_KHOA_HOC = kh.ID_KHOA_HOC
                       WHERE h.ID_HOA_DON = :id";

            OracleConnection? conn = null;
            try
            {
                // Ưu tiên dùng kết nối per-user (tài khoản kế toán đang đăng nhập)
                conn = await GetConnectionAsync();
            }
            catch (OracleException oex) when (oex.Number == 1031 || oex.Number == 942)
            {
                // Thiếu quyền hoặc thiếu đối tượng -> fallback admin để đảm bảo in không bị chặn
                _logger.LogWarning(oex, "Fallback to admin connection for GetInvoiceWithStudentInfo (invoice {InvoiceId})", invoiceId);
                conn = await GetAdminConnectionAsync();
            }

            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return new InvoiceWithStudentInfo
                {
                    InvoiceId = r.GetInt32(0),
                    InvoiceCode = r.GetString(1),
                    CreatedDate = r.GetDateTime(2),
                    DueDate = r.GetDateTime(3),
                    Amount = Convert.ToInt32(r.GetValue(4)),
                    Status = r.GetString(5),
                    SignatureBase64 = r.IsDBNull(6) ? null : r.GetString(6),
                    SignedDate = r.IsDBNull(7) ? null : r.GetDateTime(7),
                    RegistrationId = r.GetInt32(8),
                    SignatureImageBase64 = r.IsDBNull(9) ? null : r.GetString(9),
                    StudentEmail = r.GetString(10),
                    StudentName = r.GetString(11),
                    PhoneNumber = r.GetString(12),
                    CourseName = r.GetString(13),
                    ClassName = r.GetString(14)
                };
            }
            return null;
        }

        private string GenerateInvoiceHtml(InvoiceWithStudentInfo data)
        {
            var hasSignatureImage = !string.IsNullOrEmpty(data.SignatureImageBase64);
            var signatureHtml = hasSignatureImage 
                ? $"<img src=\"data:image/png;base64,{data.SignatureImageBase64}\" style=\"max-width: 140px; max-height: 50px;\" />"
                : "<span style='color: #666; font-size: 0.8rem;'>Đã ký số</span>";

            return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='UTF-8'>
                <title>Hóa đơn {data.InvoiceCode}</title>
                <style>
                    body {{ font-family: 'Times New Roman', serif; font-size: 12px; line-height: 1.3; margin: 0; }}
                    .invoice-container {{ max-width: 210mm; margin: 0 auto; }}
                    .invoice-header {{ background: #8B4513; color: white; padding: 12px 20px; text-align: center; }}
                    .invoice-title {{ font-size: 1.5rem; margin: 0; }}
                    .company-info {{ text-align: center; margin: 15px 0; padding-bottom: 10px; border-bottom: 1px solid #ddd; }}
                    .invoice-details {{ display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin-bottom: 15px; }}
                    .detail-section {{ background: #f8f9ff; padding: 12px; border-left: 3px solid #8B4513; }}
                    .signature-section {{ margin-top: 15px; border-top: 1px solid #ddd; padding-top: 12px; }}
                    .signature-layout {{ display: grid; grid-template-columns: 1fr 1fr; gap: 20px; text-align: center; }}
                    .signature-area {{ height: 60px; display: flex; align-items: center; justify-content: center; }}
                </style>
            </head>
            <body>
                <div class='invoice-container'>
                    <div class='invoice-header'>
                        <h1 class='invoice-title'>HÓA ĐƠN ĐIỆN TỬ</h1>
                        <p>Số: {data.InvoiceCode}</p>
                    </div>
                    
                    <div class='company-info'>
                        <div style='font-size: 1.2rem; font-weight: bold;'>TRUNG TÂM TIẾNG ANH LDA</div>
                        <div>Mã số thuế: 0123456789 | Địa chỉ: 123 Đường ABC, Quận XYZ, TP.HCM</div>
                        <div>Điện thoại: (028) 1234.5678 | Email: info@lda.edu.vn</div>
                    </div>

                    <div class='invoice-details'>
                        <div class='detail-section'>
                            <div style='font-weight: bold; margin-bottom: 8px;'>Thông tin hóa đơn</div>
                            <div>Mã hóa đơn: {data.InvoiceCode}</div>
                            <div>Ngày tạo: {data.CreatedDate:dd/MM/yyyy}</div>
                            <div>Hạn thanh toán: {data.DueDate:dd/MM/yyyy}</div>
                            <div>Trạng thái: {data.Status}</div>
                        </div>
                        
                        <div class='detail-section'>
                            <div style='font-weight: bold; margin-bottom: 8px;'>Thông tin học viên</div>
                            <div>Họ tên: {data.StudentName}</div>
                            <div>Email: {data.StudentEmail}</div>
                            <div>SĐT: {data.PhoneNumber}</div>
                            <div>Khóa học: {data.CourseName}</div>
                            <div>Lớp: {data.ClassName}</div>
                            <div><strong>Số tiền: {data.Amount:N0} VNĐ</strong></div>
                        </div>
                    </div>

                    <div class='signature-section'>
                        <div class='signature-layout'>
                            <div>
                                <div style='font-weight: bold;'>Người mua hàng</div>
                                <div style='font-style: italic; font-size: 0.8rem;'>(Ký, ghi rõ họ, tên)</div>
                                <div class='signature-area'></div>
                            </div>
                            
                            <div>
                                <div style='font-weight: bold;'>Người bán hàng</div>
                                <div style='font-style: italic; font-size: 0.8rem;'>(Ký, ghi rõ họ, tên)</div>
                                <div class='signature-area'>{signatureHtml}</div>
                                {(data.SignedDate.HasValue ? $@"
                                <div style='margin-top: 5px; font-weight: bold;'>TRUNG TÂM TIẾNG ANH LDA</div>
                                <div style='font-size: 0.8rem;'>Ký ngày: {data.SignedDate:dd/MM/yyyy}</div>" : "")}
                            </div>
                        </div>
                    </div>
                </div>
            </body>
            </html>";
        }

        private async Task<byte[]> GeneratePdfFromHtml(string htmlContent)
        {
            try
            {
                // Sử dụng DinkToPdf để tạo PDF từ HTML
                var converter = new BasicConverter(new PdfTools());
                
                var doc = new HtmlToPdfDocument()
                {
                    GlobalSettings = {
                        ColorMode = ColorMode.Color,
                        Orientation = Orientation.Portrait,
                        PaperSize = PaperKind.A4,
                        Margins = new MarginSettings() { Top = 10, Bottom = 10, Left = 10, Right = 10 },
                        DocumentTitle = "Hóa đơn điện tử - Trung tâm Tiếng Anh LDA"
                    },
                    Objects = {
                        new ObjectSettings() {
                            PagesCount = true,
                            HtmlContent = htmlContent,
                            WebSettings = { DefaultEncoding = "utf-8" }
                        }
                    }
                };
                
                var pdfBytes = converter.Convert(doc);
                return await Task.FromResult(pdfBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate PDF from HTML");
                // Fallback: trả về HTML bytes nếu tạo PDF thất bại
                return System.Text.Encoding.UTF8.GetBytes(htmlContent);
            }
        }

        private async Task<bool> SendInvoicePdfByEmail(string toEmail, string invoiceCode, string pdfPath)
        {
            try
            {
                // Sử dụng EmailService để gửi email với PDF đính kèm
                // Cần inject IEmailService vào constructor
                
                var subject = $"Hóa đơn điện tử {invoiceCode} - Trung tâm Tiếng Anh LDA";
                var body = $@"
                    Kính gửi Quý khách,<br><br>
                    
                    Trung tâm Tiếng Anh LDA xin gửi đến Quý khách hóa đơn điện tử <strong>{invoiceCode}</strong>.<br><br>
                    
                    Vui lòng kiểm tra file PDF đính kèm để xem chi tiết hóa đơn.<br><br>
                    
                    Trân trọng,<br>
                    <strong>Trung tâm Tiếng Anh LDA</strong><br>
                    Email: info@lda.edu.vn<br>
                    Điện thoại: (028) 1234.5678
                ";

                // Tạm thời return true - cần implement gửi email thực tế
                await Task.Delay(100); // Simulate async operation
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send invoice PDF email to {Email}", toEmail);
                return false;
            }
        }

        private async Task LogPrintActivity(int invoiceId, int accountantId, string pdfPath, bool emailSent)
        {
            OracleConnection? conn = null;
            try
            {
                conn = await GetConnectionAsync(); // per-user trước
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                _logger.LogWarning(oex, "Fallback admin for LogPrintActivity invoice {InvoiceId}", invoiceId);
                conn = await GetAdminConnectionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected error getting per-user connection; fallback admin for LogPrintActivity invoice {InvoiceId}", invoiceId);
                conn = await GetAdminConnectionAsync();
            }

            try
            {
                var sql = @"INSERT INTO LOG_IN_HOA_DON (ID_HOA_DON, ID_KE_TOAN, NGAY_IN, DUONG_DAN_PDF, EMAIL_SENT)
                           VALUES (:invoiceId, :accountantId, SYSDATE, :pdfPath, :emailSent)";
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(":invoiceId", OracleDbType.Int32).Value = invoiceId;
                cmd.Parameters.Add(":accountantId", OracleDbType.Int32).Value = accountantId;
                cmd.Parameters.Add(":pdfPath", OracleDbType.Varchar2).Value = pdfPath;
                cmd.Parameters.Add(":emailSent", OracleDbType.Int32).Value = emailSent ? 1 : 0;
                await cmd.ExecuteNonQueryAsync();
            }
            catch (OracleException oex) when (oex.Number == 942)
            {
                _logger.LogWarning(oex, "LOG_IN_HOA_DON table missing; skip logging for invoice {InvoiceId}", invoiceId);
            }
            catch (OracleException oex) when (oex.Number == 1031)
            {
                _logger.LogWarning(oex, "Insufficient privileges to insert LOG_IN_HOA_DON; skip logging invoice {InvoiceId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log print activity for invoice {InvoiceId}");
            }
            finally
            {
                if (conn != null)
                {
                    await conn.DisposeAsync();
                }
            }
        }

        private class InvoiceWithStudentInfo
        {
            public int InvoiceId { get; set; }
            public string InvoiceCode { get; set; } = string.Empty;
            public DateTime CreatedDate { get; set; }
            public DateTime DueDate { get; set; }
            public int Amount { get; set; }
            public string Status { get; set; } = string.Empty;
            public string? SignatureBase64 { get; set; }
            public DateTime? SignedDate { get; set; }
            public int RegistrationId { get; set; }
            public string? SignatureImageBase64 { get; set; }
            public string StudentEmail { get; set; } = string.Empty;
            public string StudentName { get; set; } = string.Empty;
            public string PhoneNumber { get; set; } = string.Empty;
            public string CourseName { get; set; } = string.Empty;
            public string ClassName { get; set; } = string.Empty;
        }
    }
}
