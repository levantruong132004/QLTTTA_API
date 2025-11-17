using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;
using Path = System.IO.Path;

namespace QLTTTA_API.Services
{
    public interface IPdfSignatureService
    {
        // Legacy v3 custom embedding (will be deprecated)
        Task<byte[]> CreateCryptographicallySignedPdfAsync(int invoiceId, string invoiceData, string signatureBase64, string studentName, string courseName, string className);
        Task<VerifySignatureResult> VerifyPdfIntegrityAsync(byte[] pdfBytes);
        Task<string> GetPublicKeyFingerprintAsync();
        // New PAdES compliant signing (temporarily disabled)
        Task<byte[]> SignPdfAsync(int invoiceId, string invoiceDataJson, string privateKeyPem, string? signerCertPem, IEnumerable<string>? chainCertPems);
        Task<VerifySignatureResult> VerifyPadesSignatureAsync(byte[] pdfBytes);
        Task<string> GenerateAndSaveBaseInvoicePdfAsync(int invoiceId, string invoiceCode, string studentName, string courseName, string className, int amount, DateTime createdDate, DateTime dueDate);
    }

    public class PdfSignatureService : BaseService, IPdfSignatureService
    {
        private readonly IDigitalSignatureService _digitalSignatureService;
        private readonly string _storageRoot;
        private readonly IWebHostEnvironment? _env;

        public PdfSignatureService(
            IConfiguration configuration, 
            ILogger<PdfSignatureService> logger, 
            IOracleConnectionProvider userConnProvider,
            IDigitalSignatureService digitalSignatureService,
            IWebHostEnvironment? env = null) // optional env để lấy ContentRoot
            : base(configuration, logger, userConnProvider)
        {
            _digitalSignatureService = digitalSignatureService;
            _env = env;
            _storageRoot = configuration["PdfStorage:RootPath"] ?? Path.Combine(env?.ContentRootPath ?? Directory.GetCurrentDirectory(), "InvoicesPdf");
            try { Directory.CreateDirectory(_storageRoot); } catch { }
            // Gỡ bỏ đăng ký font tùy chỉnh do FontManager không khả dụng trong phiên bản QuestPDF đang dùng.
        }

        // Style helpers cho bảng (sửa bỏ FontColor khỏi IContainer để tránh lỗi generic)
        private static IContainer CellHeader(IContainer c) => c.Background(Colors.Blue.Darken2).Padding(5).Border(1).BorderColor(Colors.Blue.Darken3).DefaultTextStyle(x => x.FontSize(11).Bold());
        private static IContainer CellBody(IContainer c) => c.Background(Colors.White).Padding(5).Border(1).BorderColor(Colors.Grey.Lighten2);
        private static IContainer CellTotal(IContainer c) => c.Background(Colors.Blue.Lighten4).Padding(5).Border(1).BorderColor(Colors.Blue.Darken2).DefaultTextStyle(x => x.FontSize(11).Bold());

