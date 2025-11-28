using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;

namespace QLTTTA_API.Services
{
    public interface IDigitalSignatureService
    {
        Task<RSAKeyPairResult> GenerateKeyPairAsync();
        Task<SignInvoiceResult> SignInvoiceAsync(int invoiceId, string privateKeyPath, int accountantId);
        Task<VerifySignatureResult> VerifyInvoiceSignatureAsync(int invoiceId);
        Task<string> ExportPublicKeyAsync(int accountantId);
        Task<RSAKeyPairResult> GenerateKeyPairAndSaveAsync(int accountantId);
        Task<RSAKeyPairResult> GenerateCenterKeyPairAndSaveAsync(int accountantId, string centerName, string address, string phone);
        Task<SignInvoiceResult> SignInvoiceWithPrivateKeyContentAsync(int invoiceId, string privateKeyPem, int accountantId);
        Task<VerifySignatureResult> VerifyPdfSignatureAsync(byte[] pdfBytes);
        Task<VerifySignatureResult> VerifyPdfIntegrityByHashAsync(int invoiceId, byte[] pdfBytes);
    }

    public class DigitalSignatureService : BaseService, IDigitalSignatureService
    {
        private readonly IEmailService _emailService;
        private readonly IPdfSignatureService _pdfSignatureService;

        public DigitalSignatureService(IConfiguration configuration, ILogger<DigitalSignatureService> logger, IOracleConnectionProvider userConnProvider, IEmailService emailService, IPdfSignatureService pdfSignatureService)
            : base(configuration, logger, userConnProvider)
        {
            _emailService = emailService;
            _pdfSignatureService = pdfSignatureService;
        }

