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
    }

    public class DigitalSignatureService : BaseService, IDigitalSignatureService
    {
        public DigitalSignatureService(IConfiguration configuration, ILogger<DigitalSignatureService> logger, IOracleConnectionProvider userConnProvider)
            : base(configuration, logger, userConnProvider) { }

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

                // Lấy public key của kế toán
                var publicKeyPem = await GetAccountantPublicKeyAsync(conn, accountantId);
                if (string.IsNullOrEmpty(publicKeyPem))
                {
                    return new SignInvoiceResult
                    {
                        Success = false,
                        Message = "Kế toán chưa có public key"
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
                
                // Lấy thông tin hóa đơn đã ký
                var invoice = await GetSignedInvoiceByIdAsync(conn, invoiceId);
                if (invoice == null)
                {
                    return new VerifySignatureResult
                    {
                        Success = false,
                        Message = "Không tìm thấy hóa đơn"
                    };
                }

                if (string.IsNullOrEmpty(invoice.SignatureBase64))
                {
                    return new VerifySignatureResult
                    {
                        Success = false,
                        Message = "Hóa đơn chưa được ký số"
                    };
                }

                // Lấy public key của kế toán đã ký
                var publicKeyPem = await GetAccountantPublicKeyAsync(conn, invoice.AccountantId);
                if (string.IsNullOrEmpty(publicKeyPem))
                {
                    return new VerifySignatureResult
                    {
                        Success = false,
                        Message = "Không tìm thấy public key của kế toán"
                    };
                }

                // Tạo lại dữ liệu gốc để xác thực
                var invoiceData = CreateInvoiceDataForSigning(invoice);
                var dataBytes = Encoding.UTF8.GetBytes(invoiceData);

                // Xác thực chữ ký
                using var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(Convert.FromBase64String(
                    publicKeyPem.Replace("-----BEGIN RSA PUBLIC KEY-----", "")
                               .Replace("-----END RSA PUBLIC KEY-----", "")
                               .Replace("\n", "").Replace("\r", "")
                ), out _);

                var signature = Convert.FromBase64String(invoice.SignatureBase64);
                var isValid = rsa.VerifyData(dataBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                return new VerifySignatureResult
                {
                    Success = true,
                    IsValidSignature = isValid,
                    Message = isValid ? "Chữ ký hợp lệ" : "Chữ ký không hợp lệ",
                    SignedBy = await GetAccountantNameAsync(conn, invoice.AccountantId),
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
                    Message = "Lỗi xác thực chữ ký: " + ex.Message
                };
            }
        }

        public async Task<string> ExportPublicKeyAsync(int accountantId)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                return await GetAccountantPublicKeyAsync(conn, accountantId) ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export public key for accountant {AccountantId}", accountantId);
                return "";
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
}