        /// <summary>
        /// Tạo PDF với chữ ký số nhúng trực tiếp vào cấu trúc PDF - Version 3.0 Enhanced
        /// </summary>
        public async Task<byte[]> CreateCryptographicallySignedPdfAsync(int invoiceId, string invoiceData, string signatureBase64, string studentName, string courseName, string className)
        {
            try
            {
                // Parse invoice data để lấy thông tin chi tiết
                var invoiceInfo = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(invoiceData);
                var invoiceCode = invoiceInfo?.TryGetValue("invoiceCode", out var codeElem) == true ? codeElem.GetString() : $"HD_{invoiceId}";
                var amount = invoiceInfo?.TryGetValue("amount", out var amtElem) == true ? amtElem.GetInt32() : 0;
                var createdDateStr = invoiceInfo?.TryGetValue("createdDate", out var cdElem) == true ? cdElem.GetString() : null;
                var dueDateStr = invoiceInfo?.TryGetValue("dueDate", out var ddElem) == true ? ddElem.GetString() : null;
                
                DateTime createdDate = DateTime.TryParse(createdDateStr, out var cd) ? cd : DateTime.Now;
                DateTime dueDate = DateTime.TryParse(dueDateStr, out var dd) ? dd : DateTime.Now.AddDays(7);

                // Kiểm tra xem đã có base PDF chưa (đã tạo khi tạo hóa đơn)
                var basePath = Path.Combine(_storageRoot, $"Invoice_{invoiceId}_base.pdf");
                
                if (!File.Exists(basePath))
                {
                    // Nếu chưa có, tạo mới
                    _logger.LogInformation("Chưa có base PDF, tạo mới cho hóa đơn {InvoiceId} - {InvoiceCode}", invoiceId, invoiceCode);
                    basePath = await GenerateAndSaveBaseInvoicePdfAsync(
                        invoiceId, 
                        invoiceCode ?? $"HD_{invoiceId}", 
                        studentName, 
                        courseName, 
                        className, 
                        amount, 
                        createdDate, 
                        dueDate
                    );
                    
                    if (string.IsNullOrEmpty(basePath) || !File.Exists(basePath))
                        throw new InvalidOperationException("Không thể tạo base PDF để ký");
                }
                else
                {
                    // Dùng lại base PDF đã có (đảm bảo hash không đổi)
                    _logger.LogInformation("✅ Sử dụng lại base PDF đã có cho hóa đơn {InvoiceId} tại {Path}", invoiceId, basePath);
                }

                var basePdfBytes = await File.ReadAllBytesAsync(basePath);
                using var sha256 = SHA256.Create();
                var pdfHash = sha256.ComputeHash(basePdfBytes);
                var pdfHashHex = Convert.ToHexString(pdfHash);

                var signatureMetadata = new
                {
                    InvoiceId = invoiceId,
                    InvoiceCode = invoiceCode,
                    StudentName = studentName,
                    CourseName = courseName,
                    ClassName = className,
                    Amount = amount,
                    CreatedDate = createdDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    DueDate = dueDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    OriginalPdfHash = pdfHashHex,
                    SignatureAlgorithm = "RSA-SHA256-PDF-EMBEDDED",
                    SignatureVersion = "3.0",
                    Issuer = "Trung tâm Tiếng Anh LDA",
                    SignedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    PublicKeyFingerprint = await GetPublicKeyFingerprintAsync()
                };
                var signatureJson = JsonSerializer.Serialize(signatureMetadata, new JsonSerializerOptions { WriteIndented = true });
                var metadataBytes = Encoding.UTF8.GetBytes(signatureJson);

                var signatureBlock = $"\n%%LDA-SIGNATURE-BOUNDARY-START%%\n\n" +
                                     "%%LDA-PDF-HASH-SIGNATURE-START%%\n" +
                                     "--BEGIN-LDA-INVOICE-HASH--\n" +
                                     $"Data: {Convert.ToBase64String(metadataBytes)}\n" +
                                     $"Signature: {signatureBase64}\n" +
                                     $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                     "Algorithm: RSA-SHA256-PDF-EMBEDDED\n" +
                                     "HashMethod: SHA256\n" +
                                     "Issuer: Trung tâm Tiếng Anh LDA\n" +
                                     "Version: 3.0\n" +
                                     $"OriginalHash: {pdfHashHex}\n" +
                                     "--END-LDA-INVOICE-HASH--\n" +
                                     "%%LDA-PDF-HASH-SIGNATURE-END%%\n";

                var finalPdfBytes = Combine(basePdfBytes, Encoding.UTF8.GetBytes(signatureBlock));
                _logger.LogInformation("📄 Đã tạo final PDF bytes, size = {Size} bytes", finalPdfBytes.Length);
                
                // Lưu signed PDF với tên file có InvoiceCode
                var signedFileName = $"Invoice_{invoiceId}_{invoiceCode}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_signed.pdf";
                var signedPath = Path.Combine(_storageRoot, signedFileName);
                await File.WriteAllBytesAsync(signedPath, finalPdfBytes);
                _logger.LogInformation("💾 Đã lưu signed PDF tại: {Path}", signedPath);
                
                _logger.LogInformation("🔄 Bắt đầu lưu metadata vào Oracle cho InvoiceId={InvoiceId}...", invoiceId);
                var hash = await SaveSignedPdfMetadataAsync(invoiceId, finalPdfBytes, "EMBEDDED_V3");
                _logger.LogInformation("✅ Đã ký PDF styled cho hóa đơn {InvoiceId} - {InvoiceCode} - Hash {Hash}", invoiceId, invoiceCode, hash);
                return finalPdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi ký PDF styled cho hóa đơn {InvoiceId}", invoiceId);
                throw;
            }
        }