        public async Task<RSAKeyPairResult> GenerateKeyPairAsync()
        {
            try
            {
                using var rsa = RSA.Create(2048);
                
                // Export public key to PEM format
                var publicKeyPem = rsa.ExportRSAPublicKeyPem();
                
                // Export private key to PEM format
                var privateKeyPem = rsa.ExportRSAPrivateKeyPem();

                return new RSAKeyPairResult
                {
                    Success = true,
                    PublicKeyPem = publicKeyPem,
                    PrivateKeyPem = privateKeyPem,
                    Message = "Tạo cặp khóa RSA thành công"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate RSA key pair");
                return new RSAKeyPairResult
                {
                    Success = false,
                    Message = "Lỗi tạo cặp khóa RSA"
                };
            }
        }

        public async Task<RSAKeyPairResult> GenerateKeyPairAndSaveAsync(int accountantId)
        {
            if (accountantId <= 0)
                return new RSAKeyPairResult { Success = false, Message = "accountantId không hợp lệ" };
            try
            {
                using var rsa = RSA.Create(2048);
                var publicKeyPem = rsa.ExportRSAPublicKeyPem();
                var privateKeyPem = rsa.ExportRSAPrivateKeyPem();

                using var conn = await GetConnectionAsync();
                var ok = await UpdateAccountantPublicKeyAsync(conn, accountantId, publicKeyPem);
                if (!ok)
                {
                    return new RSAKeyPairResult { Success = false, Message = "Không cập nhật được public key cho kế toán" };
                }

                return new RSAKeyPairResult { Success = true, PublicKeyPem = publicKeyPem, PrivateKeyPem = privateKeyPem, Message = "Đã tạo và lưu public key cho kế toán" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateKeyPairAndSaveAsync error");
                return new RSAKeyPairResult { Success = false, Message = "Lỗi tạo/lưu public key: " + ex.Message };
            }
        }

        private async Task<bool> UpdateAccountantPublicKeyAsync(OracleConnection conn, int accountantId, string publicKeyPem)
        {
            var sql = "UPDATE KE_TOAN SET KHOA_CONG_PEM = :pem WHERE ID_KE_TOAN = :id";
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":pem", OracleDbType.Clob).Value = publicKeyPem;
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = accountantId;
            var affected = await cmd.ExecuteNonQueryAsync();
            return affected > 0;
        }

        public async Task<SignInvoiceResult> SignInvoiceAsync(int invoiceId, string privateKeyPath, int accountantId)
        {
            try
            {
                // Đọc private key từ file
                if (!File.Exists(privateKeyPath))
                {
                    return new SignInvoiceResult
                    {
                        Success = false,
                        Message = "Không tìm thấy file private key"
                    };
                }

                var privateKeyPem = await File.ReadAllTextAsync(privateKeyPath);
                
                using var conn = await GetConnectionAsync();
                
                // Lấy thông tin hóa đơn
                var invoice = await GetInvoiceByIdAsync(conn, invoiceId);
                if (invoice == null)
                {
                    return new SignInvoiceResult
                    {
                        Success = false,
                        Message = "Không tìm thấy hóa đơn"
                    };
                }

                // Kiểm tra hóa đơn đã được ký chưa
                if (!string.IsNullOrEmpty(invoice.SignatureBase64))
                {
                    return new SignInvoiceResult
                    {
                        Success = false,
                        Message = "Hóa đơn đã được ký số"
                    };
                }

                // Tạo dữ liệu để ký (hash của thông tin hóa đơn)
                var invoiceData = CreateInvoiceDataForSigning(invoice);
                var dataBytes = Encoding.UTF8.GetBytes(invoiceData);

                // Ký dữ liệu
                using var rsa = RSA.Create();
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(
                    privateKeyPem.Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                               .Replace("-----END RSA PRIVATE KEY-----", "")
                               .Replace("\n", "").Replace("\r", "")
                ), out _);

                var signature = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var signatureBase64 = Convert.ToBase64String(signature);

                // Lấy public key của trung tâm (TTTA) thay vì kế toán cá nhân
                var publicKeyPem = await GetCenterPublicKeyAdminAsync();
                if (string.IsNullOrEmpty(publicKeyPem))
                {
                    return new SignInvoiceResult
                    {
                        Success = false,
                        Message = "Chưa thiết lập public key trung tâm"
                    };
                }

                // Cập nhật hóa đơn với chữ ký
                await UpdateInvoiceSignatureAsync(conn, invoiceId, signatureBase64, accountantId);

                return new SignInvoiceResult
                {
                    Success = true,
                    Message = "Ký hóa đơn thành công",
                    SignatureBase64 = signatureBase64,
                    InvoiceData = invoiceData
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sign invoice {InvoiceId}", invoiceId);
                return new SignInvoiceResult
                {
                    Success = false,
                    Message = "Lỗi ký hóa đơn: " + ex.Message
                };
            }
        }

        public async Task<VerifySignatureResult> VerifyInvoiceSignatureAsync(int invoiceId)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                
                // Lấy thông tin hóa đơn với chữ ký
                var invoice = await GetSignedInvoiceByIdAsync(conn, invoiceId);
                if (invoice == null)
                {
                    return new VerifySignatureResult
                    {
                        Success = false,
                        IsValidSignature = false,
                        Message = "Không tìm thấy hóa đơn"
                    };
                }

                // Kiểm tra hóa đơn đã được ký chưa
                if (string.IsNullOrEmpty(invoice.SignatureBase64))
                {
                    return new VerifySignatureResult
                    {
                        Success = false,
                        IsValidSignature = false,
                        Message = "Hóa đơn chưa được ký số"
                    };
                }

                // Lấy public key và thông tin từ bảng TTTA (Trung tâm)
                var centerInfo = await GetCenterInfoAsync(conn);
                if (centerInfo == null || string.IsNullOrEmpty(centerInfo.PublicKeyPem))
                {
                    return new VerifySignatureResult
                    {
                        Success = false,
                        IsValidSignature = false,
                        Message = "Chưa thiết lập public key trung tâm"
                    };
                }

                // Tạo lại dữ liệu gốc để xác thực
                var invoiceData = CreateInvoiceDataForSigning(invoice);
                var dataBytes = Encoding.UTF8.GetBytes(invoiceData);

                // Xác thực chữ ký bằng public key từ bảng TTTA
                using var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(Convert.FromBase64String(
                    centerInfo.PublicKeyPem.Replace("-----BEGIN RSA PUBLIC KEY-----", "")
                               .Replace("-----END RSA PUBLIC KEY-----", "")
                               .Replace("\n", "").Replace("\r", "")
                ), out _);

                var signature = Convert.FromBase64String(invoice.SignatureBase64);
                var isValid = rsa.VerifyData(dataBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                return new VerifySignatureResult
                {
                    Success = true,
                    IsValidSignature = isValid,
                    Message = isValid ? "Xác thực hợp lệ" : "Chữ ký không hợp lệ",
                    SignedBy = centerInfo.CenterName, // Tên trung tâm từ TTTA
                    SignedDate = invoice.SignedDate,
                    InvoiceData = invoiceData
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify invoice signature {InvoiceId}", invoiceId);
                return new VerifySignatureResult
                {
                    Success = false,
                    IsValidSignature = false,
                    Message = "Lỗi xác thực chữ ký: " + ex.Message
                };
            }
        }

        private async Task<CenterInfoDto?> GetCenterInfoAsync(OracleConnection conn)
        {
            var sql = @"SELECT TEN_TRUNG_TAM, DIA_CHI, SO_DIEN_THOAI, KHOA_CONG_PEM
                        FROM QLTT_ADMIN.TTTA 
                        WHERE ID_TTTA = 1";
            using var cmd = new OracleCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                return new CenterInfoDto
                {
                    CenterName = reader.IsDBNull(0) ? null : reader.GetString(0),
                    Address = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Phone = reader.IsDBNull(2) ? null : reader.GetString(2),
                    PublicKeyPem = reader.IsDBNull(3) ? null : reader.GetString(3)
                };
            }
            return null;
        }

