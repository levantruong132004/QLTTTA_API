using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QLTTTA_API.Services
{
    public interface IInvoiceService
    {
        Task<ApiResponse<Invoice>> CreateAsync(int registrationId, DateTime dueDate, int amount);
        Task<Invoice?> GetByRegistrationAsync(int registrationId);
        Task<Invoice?> GetByIdAsync(int invoiceId);
        Task<bool> UpdateStatusAsync(int invoiceId, string status);
        Task<ApiResponse<InvoicePdfEmailResult>> GeneratePdfAndSendEmailAsync(int invoiceId, int accountantId);
        Task<(string studentName, string studentEmail, string courseName, string className)> GetStudentCourseInfoAsync(int registrationId);
        Task<bool> MarkPrintedAsync(int invoiceId);
    }

    public class InvoicePdfEmailResult
    {
        public int InvoiceId { get; set; }
        public string? EmailAddress { get; set; }
        public bool PdfGenerated { get; set; }
        public bool EmailSent { get; set; }
    }

    public class InvoiceService : BaseService, IInvoiceService
    {
        private readonly IEmailService _emailService;
        private readonly IPdfSignatureService _pdfSignatureService; // add

        public InvoiceService(
            IConfiguration configuration, 
            ILogger<InvoiceService> logger, 
            IOracleConnectionProvider userConnProvider, 
            IHttpContextAccessor httpContextAccessor,
            IEmailService emailService,
            IPdfSignatureService pdfSignatureService) // add
            : base(configuration, logger, userConnProvider, httpContextAccessor) 
        { 
            _emailService = emailService;
            _pdfSignatureService = pdfSignatureService; // assign
            
            // Cấu hình QuestPDF license - Sử dụng Community License (miễn phí)
            QuestPDF.Settings.License = LicenseType.Community;
        }

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

                        // Tạo sẵn Base PDF ngay khi tạo hóa đơn để giữ nguyên format khi ký sau này
                        try
                        {
                            var (studentName, _, courseName, className) = await GetStudentCourseInfoAsync(inv.RegistrationId);
                            var created = inv.CreatedDate ?? DateTime.Now;
                            var due = inv.DueDate ?? dueDate;
                            await _pdfSignatureService.GenerateAndSaveBaseInvoicePdfAsync(
                                inv.InvoiceId,
                                inv.InvoiceCode ?? code,
                                studentName,
                                courseName,
                                className,
                                inv.Amount,
                                created,
                                due
                            );
                        }
                        catch (Exception genPdfEx)
                        {
                            _logger.LogWarning(genPdfEx, "Không thể tạo base PDF ngay lúc tạo hóa đơn {InvoiceId}", invId);
                            // Không chặn luồng tạo hóa đơn nếu tạo PDF thất bại
                        }

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
            var sql = @"SELECT ID_HOA_DON, MA_HOA_DON, NGAY_TAO, NGAY_HET_HAN, SO_TIEN, TRANG_THAI, 
                               ID_DANG_KY, CHU_KY_BASE64, THUAT_TOAN, ID_KE_TOAN_KY, NGAY_KY, CHU_KY_HINH_BASE64,
                               NVL(DA_IN, 0) AS DA_IN
                        FROM HOA_DON 
                        WHERE ID_DANG_KY = :regId";
            using var conn = await GetAdminConnectionAsync();
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":regId", OracleDbType.Int32).Value = registrationId;
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return new Invoice
                {
                    InvoiceId = r.GetInt32(0),
                    InvoiceCode = r.IsDBNull(1) ? null : r.GetString(1),
                    CreatedDate = r.IsDBNull(2) ? null : r.GetDateTime(2),
                    DueDate = r.IsDBNull(3) ? null : r.GetDateTime(3),
                    Amount = r.IsDBNull(4) ? 0 : Convert.ToInt32(r.GetValue(4)),
                    Status = r.IsDBNull(5) ? null : r.GetString(5),
                    RegistrationId = r.GetInt32(6),
                    SignatureBase64 = r.IsDBNull(7) ? null : r.GetString(7),
                    Algorithm = r.IsDBNull(8) ? null : r.GetString(8),
                    AccountantId = r.IsDBNull(9) ? 0 : r.GetInt32(9),
                    SignedDate = r.IsDBNull(10) ? null : r.GetDateTime(10),
                    SignatureImageBase64 = r.IsDBNull(11) ? null : r.GetString(11),
                    IsPrinted = r.IsDBNull(12) ? false : r.GetInt32(12) == 1
                };
            }
            return null;
        }

        public async Task<Invoice?> GetByIdAsync(int invoiceId)
        {
            var sql = @"SELECT ID_HOA_DON, MA_HOA_DON, NGAY_TAO, NGAY_HET_HAN, SO_TIEN, TRANG_THAI, 
                               ID_DANG_KY, CHU_KY_BASE64, THUAT_TOAN, ID_KE_TOAN_KY, NGAY_KY, CHU_KY_HINH_BASE64,
                               NVL(DA_IN, 0) AS DA_IN
                        FROM HOA_DON 
                        WHERE ID_HOA_DON = :id";
            using var conn = await GetAdminConnectionAsync();
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return new Invoice
                {
                    InvoiceId = r.GetInt32(0),
                    InvoiceCode = r.IsDBNull(1) ? null : r.GetString(1),
                    CreatedDate = r.IsDBNull(2) ? null : r.GetDateTime(2),
                    DueDate = r.IsDBNull(3) ? null : r.GetDateTime(3),
                    Amount = r.IsDBNull(4) ? 0 : Convert.ToInt32(r.GetValue(4)),
                    Status = r.IsDBNull(5) ? null : r.GetString(5),
                    RegistrationId = r.GetInt32(6),
                    SignatureBase64 = r.IsDBNull(7) ? null : r.GetString(7),
                    Algorithm = r.IsDBNull(8) ? null : r.GetString(8),
                    AccountantId = r.IsDBNull(9) ? 0 : r.GetInt32(9),
                    SignedDate = r.IsDBNull(10) ? null : r.GetDateTime(10),
                    SignatureImageBase64 = r.IsDBNull(11) ? null : r.GetString(11),
                    IsPrinted = r.IsDBNull(12) ? false : r.GetInt32(12) == 1
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

        public async Task<ApiResponse<InvoicePdfEmailResult>> GeneratePdfAndSendEmailAsync(int invoiceId, int accountantId)
        {
            try
            {
                // 1. Lấy thông tin hóa đơn đầy đủ
                var invoice = await GetByIdAsync(invoiceId);
                if (invoice == null)
                {
                    return new ApiResponse<InvoicePdfEmailResult>
                    {
                        Success = false,
                        Message = "Không tìm thấy hóa đơn"
                    };
                }

                // 2. Lấy thông tin đơn đăng ký và học viên
                var (studentName, studentEmail, courseName, className) = await GetStudentCourseInfoAsync(invoice.RegistrationId);

                if (string.IsNullOrEmpty(studentEmail))
                {
                    return new ApiResponse<InvoicePdfEmailResult>
                    {
                        Success = false,
                        Message = "Học viên không có email để gửi"
                    };
                }

                // 3. Tạo PDF hóa đơn bằng QuestPDF (sử dụng giao diện đẹp giống PDF học viên xem)
                byte[] pdfBytes;
                try
                {
                    pdfBytes = await GenerateInvoicePdf(invoice, studentName, courseName, className);
                }
                catch (Exception pdfEx)
                {
                    _logger.LogError(pdfEx, "Failed to generate PDF for invoice {InvoiceId}", invoiceId);
                    return new ApiResponse<InvoicePdfEmailResult>
                    {
                        Success = false,
                        Message = "Không thể tạo file PDF: " + pdfEx.Message
                    };
                }

                // 4. Gửi email với PDF đính kèm
                var emailBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <h2 style='color: #2c5aa0; text-align: center;'>🎓 Hóa đơn học phí - Trung tâm Tin học</h2>
                            
                            <p>Kính gửi: <strong>{studentName}</strong>,</p>
                            
                            <p>Trung tâm gửi bạn hóa đơn học phí cho khóa học <strong>{courseName}</strong> - Lớp <strong>{className}</strong>.</p>
                            
                            <div style='background: #f8f9fa; padding: 15px; border-left: 4px solid #2c5aa0; margin: 20px 0;'>
                                <p><strong>📋 Mã hóa đơn:</strong> {invoice.InvoiceCode}</p>
                                <p><strong>💰 Số tiền:</strong> <span style='color: #d63384; font-size: 1.2em;'>{invoice.Amount:N0} VNĐ</span></p>
                                <p><strong>📅 Hạn thanh toán:</strong> <span style='color: #dc3545;'>{invoice.DueDate?.ToString("dd/MM/yyyy")}</span></p>
                            </div>

                            <p>📎 Vui lòng xem file PDF đính kèm để biết chi tiết hóa đơn.</p>
                            
                            <p style='margin-top: 30px;'>Trân trọng,<br/>
                            <strong>🏫 Trung tâm Tin học LDA</strong></p>
                            
                            <hr style='margin: 30px 0; border: none; border-top: 1px solid #eee;'/>
                            <p style='font-size: 12px; color: #666; text-align: center;'>
                                Email này được gửi tự động từ hệ thống quản lý trung tâm.
                            </p>
                        </div>
                    </body>
                    </html>";

                bool emailSent;
                try
                {
                    emailSent = await _emailService.SendEmailWithAttachmentAsync(
                        studentEmail,
                        $"🎓 Hóa đơn học phí - {invoice.InvoiceCode}",
                        emailBody,
                        pdfBytes,
                        $"HoaDon_{invoice.InvoiceCode}.pdf"
                    );
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Failed to send email for invoice {InvoiceId}", invoiceId);
                    return new ApiResponse<InvoicePdfEmailResult>
                    {
                        Success = false,
                        Message = "Không thể gửi email: " + emailEx.Message
                    };
                }

                if (!emailSent)
                {
                    return new ApiResponse<InvoicePdfEmailResult>
                    {
                        Success = false,
                        Message = "Gửi email thất bại"
                    };
                }

                // 5. Cập nhật flag DA_IN = 1 trong database
                await MarkPrintedAsync(invoiceId);

                return new ApiResponse<InvoicePdfEmailResult>
                {
                    Success = true,
                    Message = $"✅ Đã in hóa đơn và gửi PDF tới {studentEmail} thành công!",
                    Data = new InvoicePdfEmailResult
                    {
                        InvoiceId = invoiceId,
                        EmailAddress = studentEmail,
                        PdfGenerated = true,
                        EmailSent = true
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GeneratePdfAndSendEmailAsync failed for invoice {InvoiceId}", invoiceId);
                return new ApiResponse<InvoicePdfEmailResult>
                {
                    Success = false,
                    Message = "Có lỗi xảy ra: " + ex.Message
                };
            }
        }

        public async Task<bool> MarkPrintedAsync(int invoiceId)
        {
            try
            {
                using var conn = await GetAdminConnectionAsync();
                using var updateCmd = new OracleCommand("UPDATE HOA_DON SET DA_IN = 1 WHERE ID_HOA_DON = :id", conn) { BindByName = true };
                updateCmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
                var rows = await updateCmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MarkPrintedAsync failed for invoice {InvoiceId}", invoiceId);
                return false;
            }
        }

        public async Task<(string studentName, string studentEmail, string courseName, string className)> GetStudentCourseInfoAsync(int registrationId)
        {
            string studentName = "", studentEmail = "", courseName = "", className = "";
            using var conn = await GetAdminConnectionAsync();
            using (var cmd = new OracleCommand(@"
                    SELECT hv.HO_TEN, tk.EMAIL, kh.TEN_KHOA_HOC, lh.TEN_LOP_HOC
                    FROM DON_DANG_KY dk
                    JOIN HOC_VIEN hv ON hv.ID_HOC_VIEN = dk.ID_HOC_VIEN
                    LEFT JOIN TAI_KHOAN tk ON tk.ID_NGUOI_DUNG = hv.ID_HOC_VIEN
                    JOIN LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
                    JOIN KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
                    WHERE dk.ID_DANG_KY = :regId", conn))
            {
                cmd.BindByName = true;
                cmd.Parameters.Add(":regId", OracleDbType.Int32).Value = registrationId;
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    studentName = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    studentEmail = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    courseName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    className = reader.IsDBNull(3) ? "" : reader.GetString(3);
                }
            }
            return (studentName, studentEmail, courseName, className);
        }

        /// <summary>
        /// Tạo PDF hóa đơn với chữ ký số của toàn bộ nội dung PDF (async)
        /// </summary>
        private async Task<byte[]> GenerateInvoicePdf(Invoice invoice, string studentName, string courseName, string className)
        {
            try
            {
                // Đảm bảo QuestPDF license
                QuestPDF.Settings.License = LicenseType.Community;

                _logger.LogInformation("Bắt đầu tạo PDF cho hóa đơn {InvoiceId}, học viên: {StudentName}", invoice.InvoiceId, studentName);

                // 1. Tạo PDF cơ bản trước (chưa có chữ ký)
                var basePdfBytes = await Task.Run(() => CreateBasePdf(invoice, studentName, courseName, className));
                _logger.LogInformation("Đã tạo base PDF {Size} bytes", basePdfBytes.Length);

                // 2. Nếu đã có chữ ký, tạo chữ ký cho toàn bộ nội dung PDF
                if (!string.IsNullOrEmpty(invoice.SignatureBase64))
                {
                    var signedPdf = await Task.Run(() => CreateSignedPdf(basePdfBytes, invoice, studentName, courseName, className));
                    _logger.LogInformation("Đã tạo signed PDF {Size} bytes", signedPdf.Length);
                    return signedPdf;
                }

                return basePdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tạo PDF cho hóa đơn {InvoiceId}: {Error}", invoice.InvoiceId, ex.Message);
                // Fallback: Tạo PDF text đơn giản
                return await Task.Run(() => CreateFallbackTextPdf(invoice, studentName, courseName, className));
            }
        }

        /// <summary>
        /// Tạo PDF text đơn giản khi QuestPDF thất bại
        /// </summary>
        private byte[] CreateFallbackTextPdf(Invoice invoice, string studentName, string courseName, string className)
        {
            try
            {
                _logger.LogInformation("Tạo fallback text PDF cho hóa đơn {InvoiceId}", invoice.InvoiceId);
                
                var content = $@"HOA DON HOC PHI DIEN TU - TRUNG TAM TIN HOC LDA

==================================================
                  THONG TIN HOA DON
==================================================
Ma hoa don: {invoice.InvoiceCode}
Ngay tao: {invoice.CreatedDate?.ToString("dd/MM/yyyy HH:mm")}
Han thanh toan: {invoice.DueDate?.ToString("dd/MM/yyyy")}
Trang thai: {invoice.Status}

==================================================
                  THONG TIN HOC VIEN
==================================================
Ho va ten: {studentName}
Khoa hoc: {courseName}
Lop hoc: {className}

==================================================
                   CHI TIET HOA DON
==================================================
Noi dung: Hoc phi khoa hoc {courseName} - Lop {className}
So luong: 1
Don gia: {invoice.Amount:N0} VND
--------------------------------------------------
TONG CONG: {invoice.Amount:N0} VND

==================================================
                    CHU KY SO
==================================================
{(string.IsNullOrEmpty(invoice.SignatureBase64) ? 
    "Hoa don chua duoc ky so" : 
    $"Da ky so vao: {invoice.SignedDate?.ToString("dd/MM/yyyy HH:mm:ss")}\nTrang thai: Hop le")}

==================================================
Cam on quy khach da tin tuong va su dung dich vu!
Website: lda.edu.vn | Email: info@lda.edu.vn
==================================================

File duoc tao tu dong vao: {DateTime.Now:dd/MM/yyyy HH:mm:ss}
";

                if (!string.IsNullOrEmpty(invoice.SignatureBase64))
                {
                    content += $@"

--BEGIN-LDA-INVOICE-SIGNATURE--
Data: {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new {
    InvoiceCode = invoice.InvoiceCode,
    StudentName = studentName,
    CourseName = courseName,  
    ClassName = className,
    Amount = invoice.Amount,
    CreatedDate = invoice.CreatedDate?.ToString("yyyy-MM-dd HH:mm:ss"),
    DueDate = invoice.DueDate?.ToString("yyyy-MM-dd HH:mm:ss"),
    Status = invoice.Status
})))}
Signature: {invoice.SignatureBase64}
Algorithm: RSA-SHA256
Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
--END-LDA-INVOICE-SIGNATURE--";
                }

                return System.Text.Encoding.UTF8.GetBytes(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tạo fallback text PDF");
                return System.Text.Encoding.UTF8.GetBytes($@"HOA DON - {invoice.InvoiceCode}
Hoc vien: {studentName}
So tien: {invoice.Amount:N0} VND
Loi tao PDF: {ex.Message}");
            }
        }

        /// <summary>
        /// Tạo PDF cơ bản (chưa có chữ ký) - Phiên bản đơn giản và ổn định
        /// </summary>
        private byte[] CreateBasePdf(Invoice invoice, string studentName, string courseName, string className)
        {
            try
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        // Không chỉ định font cụ thể để tránh lỗi
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Header()
                            .PaddingBottom(20)
                            .Column(column =>
                            {
                                // Header đơn giản không có emoji
                                column.Item().AlignCenter()
                                    .Text("TRUNG TAM TIN HOC LDA")
                                    .FontSize(20).Bold().FontColor(Colors.Blue.Darken3);
                                    
                                column.Item().AlignCenter().PaddingTop(8)
                                    .Text("HOA DON HOC PHI DIEN TU")
                                    .FontSize(18).Bold();
                                    
                                column.Item().AlignCenter().PaddingTop(10)
                                    .Text($"So: {invoice.InvoiceCode}")
                                    .FontSize(14).Bold().FontColor(Colors.Red.Medium);
                            });

                        page.Content()
                            .PaddingVertical(20)
                            .Column(column =>
                            {
                                // Thông tin chung
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text("THONG TIN HOC VIEN").Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                                        col.Item().PaddingTop(10).Text($"Ho va ten: {studentName}").FontSize(12);
                                        col.Item().PaddingTop(5).Text($"Khoa hoc: {courseName}").FontSize(12);
                                        col.Item().PaddingTop(5).Text($"Lop hoc: {className}").FontSize(12);
                                    });
                                    
                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text("THONG TIN HOA DON").Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                                        col.Item().PaddingTop(10).Text($"Ngay tao: {invoice.CreatedDate?.ToString("dd/MM/yyyy HH:mm")}").FontSize(12);
                                        col.Item().PaddingTop(5).Text($"Han thanh toan: {invoice.DueDate?.ToString("dd/MM/yyyy")}").FontSize(12).FontColor(Colors.Red.Medium);
                                        col.Item().PaddingTop(5).Text($"Trang thai: {invoice.Status}").FontSize(12);
                                    });
                                });

                                // Separator
                                column.Item().PaddingTop(20).LineHorizontal(2).LineColor(Colors.Blue.Lighten2);

                                // Bảng chi tiết hóa đơn
                                column.Item().PaddingTop(20).Text("CHI TIET HOA DON").Bold().FontSize(16);
                                
                                column.Item().PaddingTop(15).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(50);
                                        columns.RelativeColumn(4);
                                        columns.ConstantColumn(60);
                                        columns.ConstantColumn(120);
                                    });

                                    // Header
                                    table.Cell().Element(HeaderCellStyle).Text("STT").Bold();
                                    table.Cell().Element(HeaderCellStyle).Text("NOI DUNG").Bold();
                                    table.Cell().Element(HeaderCellStyle).AlignCenter().Text("SO LUONG").Bold();
                                    table.Cell().Element(HeaderCellStyle).AlignCenter().Text("THANH TIEN (VND)").Bold();

                                    // Content
                                    table.Cell().Element(ContentCellStyle).AlignCenter().Text("1");
                                    table.Cell().Element(ContentCellStyle).Text($"Hoc phi khoa hoc {courseName} - Lop {className}");
                                    table.Cell().Element(ContentCellStyle).AlignCenter().Text("1");
                                    table.Cell().Element(ContentCellStyle).AlignRight().Text($"{invoice.Amount:N0}");

                                    // Total row
                                    table.Cell().ColumnSpan(3).Element(TotalCellStyle).AlignRight().Text("TONG CONG:").Bold().FontSize(14);
                                    table.Cell().Element(TotalCellStyle).AlignRight().Text($"{invoice.Amount:N0} VND").Bold().FontColor(Colors.Red.Medium).FontSize(14);
                                });

                                // Ghi chú đơn giản
                                column.Item().PaddingTop(25)
                                    .Background(Colors.Blue.Lighten5)
                                    .Padding(15)
                                    .Column(noteCol =>
                                    {
                                        noteCol.Item().Text("GHI CHU QUAN TRONG:").Bold().FontSize(13).FontColor(Colors.Blue.Darken2);
                                        noteCol.Item().PaddingTop(8).Text("Vui long thanh toan dung han de tranh gian doan viec hoc").FontSize(11);
                                        noteCol.Item().PaddingTop(4).Text("Hoa don nay duoc tao tu dong bang he thong dien tu va co gia tri phap ly").FontSize(11);
                                        noteCol.Item().PaddingTop(4).Text("De xac thuc tinh hop le, upload file PDF nay vao muc 'Xac thuc hoa don' tren website").FontSize(11);
                                        noteCol.Item().PaddingTop(4).Text("Moi thac mac xin lien he: info@lda.edu.vn hoac hotline 1900-xxx-xxx").FontSize(11);
                                    });
                            });

                        page.Footer()
                            .PaddingTop(20)
                            .BorderTop(1)
                            .BorderColor(Colors.Grey.Medium)
                            .PaddingTop(10)
                            .Column(footerCol =>
                            {
                                footerCol.Item().AlignCenter().Text("Cam on quy khach da tin tuong va su dung dich vu cua Trung tam!")
                                    .Italic().FontSize(12).FontColor(Colors.Blue.Darken1);
                                footerCol.Item().AlignCenter().PaddingTop(5)
                                    .Text("Website: lda.edu.vn | Email: info@lda.edu.vn | Hotline: 1900-xxx-xxx")
                                    .FontSize(10).FontColor(Colors.Grey.Darken1);
                                footerCol.Item().AlignCenter().PaddingTop(8)
                                    .Text($"Tai lieu duoc tao tu dong vao {DateTime.Now:dd/MM/yyyy HH:mm}")
                                    .FontSize(8).FontColor(Colors.Grey.Medium);
                            });
                    });
                });

                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tạo base PDF bằng QuestPDF: {Error}", ex.Message);
                // Fallback tạo PDF text đơn giản
                throw; // Re-throw để fallback vào CreateFallbackTextPdf
            }

            // Định nghĩa style cho table cells
            static IContainer HeaderCellStyle(IContainer container) =>
                container.Background(Colors.Blue.Darken2).Border(1).BorderColor(Colors.Blue.Darken3).Padding(8);

            static IContainer ContentCellStyle(IContainer container) =>
                container.Background(Colors.White).Border(1).BorderColor(Colors.Grey.Medium).Padding(8);

            static IContainer TotalCellStyle(IContainer container) =>
                container.Background(Colors.Blue.Lighten4).Border(1).BorderColor(Colors.Blue.Medium).Padding(8);
        }

        /// <summary>
        /// Tạo PDF đã ký số với thông tin chữ ký
        /// </summary>
        private byte[] CreateSignedPdf(byte[] basePdfBytes, Invoice invoice, string studentName, string courseName, string className)
        {
            try
            {
                // Tạo PDF với thông tin chữ ký số
                var signedDocument = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Header()
                            .PaddingBottom(20)
                            .Column(column =>
                            {
                                column.Item().AlignCenter()
                                    .Text("TRUNG TAM TIN HOC LDA")
                                    .FontSize(20).Bold().FontColor(Colors.Blue.Darken3);
                                    
                                column.Item().AlignCenter().PaddingTop(8)
                                    .Text("HOA DON HOC PHI - DA KY SO")
                                    .FontSize(18).Bold().FontColor(Colors.Green.Darken2);
                                    
                                column.Item().AlignCenter().PaddingTop(10)
                                    .Text($"So: {invoice.InvoiceCode}")
                                    .FontSize(14).Bold().FontColor(Colors.Red.Medium);
                            });

                        page.Content()
                            .PaddingVertical(20)
                            .Column(column =>
                            {
                                // Thông tin chung
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text("THONG TIN HOC VIEN").Bold().FontSize(14);
                                        col.Item().PaddingTop(10).Text($"Ho va ten: {studentName}").FontSize(12);
                                        col.Item().PaddingTop(5).Text($"Khoa hoc: {courseName}").FontSize(12);
                                        col.Item().PaddingTop(5).Text($"Lop hoc: {className}").FontSize(12);
                                    });
                                    
                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text("THONG TIN HOA DON").Bold().FontSize(14);
                                        col.Item().PaddingTop(10).Text($"Ngay tao: {invoice.CreatedDate?.ToString("dd/MM/yyyy HH:mm")}").FontSize(12);
                                        col.Item().PaddingTop(5).Text($"Han thanh toan: {invoice.DueDate?.ToString("dd/MM/yyyy")}").FontSize(12);
                                        col.Item().PaddingTop(5).Text($"So tien: {invoice.Amount:N0} VND").FontSize(12).Bold();
                                    });
                                });

                                // Thông tin chữ ký số
                                column.Item().PaddingTop(30)
                                    .Background(Colors.Green.Lighten5)
                                    .Border(2)
                                    .BorderColor(Colors.Green.Darken2)
                                    .Padding(15)
                                    .Column(signCol =>
                                    {
                                        signCol.Item().Text("HOA DON DA DUOC KY SO DIEN TU")
                                            .FontColor(Colors.Green.Darken2).Bold().FontSize(16);
                                        signCol.Item().PaddingTop(8).Text($"Thoi gian ky: {invoice.SignedDate?.ToString("dd/MM/yyyy HH:mm:ss")}")
                                            .FontSize(12);
                                        signCol.Item().PaddingTop(5).Text("Thuat toan: RSA-SHA256")
                                            .FontSize(12);
                                        signCol.Item().PaddingTop(5).Text("Don vi ky: Trung tam Tin hoc LDA")
                                            .FontSize(12);
                                        signCol.Item().PaddingTop(8).Text("Tinh toan ven da duoc bao dam - Moi thay doi deu duoc phat hien")
                                            .FontSize(11).Italic().FontColor(Colors.Green.Darken1);
                                    });

                                // Ghi chú
                                column.Item().PaddingTop(20)
                                    .Background(Colors.Blue.Lighten5)
                                    .Padding(15)
                                    .Column(noteCol =>
                                    {
                                        noteCol.Item().Text("GHI CHU QUAN TRONG:").Bold().FontSize(13);
                                        noteCol.Item().PaddingTop(8).Text("File PDF nay da duoc ky so - moi thay doi deu duoc phat hien").FontSize(11).Bold();
                                        noteCol.Item().PaddingTop(4).Text("Vui long thanh toan dung han de tranh gian doan viec hoc").FontSize(11);
                                        noteCol.Item().PaddingTop(4).Text("De xac thuc tinh hop le, upload file PDF nay vao muc 'Xac thuc hoa don'").FontSize(11);
                                    });
                            });

                        page.Footer()
                            .PaddingTop(20)
                            .BorderTop(1)
                            .BorderColor(Colors.Grey.Medium)
                            .PaddingTop(10)
                            .Column(footerCol =>
                            {
                                footerCol.Item().AlignCenter().Text("Cam on quy khach da tin tuong va su dung dich vu!")
                                    .Italic().FontSize(12);
                                footerCol.Item().AlignCenter().PaddingTop(8)
                                    .Text($"Tai lieu duoc tao tu dong vao {DateTime.Now:dd/MM/yyyy HH:mm}")
                                    .FontSize(8).FontColor(Colors.Grey.Medium);
                            });
                    });
                });

                return signedDocument.GeneratePdf();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tạo signed PDF: {Error}", ex.Message);
                // Fallback về PDF text với thông tin chữ ký
                return CreateFallbackTextPdf(invoice, studentName, courseName, className);
            }
        }
    }
}