        public async Task<string> GenerateAndSaveBaseInvoicePdfAsync(int invoiceId, string invoiceCode, string studentName, string courseName, string className, int amount, DateTime createdDate, DateTime dueDate)
        {
            try
            {
                QuestPDF.Settings.License = LicenseType.Community;
                var filePath = Path.Combine(_storageRoot, $"Invoice_{invoiceId}_base.pdf");

                var bytes = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor("#ffffff");
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Header().Background("#5c3a1e").Padding(12).Column(col =>
                        {
                            col.Item().Text($"Số: {invoiceCode}").FontColor("#fdfdfd").FontSize(10);
                            col.Item().AlignCenter().Text("TRUNG TÂM TIẾNG ANH LDA").FontColor("#fdfdfd").FontSize(18).Bold();
                            col.Item().AlignCenter().Text("Mã số thuế: 0123456789 | Địa chỉ: Đại Học Công Thương, TPHCM\nĐiện thoại: (028) 1234.5678 | Email: TTT@lda.edu.vn")
                                .FontColor("#fdfdfd").FontSize(9);
                        });

                        page.Content().PaddingTop(15).Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Border(1).BorderColor("#e0e0e0").Background("#f9fafe").Padding(10).Column(box =>
                                {
                                    box.Item().Text("Thông tin hóa đơn").Bold().FontSize(12).FontColor("#222");
                                    box.Item().PaddingTop(6).Text($"Mã hóa đơn: {invoiceCode}").Bold();
                                    box.Item().Text($"Ngày tạo: {createdDate:dd/MM/yyyy}");
                                    box.Item().Text($"Hạn thanh toán: {dueDate:dd/MM/yyyy}");
                                    box.Item().Text("Trạng thái: Chưa thanh toán").FontColor("#4b2e13").Bold();
                                });
                            });

                            col.Item().PaddingTop(18).Border(1).BorderColor("#5c3a1e").Background("#5c3a1e").Padding(12).Column(pay =>
                            {
                                pay.Item().AlignCenter().Text("THÔNG TIN THANH TOÁN").FontColor("#ffffff").Bold();
                                pay.Item().PaddingTop(8).Background("#6a4524").Padding(10).Row(r =>
                                {
                                    r.RelativeItem().Column(c =>
                                    {
                                        c.Item().Text("Ngân hàng:").FontColor("#f0e6dd");
                                        c.Item().Text("Số tài khoản:").FontColor("#f0e6dd");
                                        c.Item().Text("Chủ tài khoản:").FontColor("#f0e6dd");
                                        c.Item().Text("Số tiền cần chuyển:").FontColor("#f0e6dd");
                                        c.Item().Text("Nội dung CK:").FontColor("#f0e6dd");
                                    });
                                    r.RelativeItem().Column(c =>
                                    {
                                        c.Item().Text("HDBank").FontColor("#ffffff").Bold();
                                        c.Item().Text("215704070010285").FontColor("#ffffff").Bold();
                                        c.Item().Text("TRUNG TÂM TIẾNG ANH LDA").FontColor("#ffffff").Bold();
                                        c.Item().Text($"{amount:N0} VND").FontColor("#ffc107").Bold();
                                        c.Item().Text(invoiceCode).FontColor("#ffffff").Bold();
                                    });
                                    r.ConstantItem(70).Border(1).BorderColor("#8b5a2b").AlignCenter().Padding(6).Text("QR").FontColor("#ffffff").Bold();
                                });
                            });