        public async Task<string> ExportPublicKeyAsync(int accountantId)
        {
            try
            {
                // Public key is now stored in TTTA (Center), not per accountant
                return await GetCenterPublicKeyAdminAsync() ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export public key for accountant {AccountantId}", accountantId);
                return "";
            }
        }

        public async Task<RSAKeyPairResult> GenerateCenterKeyPairAndSaveAsync(int accountantId, string centerName, string address, string phone)
        {
            if (accountantId <= 0) return new RSAKeyPairResult { Success = false, Message = "accountantId không hợp lệ" };
            if (string.IsNullOrWhiteSpace(centerName)) return new RSAKeyPairResult { Success = false, Message = "Thiếu tên trung tâm" };
            try
            {
                using var conn = await GetAdminConnectionAsync(); // dùng kết nối admin để đảm bảo quyền/scheme
                await EnsureTttaRowAsync(conn);

                var existing = await GetCenterPublicKeyAsync(conn);
                if (!string.IsNullOrEmpty(existing))
                {
                    return new RSAKeyPairResult { Success = false, Message = "Public key trung tâm đã được thiết lập và không thể thay đổi" };
                }

                using var rsa = RSA.Create(2048);
                var publicKeyPem = rsa.ExportRSAPublicKeyPem();
                var privateKeyPem = rsa.ExportRSAPrivateKeyPem();

                // Save center info + public key
                await UpdateCenterInfoAndPublicKeyAsync(conn, centerName, address, phone, publicKeyPem);

                // Email private key to accountant (lấy email qua kết nối admin để tránh thiếu quyền)
                var email = await GetAccountantEmailAsync(conn, accountantId);
                if (!string.IsNullOrWhiteSpace(email))
                {
                    var att = new List<(string fileName, byte[] content, string contentType)>
                    {
                        ($"private_key_{DateTime.UtcNow:yyyyMMddHHmmss}.pem", Encoding.UTF8.GetBytes(privateKeyPem), "application/x-pem-file")
                    };
                    await _emailService.SendAsync(email, "Private Key Trung Tâm - LDA", "<p>Đây là private key của trung tâm. Vui lòng lưu trữ an toàn và không chia sẻ.</p>", att);
                }

                return new RSAKeyPairResult { Success = true, PublicKeyPem = publicKeyPem, PrivateKeyPem = privateKeyPem, Message = "Đã tạo và lưu public key trung tâm. Private key đã gửi email cho kế toán." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateCenterKeyPairAndSaveAsync error");
                return new RSAKeyPairResult { Success = false, Message = "Lỗi tạo/lưu public key trung tâm: " + ex.Message };
            }
        }

        public async Task<SignInvoiceResult> SignInvoiceWithPrivateKeyContentAsync(int invoiceId, string privateKeyPem, int accountantId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(privateKeyPem))
                    return new SignInvoiceResult { Success = false, Message = "File private key không hợp lệ" };

                using var conn = await GetConnectionAsync();
                var invoice = await GetInvoiceByIdAsync(conn, invoiceId);
                if (invoice == null) return new SignInvoiceResult { Success = false, Message = "Không tìm thấy hóa đơn" };
                if (!string.IsNullOrEmpty(invoice.SignatureBase64))
                    return new SignInvoiceResult { Success = false, Message = "Hóa đơn đã được ký số" };

                // Build data and sign
                var invoiceData = CreateInvoiceDataForSigning(invoice);
                var dataBytes = Encoding.UTF8.GetBytes(invoiceData);
                using var rsa = RSA.Create();
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(
                    privateKeyPem.Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                               .Replace("-----END RSA PRIVATE KEY-----", "")
                               .Replace("\n", "").Replace("\r", "")
                ), out _);
                var signature = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var signatureBase64 = Convert.ToBase64String(signature);

