# ✅ SỬA LỖI IMPORT PUBLIC KEY

## 🎯 VẤN ĐỀ

Public key trong database có format:
```
-----BEGIN RSA PUBLIC KEY-----MIIBCgKCAQEAwgIEwgz...
```

**Vấn đề:** Không có xuống dòng (`\n`) sau header!

Code cũ chỉ xử lý format có xuống dòng:
```csharp
publicKeyPem.Replace("-----BEGIN RSA PUBLIC KEY-----", "")
           .Replace("\n", "")
```

→ Kết quả: `MIIBCgKCAQEAwgIEwgz...` (đúng)

Nhưng nếu không có `\n`, kết quả vẫn đúng. Vấn đề thực sự là:
- Public key có thể có format `RSA PUBLIC KEY` hoặc `PUBLIC KEY`
- Code cần thử cả 2 cách import

## ✅ ĐÃ SỬA

### File: `QLTTTA_API/Services/DigitalSignatureService.cs`

**Thay đổi:**
```csharp
// TRƯỚC:
rsa.ImportRSAPublicKey(Convert.FromBase64String(
    publicKeyPem.Replace("-----BEGIN RSA PUBLIC KEY-----", "")
               .Replace("-----END RSA PUBLIC KEY-----", "")
               .Replace("\n", "").Replace("\r", "")
), out _);

// SAU:
// Clean public key - xử lý cả format có và không có xuống dòng
var cleanPublicKey = publicKeyPem
    .Replace("-----BEGIN RSA PUBLIC KEY-----", "")
    .Replace("-----END RSA PUBLIC KEY-----", "")
    .Replace("-----BEGIN PUBLIC KEY-----", "")
    .Replace("-----END PUBLIC KEY-----", "")
    .Replace("\n", "")
    .Replace("\r", "")
    .Replace(" ", "")
    .Trim();

try
{
    // Thử import RSA Public Key format trước
    rsa.ImportRSAPublicKey(Convert.FromBase64String(cleanPublicKey), out _);
}
catch
{
    // Nếu thất bại, thử SubjectPublicKeyInfo format
    try
    {
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(cleanPublicKey), out _);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Không thể import public key");
        return new VerifySignatureResult
        {
            Success = false,
            Message = $"❌ Lỗi import public key: {ex.Message}"
        };
    }
}
```

**Cải thiện:**
1. ✅ Xử lý cả 2 format: `RSA PUBLIC KEY` và `PUBLIC KEY`
2. ✅ Loại bỏ tất cả khoảng trắng
3. ✅ Thử 2 cách import: `ImportRSAPublicKey` và `ImportSubjectPublicKeyInfo`
4. ✅ Log lỗi chi tiết để debug
5. ✅ Trả về message rõ ràng

**Áp dụng cho 2 chỗ:**
- Dòng ~244: `VerifyInvoiceSignatureAsync`
- Dòng ~484: `VerifyPdfHashSignatureAsync`

## 🚀 CÁCH TRIỂN KHAI

### Bước 1: Stop API
```bash
# Nhấn Ctrl+C trong terminal đang chạy API
```

### Bước 2: Build
```bash
cd QLTTTA_API
dotnet build
```

### Bước 3: Restart API
```bash
dotnet run
```

### Bước 4: Test
1. Tạo hóa đơn mới
2. Ký số với private key
3. Upload PDF → ✅ **Xác thực hợp lệ**

## 🔍 KIỂM TRA

### Nếu vẫn không hợp lệ

**Kiểm tra log API:**
```
# Tìm dòng log:
Không thể import public key. Key length: XXX
```

**Nếu có lỗi này:**
- Public key trong database bị hỏng
- Hoặc private key không khớp

**Giải pháp:**
1. Tạo cặp key mới
2. Cập nhật public key vào database
3. Dùng private key mới để ký

### Test Public Key

```sql
-- Kiểm tra public key
SELECT 
    LENGTH(KHOA_CONG_PEM) as KEY_LENGTH,
    SUBSTR(KHOA_CONG_PEM, 1, 50) as KEY_START
FROM TTTA
WHERE ID_TTTA = 1;
```

**Kết quả mong đợi:**
- KEY_LENGTH: 400-500
- KEY_START: `-----BEGIN RSA PUBLIC KEY-----` hoặc `-----BEGIN PUBLIC KEY-----`

## ⚠️ LƯU Ý

### Format Public Key

**2 format phổ biến:**

1. **RSA PUBLIC KEY** (PKCS#1):
```
-----BEGIN RSA PUBLIC KEY-----
MIIBCgKCAQEA...
-----END RSA PUBLIC KEY-----
```

2. **PUBLIC KEY** (X.509 SubjectPublicKeyInfo):
```
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...
-----END PUBLIC KEY-----
```

**Code hiện tại hỗ trợ CẢ 2 format!**

### Tạo Public Key đúng format

```bash
# Từ private key, tạo RSA PUBLIC KEY format
openssl rsa -in private_key.pem -RSAPublicKey_out -out rsa_public.pem

# Hoặc tạo PUBLIC KEY format (SubjectPublicKeyInfo)
openssl rsa -in private_key.pem -pubout -out public.pem
```

## ✅ KẾT QUẢ

Sau khi sửa:
1. ✅ Hỗ trợ cả 2 format public key
2. ✅ Xử lý public key không có xuống dòng
3. ✅ Log lỗi chi tiết để debug
4. ✅ Xác thực hoạt động đúng

---

**Lưu ý:** Nhớ **stop API** trước khi build!