                            col.Item().PaddingTop(25).Row(r =>
                            {
                                r.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Người nhận").Bold().FontSize(11);
                                    c.Item().Text("(Ký, ghi rõ họ, tên)").FontSize(8).Italic();
                                });
                                r.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Người ký").Bold().FontSize(11);
                                    c.Item().Text("(Ký, ghi rõ họ, tên)").FontSize(8).Italic();
                                });
                            });

                            col.Item().PaddingTop(40).AlignCenter().Text("TRUNG TÂM TIẾNG ANH LDA").Bold().FontSize(13);
                            col.Item().AlignCenter().Text($"Ký ngày: {createdDate:dd/MM/yyyy}").FontSize(9);
                            col.Item().PaddingTop(6).AlignCenter().Text("Hóa đơn này đã được ký số và xác thực bởi Trung tâm Tiếng Anh LDA").FontSize(7).Italic().FontColor("#6a4524");
                        });
                    });
                }).GeneratePdf();

                await File.WriteAllBytesAsync(filePath, bytes);
                _logger.LogInformation("Đã tạo base PDF (styled) cho hóa đơn {InvoiceId} tại {Path}", invoiceId, filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tạo base PDF styled cho hóa đơn {InvoiceId}", invoiceId);
                return string.Empty;
            }
        }

        private static byte[] Combine(byte[] a, byte[] b)
        {
            var r = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, r, 0, a.Length);
            Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
            return r;
        }

        /// <summary>
        /// Xác thực tính toàn vẹn của PDF có chữ ký số nhúng
        /// </summary>
        public async Task<VerifySignatureResult> VerifyPdfIntegrityAsync(byte[] pdfBytes)
        {
            try
            {
                var content = Encoding.UTF8.GetString(pdfBytes);
                
                // Tìm kiếm PDF hash signature (Version 3.0)
                var hashDataTag = "--BEGIN-LDA-INVOICE-HASH--";
                var hashEndTag = "--END-LDA-INVOICE-HASH--";
                
                var hashStartIdx = content.LastIndexOf(hashDataTag);
                var hashEndIdx = content.LastIndexOf(hashEndTag);
                
                if (hashStartIdx < 0 || hashEndIdx < 0 || hashEndIdx <= hashStartIdx)
                {
                    return new VerifySignatureResult 
                    { 
                        Success = false, 
                        Message = "❌ Không tìm thấy chữ ký số version 3.0 trong PDF" 
                    };
                }

                // Parse signature data
                var embedded = content.Substring(hashStartIdx + hashDataTag.Length, hashEndIdx - (hashStartIdx + hashDataTag.Length));
                var signatureInfo = ParseEmbeddedSignature(embedded);

                if (signatureInfo == null)
                {
                    return new VerifySignatureResult 
                    { 
                        Success = false, 
                        Message = "❌ Không thể phân tích dữ liệu chữ ký" 
                    };
                }

                // Lấy phần PDF thuần (trước signature boundary)
                var signatureStart = content.IndexOf("%%LDA-SIGNATURE-BOUNDARY-START%%");
                if (signatureStart < 0)
                {
                    return new VerifySignatureResult 
                    { 
                        Success = false, 
                        Message = "❌ Không tìm thấy signature boundary trong PDF" 
                    };
                }

                var purePdfContent = content.Substring(0, signatureStart);
                var purePdfBytes = Encoding.UTF8.GetBytes(purePdfContent);

                // Tính hash của PDF hiện tại
                using var sha256 = SHA256.Create();
                var currentPdfHash = sha256.ComputeHash(purePdfBytes);
                var currentPdfHashHex = Convert.ToHexString(currentPdfHash);

                // So sánh hash
                var expectedHash = signatureInfo.GetValueOrDefault("OriginalHash")?.ToString() ?? "";
                var isPdfIntegrityValid = string.Equals(currentPdfHashHex, expectedHash, StringComparison.OrdinalIgnoreCase);

                // Xác thực chữ ký RSA
                var publicKeyPem = await GetCenterPublicKeyAsync();
                if (string.IsNullOrEmpty(publicKeyPem))
                {
                    return new VerifySignatureResult 
                    { 
                        Success = false, 
                        Message = "❌ Không tìm thấy public key để xác thực" 
                    };
                }

                var dataString = signatureInfo.GetValueOrDefault("Data")?.ToString() ?? "";
                var signatureString = signatureInfo.GetValueOrDefault("Signature")?.ToString() ?? "";

                if (string.IsNullOrEmpty(dataString) || string.IsNullOrEmpty(signatureString))
                {
                    return new VerifySignatureResult 
                    { 
                        Success = false, 
                        Message = "❌ Dữ liệu chữ ký không đầy đủ" 
                    };
                }

                var dataBytes = Convert.FromBase64String(dataString);
                var signature = Convert.FromBase64String(signatureString);

                using var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(Convert.FromBase64String(
                    publicKeyPem.Replace("-----BEGIN RSA PUBLIC KEY-----", "")
                               .Replace("-----END RSA PUBLIC KEY-----", "")
                               .Replace("\n", "").Replace("\r", "")
                ), out _);
                
                var isSignatureValid = rsa.VerifyData(dataBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                // Kết quả tổng hợp
                var isOverallValid = isPdfIntegrityValid && isSignatureValid;

                var timestamp = signatureInfo.GetValueOrDefault("Timestamp")?.ToString() ?? "";
                var issuer = signatureInfo.GetValueOrDefault("Issuer")?.ToString() ?? "Trung tâm Tin học LDA";

                // Parse invoice data để hiển thị
                var invoiceDataJson = Encoding.UTF8.GetString(dataBytes);
                var displayInfo = CreateDisplayInfo(invoiceDataJson);

                var resultMessage = isOverallValid 
                    ? $"✅ PDF CÓ CHỮ KÝ SỐ VERSION 3.0 - HOÀN TOÀN HỢP LỆ\n\n{displayInfo}\n" +
                      $"🛡️ CHI TIẾT XÁC THỰC:\n" +
                      $"🔐 Chữ ký RSA: ✅ HỢP LỆ\n" +
                      $"📄 Tính toàn vẹn PDF: ✅ KHÔNG BỊ THAY ĐỔI\n" +
                      $"🕐 Thời gian ký: {timestamp}\n" +
                      $"🔒 Thuật toán: RSA-SHA256-PDF-EMBEDDED\n" +
                      $"🏛️ Đơn vị phát hành: {issuer}\n" +
                      $"📋 Version: 3.0 Enhanced\n" +
                      $"✓ File PDF này là tài liệu chính thức, an toàn và chưa bị chỉnh sửa"
                    : $"❌ PDF KHÔNG HỢP LỆ - CẢNH BÁO BẢO MẬT VERSION 3.0\n\n" +
                      $"⚠️ Chi tiết vấn đề:\n" +
                      $"🔐 Chữ ký RSA: {(isSignatureValid ? "✅ Hợp lệ" : "❌ Không hợp lệ")}\n" +
                      $"📄 Tính toàn vẹn PDF: {(isPdfIntegrityValid ? "✅ Không đổi" : "❌ Đã thay đổi")}\n" +
                      $"Expected Hash: {expectedHash[..16]}...\n" +
                      $"Current Hash:  {currentPdfHashHex[..16]}...\n" +
                      $"❗ KHÔNG SỬ DỤNG FILE NÀY CHO CÁC GIAO DỊCH CHÍNH THỨC";

                return new VerifySignatureResult 
                { 
                    Success = true, 
                    IsValidSignature = isOverallValid,
                    Message = resultMessage,
                    SignedBy = issuer,
                    SignedDate = DateTime.TryParse(timestamp, out var parsedTime) ? parsedTime : null,
                    InvoiceData = invoiceDataJson
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xác thực PDF integrity");
                return new VerifySignatureResult 
                { 
                    Success = false, 
                    Message = $"❌ Lỗi xác thực PDF: {ex.Message}" 
                };
            }
        }

        /// <summary>
        /// Parse embedded signature từ PDF content
        /// </summary>
        private Dictionary<string, object>? ParseEmbeddedSignature(string embedded)
        {
            try
            {
                var result = new Dictionary<string, object>();
                var lines = embedded.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    
                    var colonIndex = trimmed.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        var key = trimmed.Substring(0, colonIndex).Trim();
                        var value = trimmed.Substring(colonIndex + 1).Trim();
                        result[key] = value;
                    }
                }
                
                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tạo thông tin hiển thị từ invoice data
        /// </summary>
        private string CreateDisplayInfo(string invoiceDataJson)
        {
            try
            {
                var invoiceInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(invoiceDataJson);
                if (invoiceInfo == null) return "Không thể đọc dữ liệu hóa đơn";

                var displayInfo = "📄 THÔNG TIN HÓA ĐƠN:\n";
                foreach (var kvp in invoiceInfo)
                {
                    var displayName = kvp.Key switch
                    {
                        "InvoiceCode" => "📋 Mã hóa đơn",
                        "StudentName" => "👤 Học viên", 
                        "CourseName" => "📚 Khóa học",
                        "ClassName" => "🏫 Lớp học",
                        "Amount" => "💰 Số tiền",
                        "CreatedDate" => "📅 Ngày tạo",
                        "SignedAt" => "🕐 Thời gian ký",
                        "SignatureVersion" => "📋 Version",
                        _ => $"• {kvp.Key}"
                    };
                    
                    var value = kvp.Value?.ToString() ?? "N/A";
                    if (kvp.Key == "Amount" && int.TryParse(value, out var amount))
                        value = $"{amount:N0} VNĐ";
                        
                    displayInfo += $"{displayName}: {value}\n";
                }
                return displayInfo;
            }
            catch
            {
                return $"📄 Dữ liệu hóa đơn gốc:\n{invoiceDataJson}\n";
            }
        }

        /// <summary>
        /// Lấy fingerprint của public key
        /// </summary>
        public async Task<string> GetPublicKeyFingerprintAsync()
        {
            try
            {
                var publicKeyPem = await GetCenterPublicKeyAsync();
                if (string.IsNullOrEmpty(publicKeyPem)) return "UNKNOWN";
                
                var cleanKey = publicKeyPem.Replace("-----BEGIN RSA PUBLIC KEY-----", "")
                                          .Replace("-----END RSA PUBLIC KEY-----", "")
                                          .Replace("\n", "").Replace("\r", "");
                
                using var sha256 = SHA256.Create();
                var keyBytes = Convert.FromBase64String(cleanKey);
                var hashBytes = sha256.ComputeHash(keyBytes);
                return Convert.ToHexString(hashBytes)[..16];
            }
            catch
            {
                return "UNKNOWN";
            }
        }

        /// <summary>
        /// Lấy center public key
        /// </summary>
        private async Task<string> GetCenterPublicKeyAsync()
        {
            try
            {
                using var conn = await GetAdminConnectionAsync();
                using var cmd = new OracleCommand("SELECT KHOA_CONG_PEM FROM QLTT_ADMIN.TTTA WHERE ID_TTTA = 1", conn);
                var result = await cmd.ExecuteScalarAsync();
                return result?.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Helper methods để extract thông tin từ invoice data
        /// </summary>
        private string GetInvoiceCodeFromData(string invoiceData)
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(invoiceData);
                return data?.GetValueOrDefault("InvoiceCode")?.ToString() ?? "";
            }
            catch { return ""; }
        }

        private int GetAmountFromData(string invoiceData)
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(invoiceData);
                var amountStr = data?.GetValueOrDefault("Amount")?.ToString() ?? "0";
                return int.TryParse(amountStr, out var amount) ? amount : 0;
            }
            catch { return 0; }
        }

        private async Task<string> SaveSignedPdfMetadataAsync(int invoiceId, byte[] pdfBytes, string signatureVersion)
        {
            Directory.CreateDirectory(_storageRoot);
            var fileName = $"Invoice_{invoiceId}_{DateTime.UtcNow:yyyyMMddHHmmss}_signed.pdf";
            var absolutePath = Path.Combine(_storageRoot, fileName);
            await File.WriteAllBytesAsync(absolutePath, pdfBytes);
            var hash = Convert.ToHexString(SHA256.HashData(pdfBytes));
            
            _logger.LogInformation("🔐 Bắt đầu lưu metadata: InvoiceId={InvoiceId}, Hash={Hash}, Version={Version}, FileName={FileName}", 
                invoiceId, hash[..16] + "...", signatureVersion, fileName);

            try
            {
                // Tạo connection mới trực tiếp từ connection string (không qua session)
                // Dùng _connectionString từ BaseService (đã được load từ OracleDbConnection)
                _logger.LogInformation("🔑 Sử dụng connection string từ BaseService");
                
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                _logger.LogInformation("✅ Đã kết nối Oracle thành công, bắt đầu transaction...");
                
                // Set schema và bắt đầu transaction
                using var setSchemaCmd = new OracleCommand("ALTER SESSION SET CURRENT_SCHEMA = QLTT_ADMIN", conn);
                await setSchemaCmd.ExecuteNonQueryAsync();
                _logger.LogInformation("📊 Đã set CURRENT_SCHEMA = QLTT_ADMIN");
                
                using var transaction = conn.BeginTransaction();
                try
                {
                    var sql = @"UPDATE HOA_DON 
                                 SET SIGNED_PDF_HASH = :hash,
                                     SIGNED_PDF_VERSION = :version,
                                     SIGNED_PDF_PATH = :path
                               WHERE ID_HOA_DON = :id";
                    using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                    cmd.Transaction = transaction;
                    cmd.Parameters.Add(":hash", OracleDbType.Varchar2).Value = hash;
                    cmd.Parameters.Add(":version", OracleDbType.Varchar2).Value = signatureVersion;
                    cmd.Parameters.Add(":path", OracleDbType.Varchar2).Value = fileName;
                    cmd.Parameters.Add(":id", OracleDbType.Int32).Value = invoiceId;
                    
                    _logger.LogInformation("🔄 Thực thi UPDATE với InvoiceId={InvoiceId}...", invoiceId);
                    var rowsAffected = await cmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("📊 Rows affected: {RowsAffected}", rowsAffected);
                    
                    if (rowsAffected == 0)
                    {
                        _logger.LogError("❌ Không tìm thấy hóa đơn {InvoiceId} để cập nhật hash PDF", invoiceId);
                        transaction.Rollback();
                        throw new InvalidOperationException($"Không tìm thấy hóa đơn {invoiceId}");
                    }
                    
                    transaction.Commit();
                    _logger.LogInformation("✅ COMMIT thành công! Đã lưu metadata PDF: InvoiceId={InvoiceId}, Hash={Hash}, Version={Version}, Path={Path}", 
                        invoiceId, hash, signatureVersion, fileName);
                }
                catch (Exception txEx)
                {
                    _logger.LogError(txEx, "❌ Lỗi trong transaction, ROLLBACK: {Message}", txEx.Message);
                    transaction.Rollback();
                    throw;
                }
            }
            catch (OracleException oex)
            {
                _logger.LogError(oex, "❌ LỖI ORACLE: Code={Code}, Message={Message}", oex.Number, oex.Message);
                throw new InvalidOperationException($"Lỗi Oracle khi lưu metadata PDF (Code {oex.Number}): {oex.Message}", oex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ LỖI: Không thể cập nhật hash PDF cho hóa đơn {InvoiceId}. Chi tiết: {Message}", invoiceId, ex.Message);
                throw new InvalidOperationException($"Không thể lưu metadata PDF: {ex.Message}", ex);
            }

            return hash;
        }

        public Task<byte[]> SignPdfAsync(int invoiceId, string invoiceDataJson, string privateKeyPem, string? signerCertPem, IEnumerable<string>? chainCertPems)
            => throw new NotSupportedException("Chữ ký PAdES tạm thời chưa khả dụng trong bản build này");

        public Task<VerifySignatureResult> VerifyPadesSignatureAsync(byte[] pdfBytes)
            => throw new NotSupportedException("Xác thực chữ ký PAdES tạm thời chưa khả dụng trong bản build này");
    }
}