                // Ensure Center public key exists (đọc qua admin để tránh thiếu quyền)
                var centerPub = await GetCenterPublicKeyAdminAsync();
                if (string.IsNullOrEmpty(centerPub))
                    return new SignInvoiceResult { Success = false, Message = "Chưa thiết lập public key trung tâm" };

                // Update invoice signature
                await UpdateInvoiceSignatureAsync(conn, invoiceId, signatureBase64, accountantId);

                // Generate PDF and send to student
                var studentEmail = await GetStudentEmailByInvoiceAsync(conn, invoiceId);
                
                // Fetch details for PDF
                var details = await GetInvoicePdfDetailsAsync(conn, invoiceId);
                
                // Use PdfSignatureService to create signed PDF
                var pdfBytes = await _pdfSignatureService.CreateCryptographicallySignedPdfAsync(
                    invoiceId, 
                    invoiceData, 
                    signatureBase64, 
                    details.StudentName, 
                    details.CourseName, 
                    details.ClassName
                );

                if (!string.IsNullOrWhiteSpace(studentEmail))
                {
                    await _emailService.SendAsync(studentEmail, "Hóa đơn đã ký số - Trung tâm LDA",
                        "<p>Đính kèm là hóa đơn đã ký số của bạn.</p>", new[] { ($"hoa_don_{invoice.InvoiceCode}.pdf", pdfBytes, "application/pdf") });
                }

                return new SignInvoiceResult { Success = true, Message = "Ký hóa đơn thành công và đã gửi email cho học viên", SignatureBase64 = signatureBase64, InvoiceData = invoiceData };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignInvoiceWithPrivateKeyContentAsync error for {InvoiceId}", invoiceId);
                return new SignInvoiceResult { Success = false, Message = "Lỗi ký hóa đơn: " + ex.Message };
            }
        }

        public async Task<VerifySignatureResult> VerifyPdfSignatureAsync(byte[] pdfBytes)
        {
            try
            {
                var publicKeyPem = await GetCenterPublicKeyAdminAsync();
                if (string.IsNullOrEmpty(publicKeyPem))
                    return new VerifySignatureResult { Success = false, Message = "Chưa thiết lập public key trung tâm" };

                // Extract embedded data and signature from PDF text content
                var content = Encoding.UTF8.GetString(pdfBytes);
                var dataTag = "--BEGIN-LDA-INVOICE--";
                var endTag = "--END-LDA-INVOICE--";
                var startIdx = content.IndexOf(dataTag);
                var endIdx = content.IndexOf(endTag);
                if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx)
                    return new VerifySignatureResult { Success = false, Message = "Không tìm thấy dữ liệu chữ ký trong PDF" };
                var embedded = content.Substring(startIdx + dataTag.Length, endIdx - (startIdx + dataTag.Length));
                // Parse simple lines
                string? base64Data = null; string? base64Sig = null;
                foreach (var line in embedded.Split('\n'))
                {
                    var t = line.Trim();
                    if (t.StartsWith("Data:", StringComparison.OrdinalIgnoreCase)) base64Data = t.Substring(5).Trim();
                    if (t.StartsWith("Signature:", StringComparison.OrdinalIgnoreCase)) base64Sig = t.Substring(10).Trim();
                }
                if (string.IsNullOrEmpty(base64Data) || string.IsNullOrEmpty(base64Sig))
                    return new VerifySignatureResult { Success = false, Message = "Thiếu dữ liệu/ chữ ký trong PDF" };

                var dataBytes = Convert.FromBase64String(base64Data);
                var signature = Convert.FromBase64String(base64Sig);

                using var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(Convert.FromBase64String(
                    publicKeyPem.Replace("-----BEGIN RSA PUBLIC KEY-----", "")
                               .Replace("-----END RSA PUBLIC KEY-----", "")
                               .Replace("\n", "").Replace("\r", "")
                ), out _);
                var ok = rsa.VerifyData(dataBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                return new VerifySignatureResult { Success = true, IsValidSignature = ok, Message = ok ? "Chữ ký hợp lệ" : "Chữ ký không hợp lệ", InvoiceData = Encoding.UTF8.GetString(dataBytes) };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyPdfSignatureAsync error");
                return new VerifySignatureResult { Success = false, Message = "Lỗi xác thực chữ ký: " + ex.Message };
            }
        }

        public async Task<VerifySignatureResult> VerifyPdfIntegrityByHashAsync(int invoiceId, byte[] pdfBytes)
        {
            try
            {
                using var conn = await GetAdminConnectionAsync();
                var sql = @"SELECT SIGNED_PDF_HASH, SIGNED_PDF_VERSION, SIGNED_PDF_PATH, NGAY_KY
                              FROM HOA_DON WHERE ID_HOA_DON = :id";
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return new VerifySignatureResult { Success = false, Message = "Không tìm thấy hóa đơn" };
                }

                var storedHash = reader.IsDBNull(0) ? null : reader.GetString(0);
                if (string.IsNullOrEmpty(storedHash))
                {
                    return new VerifySignatureResult { Success = false, Message = "Hóa đơn chưa có mã băm đã lưu" };
                }

                var version = reader.IsDBNull(1) ? "UNKNOWN" : reader.GetString(1);
                var path = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var signedDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);

                var uploadedHash = Convert.ToHexString(SHA256.HashData(pdfBytes));
                var match = string.Equals(uploadedHash, storedHash, StringComparison.OrdinalIgnoreCase);

                var payload = JsonSerializer.Serialize(new
                {
                    storedHash,
                    uploadedHash,
                    version,
                    filePath = path
                });

                return new VerifySignatureResult
                {
                    Success = true,
                    IsValidSignature = match,
                    Message = match ? "✅ File PDF trùng khớp mã băm đã lưu" : "❌ Mã băm của file không trùng với dữ liệu đã lưu",
                    SignedDate = signedDate,
                    SignedBy = version,
                    InvoiceData = payload
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyPdfIntegrityByHashAsync error for {InvoiceId}", invoiceId);
                return new VerifySignatureResult { Success = false, Message = $"Lỗi kiểm tra mã băm: {ex.Message}" };
            }
        }

        private async Task<Invoice?> GetInvoiceByIdAsync(OracleConnection conn, int invoiceId)
        {
            var sql = @"SELECT ID_HOA_DON, MA_HOA_DON, NGAY_TAO, NGAY_HET_HAN, SO_TIEN, TRANG_THAI, ID_DANG_KY, CHU_KY_BASE64
                        FROM HOA_DON WHERE ID_HOA_DON = :id";
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
            using var reader = await cmd.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                return new Invoice
                {
                    InvoiceId = reader.GetInt32(0),
                    InvoiceCode = reader.GetString(1),
                    CreatedDate = reader.GetDateTime(2),
                    DueDate = reader.GetDateTime(3),
                    Amount = Convert.ToInt32(reader.GetValue(4)),
                    Status = reader.GetString(5),
                    RegistrationId = reader.GetInt32(6),
                    SignatureBase64 = reader.IsDBNull(7) ? null : reader.GetString(7)
                };
            }
            return null;
        }

        private async Task<Invoice?> GetSignedInvoiceByIdAsync(OracleConnection conn, int invoiceId)
        {
            var sql = @"SELECT ID_HOA_DON, MA_HOA_DON, NGAY_TAO, NGAY_HET_HAN, SO_TIEN, TRANG_THAI, ID_DANG_KY, 
                              CHU_KY_BASE64, THUAT_TOAN, ID_KE_TOAN_KY, NGAY_KY
                        FROM HOA_DON WHERE ID_HOA_DON = :id";
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
            using var reader = await cmd.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                return new Invoice
                {
                    InvoiceId = reader.GetInt32(0),
                    InvoiceCode = reader.GetString(1),
                    CreatedDate = reader.GetDateTime(2),
                    DueDate = reader.GetDateTime(3),
                    Amount = Convert.ToInt32(reader.GetValue(4)),
                    Status = reader.GetString(5),
                    RegistrationId = reader.GetInt32(6),
                    SignatureBase64 = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Algorithm = reader.IsDBNull(8) ? null : reader.GetString(8),
                    AccountantId = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                    SignedDate = reader.IsDBNull(10) ? null : reader.GetDateTime(10)
                };
            }
            return null;
        }

        private async Task<string?> GetAccountantPublicKeyAsync(OracleConnection conn, int accountantId)
        {
            // Read from a restricted view to avoid exposing full KE_TOAN to all roles
            var sql = @"SELECT KHOA_CONG_PEM FROM KE_TOAN_PUB WHERE ID_KE_TOAN = :id";
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = accountantId;
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }

        private async Task<string?> GetAccountantNameAsync(OracleConnection conn, int accountantId)
        {
            // Read from a restricted view to avoid exposing full KE_TOAN to all roles
            var sql = @"SELECT HO_TEN FROM KE_TOAN_PUB WHERE ID_KE_TOAN = :id";
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = accountantId;
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }

        private async Task UpdateInvoiceSignatureAsync(OracleConnection conn, int invoiceId, string signatureBase64, int accountantId)
        {
            var sql = @"UPDATE HOA_DON SET CHU_KY_BASE64 = :sig, THUAT_TOAN = :alg, ID_KE_TOAN_KY = :accId, NGAY_KY = SYSDATE 
                        WHERE ID_HOA_DON = :id";
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":sig", OracleDbType.Varchar2).Value = signatureBase64;
            cmd.Parameters.Add(":alg", OracleDbType.Varchar2).Value = "RSA-SHA256";
            cmd.Parameters.Add(":accId", OracleDbType.Int32).Value = accountantId;
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
            await cmd.ExecuteNonQueryAsync();
        }

        private string CreateInvoiceDataForSigning(Invoice invoice)
        {
            // Tạo chuỗi dữ liệu chuẩn hóa để ký
            var data = new
            {
                InvoiceCode = invoice.InvoiceCode,
                CreatedDate = invoice.CreatedDate?.ToString("yyyy-MM-dd HH:mm:ss"),
                DueDate = invoice.DueDate?.ToString("yyyy-MM-dd HH:mm:ss"),
                Amount = invoice.Amount,
                RegistrationId = invoice.RegistrationId
            };
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        private async Task EnsureTttaRowAsync(OracleConnection conn)
        {
            var countSql = "SELECT COUNT(*) FROM QLTT_ADMIN.TTTA";
            using var cmdCount = new OracleCommand(countSql, conn);
            var cnt = Convert.ToInt32(await cmdCount.ExecuteScalarAsync());
            if (cnt == 0)
            {
                using var cmdIns = new OracleCommand("INSERT INTO QLTT_ADMIN.TTTA (ID_TTTA, TEN_TRUNG_TAM, DIA_CHI, SO_DIEN_THOAI, KHOA_CONG_PEM, CREATED_AT, UPDATED_AT) VALUES (1, N'Trung tâm LDA', N'Địa chỉ...', '0000000000', NULL, SYSDATE, SYSDATE)", conn);
                await cmdIns.ExecuteNonQueryAsync();
            }
        }

        private async Task<string?> GetCenterPublicKeyAsync(OracleConnection conn)
        {
            var sql = "SELECT KHOA_CONG_PEM FROM QLTT_ADMIN.TTTA WHERE ID_TTTA = 1";
            using var cmd = new OracleCommand(sql, conn);
            var obj = await cmd.ExecuteScalarAsync();
            return obj?.ToString();
        }

        private async Task UpdateCenterPublicKeyAsync(OracleConnection conn, string publicKeyPem)
        {
            var sql = "UPDATE QLTT_ADMIN.TTTA SET KHOA_CONG_PEM = :pem, UPDATED_AT = SYSDATE WHERE ID_TTTA = 1";
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":pem", OracleDbType.Clob).Value = publicKeyPem;
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateCenterInfoAndPublicKeyAsync(OracleConnection conn, string centerName, string address, string phone, string publicKeyPem)
        {
            var sql = @"UPDATE QLTT_ADMIN.TTTA 
                        SET TEN_TRUNG_TAM = :name, DIA_CHI = :addr, SO_DIEN_THOAI = :phone, KHOA_CONG_PEM = :pem, UPDATED_AT = SYSDATE 
                      WHERE ID_TTTA = 1";
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":name", OracleDbType.NVarchar2).Value = centerName;
            cmd.Parameters.Add(":addr", OracleDbType.NVarchar2).Value = (object?)address ?? DBNull.Value;
            cmd.Parameters.Add(":phone", OracleDbType.Varchar2).Value = (object?)phone ?? DBNull.Value;
            cmd.Parameters.Add(":pem", OracleDbType.Clob).Value = publicKeyPem;
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<string?> GetCenterPublicKeyAdminAsync()
        {
            using var conn = await GetAdminConnectionAsync();
            return await GetCenterPublicKeyAsync(conn);
        }

        private async Task<string?> GetAccountantEmailAsync(OracleConnection conn, int accountantId)
        {
            var sql = @"SELECT tk.EMAIL FROM TAI_KHOAN tk WHERE tk.ID_NGUOI_DUNG = :id";
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = accountantId;
            var obj = await cmd.ExecuteScalarAsync();
            return obj?.ToString();
        }

        private async Task<string?> GetStudentEmailByInvoiceAsync(OracleConnection conn, int invoiceId)
        {
            var sql = @"SELECT tk.EMAIL
                         FROM HOA_DON hd
                         JOIN DON_DANG_KY dk ON dk.ID_DANG_KY = hd.ID_DANG_KY
                         JOIN HOC_VIEN hv ON hv.ID_HOC_VIEN = dk.ID_HOC_VIEN
                         JOIN TAI_KHOAN tk ON tk.ID_NGUOI_DUNG = hv.ID_HOC_VIEN
                        WHERE hd.ID_HOA_DON = :id";
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
            var obj = await cmd.ExecuteScalarAsync();
            return obj?.ToString();
        }

        private async Task<(string StudentName, string CourseName, string ClassName)> GetInvoicePdfDetailsAsync(OracleConnection conn, int invoiceId)
        {
            var sql = @"SELECT hv.HO_TEN, kh.TEN_KHOA_HOC, lh.TEN_LOP_HOC
                        FROM HOA_DON hd
                        JOIN DON_DANG_KY dk ON dk.ID_DANG_KY = hd.ID_DANG_KY
                        JOIN HOC_VIEN hv ON hv.ID_HOC_VIEN = dk.ID_HOC_VIEN
                        JOIN LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
                        JOIN KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
                        WHERE hd.ID_HOA_DON = :id";
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (
                    reader.IsDBNull(0) ? "" : reader.GetString(0),
                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2)
                );
            }
            return ("", "", "");
        }

        private async Task<byte[]> GenerateInvoicePdfAsync(OracleConnection conn, int invoiceId, string invoiceData, string signatureBase64)
        {
            // Minimal PDF bytes with embedded markers for data/signature (not a cryptographically signed PDF)
            var header = "%PDF-1.4\n";
            var bodyText = $"HÓA ĐƠN ĐIỆN TỬ LDA\nInvoiceId: {invoiceId}\n\n--BEGIN-LDA-INVOICE--\nData: {Convert.ToBase64String(Encoding.UTF8.GetBytes(invoiceData))}\nSignature: {signatureBase64}\n--END-LDA-INVOICE--\n";
            var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            await writer.WriteAsync(header);
            // This is not a full PDF spec; but most viewers accept simple text streams in content for demo
            await writer.WriteAsync(bodyText);
            await writer.FlushAsync();
            return stream.ToArray();
        }
    }

    // DTOs for results
    public class RSAKeyPairResult
    {
        public bool Success { get; set; }
        public string? PublicKeyPem { get; set; }
        public string? PrivateKeyPem { get; set; }
        public string? Message { get; set; }
    }

    public class SignInvoiceResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? SignatureBase64 { get; set; }
        public string? InvoiceData { get; set; }
    }

    public class VerifySignatureResult
    {
        public bool Success { get; set; }
        public bool IsValidSignature { get; set; }
        public string? Message { get; set; }
        public string? SignedBy { get; set; }
        public DateTime? SignedDate { get; set; }
        public string? InvoiceData { get; set; }
    }

    public class SignatureInfoDto
    {
        public string? SignatureBase64 { get; set; }
        public string? PublicKeyPem { get; set; }
        public string? SignerName { get; set; }
        public DateTime? SignedDate { get; set; }
        public string? Algorithm { get; set; }
    }

    public class CenterInfoDto
    {
        public string? CenterName { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? PublicKeyPem { get; set; }
    }
}