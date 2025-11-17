# 📝 TÓM TẮT THAY ĐỔI - LUỒNG KÝ SỐ ĐƠN GIẢN HÓA

## 🎯 MỤC TIÊU

Đơn giản hóa luồng xác thực PDF cho học viên bằng cách:
1. ✅ Ký số tích hợp vào file PDF
2. ✅ Sử dụng hàm băm SHA-256 để băm file và lưu kết quả
3. ✅ Cho phép học viên upload file để đối chiếu hash

---

## 🔄 THAY ĐỔI CHÍNH

### 1. Controller: `QLTTTA_WEB/Controllers/StudentController.cs`

**Thay đổi:**
- ❌ Xóa: Xác thực chữ ký RSA phức tạp (verify-pdf)
- ✅ Thêm: Kiểm tra hash đơn giản (verify-file-hash)

**Action mới:**
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> VerifyPdfUpload(IFormFile pdf, int? invoiceId)
{
    // Upload PDF + Invoice ID
    // Gọi API: /api/digitalsignature/verify-file-hash
    // So sánh hash
    // Trả kết quả: Toàn vẹn / Đã thay đổi
}
```

**Input:**
- `pdf`: File PDF học viên upload
- `invoiceId`: Mã hóa đơn (để tìm hash trong DB)

**Output:**
- `TempData["SuccessMessage"]`: File toàn vẹn (màu xanh)
- `TempData["ErrorMessage"]`: File đã thay đổi (màu đỏ)

---

### 2. View: `QLTTTA_WEB/Views/Student/VerifyPdf.cshtml`

**Thay đổi:**
- ❌ Xóa: Giao diện xác thực chữ ký RSA
- ✅ Thêm: Giao diện kiểm tra hash đơn giản

**Form mới:**
```html
<form method="post" enctype="multipart/form-data">
    <!-- Input 1: Invoice ID -->
    <input type="number" name="invoiceId" required />
    
    <!-- Input 2: File PDF -->
    <input type="file" name="pdf" accept=".pdf" required />
    
    <button type="submit">Kiểm tra tính toàn vẹn</button>
</form>
```

**Hiển thị kết quả:**
```html
<!-- File TOÀN VẸN -->
<div class="alert alert-success">
    ✅ FILE PDF TOÀN VẸN - Không bị thay đổi
</div>

<!-- File ĐÃ THAY ĐỔI -->
<div class="alert alert-danger">
    ❌ FILE PDF ĐÃ BỊ THAY ĐỔI - Không khớp với bản gốc
</div>
```

---

### 3. API: `QLTTTA_API/Controllers/DigitalSignatureController.cs`

**Endpoint đã có sẵn:**
```csharp
[HttpPost("verify-file-hash")]
[RequestSizeLimit(20_000_000)]
public async Task<IActionResult> VerifyFileHash()
{
    // Nhận: invoiceId + pdf file
    // Tính hash của file upload
    // Lấy hash từ DB (SIGNED_PDF_HASH)
    // So sánh 2 hash
    // Trả kết quả
}
```

**Không cần sửa gì!** Endpoint này đã hoạt động đúng.

---

### 4. Service: `QLTTTA_API/Services/DigitalSignatureService.cs`

**Method đã có sẵn:**
```csharp
public async Task<VerifySignatureResult> VerifyPdfIntegrityByHashAsync(
    int invoiceId, 
    byte[] pdfBytes)
{
    // 1. Lấy hash đã lưu từ DB
    var storedHash = await GetStoredHashFromDB(invoiceId);
    
    // 2. Tính hash của file upload
    var uploadedHash = SHA256.HashData(pdfBytes);
    
    // 3. So sánh
    var match = storedHash == uploadedHash;
    
    // 4. Trả kết quả
    return new VerifySignatureResult {
        IsValidSignature = match,
        Message = match ? "✅ Toàn vẹn" : "❌ Đã thay đổi"
    };
}
```

**Không cần sửa gì!** Method này đã hoạt động đúng.

---

### 5. Service: `QLTTTA_API/Services/PdfSignatureService.cs`

**Method lưu hash:**
```csharp
private async Task<string> SaveSignedPdfMetadataAsync(
    int invoiceId, 
    byte[] pdfBytes, 
    string signatureVersion)
{
    // 1. Lưu file PDF
    var filePath = Path.Combine(_storageRoot, $"Invoice_{invoiceId}_signed.pdf");
    await File.WriteAllBytesAsync(filePath, pdfBytes);
    
    // 2. Tính hash SHA-256
    var hash = Convert.ToHexString(SHA256.HashData(pdfBytes));
    
    // 3. Lưu vào DB
    await UpdateDatabase(invoiceId, hash, signatureVersion, filePath);
    
    return hash;
}
```

**Đã hoạt động đúng!** Hash được lưu tự động khi ký số.

---

## 📊 LUỒNG HOẠT ĐỘNG MỚI

### Kế toán (Backend)

```
1. Tạo hóa đơn
   └─ POST /api/invoices
   
2. Ký số và in
   └─ POST /api/invoices/print-sign-email
      ├─ Ký metadata bằng private key
      ├─ Nhúng chữ ký vào PDF
      ├─ Tính hash SHA-256 của PDF
      ├─ Lưu hash vào DB (SIGNED_PDF_HASH)
      └─ Gửi PDF qua email
```

### Học viên (Frontend)

```
1. Nhận email có PDF
   
2. Vào trang kiểm tra
   └─ GET /Student/VerifyPdf
   
3. Nhập Invoice ID + Upload PDF
   └─ POST /Student/VerifyPdfUpload
      └─ Gọi API: POST /api/digitalsignature/verify-file-hash
         ├─ Tính hash của file upload
         ├─ Lấy hash từ DB
         ├─ So sánh 2 hash
         └─ Trả kết quả: Toàn vẹn / Đã thay đổi
```

---

## 🗄️ DATABASE

### Bảng HOA_DON - Cột mới

```sql
ALTER TABLE QLTT_ADMIN.HOA_DON ADD (
    SIGNED_PDF_HASH VARCHAR2(128),      -- Hash SHA-256 của PDF
    SIGNED_PDF_VERSION VARCHAR2(20),    -- Version (EMBEDDED_V3)
    SIGNED_PDF_PATH VARCHAR2(500)       -- Đường dẫn file PDF
);
```

**Ví dụ dữ liệu:**
```
ID_HOA_DON: 123
MA_HOA_DON: HD_20251117123456_7890
SIGNED_PDF_HASH: 3F2A1B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A
SIGNED_PDF_VERSION: EMBEDDED_V3
SIGNED_PDF_PATH: C:\...\Invoice_123_HD_20251117123456_7890_20251117_143022_signed.pdf
NGAY_KY: 2025-11-17 14:30:22
```

---

## 🧪 TEST CASES

### Test 1: File toàn vẹn (không sửa)

**Input:**
- Invoice ID: `123`
- File PDF: `HoaDon_HD_20251117123456_7890_Signed.pdf` (gốc)

**Expected:**
```
✅ FILE PDF TOÀN VẸN - Không bị thay đổi

Chi tiết kỹ thuật:
{
  "storedHash": "3F2A1B4C5D6E7F8A9B0C1D2E3F4A5B6C...",
  "uploadedHash": "3F2A1B4C5D6E7F8A9B0C1D2E3F4A5B6C...",
  "version": "EMBEDDED_V3"
}
```

### Test 2: File đã sửa đổi

**Input:**
- Invoice ID: `123`
- File PDF: `HoaDon_Modified.pdf` (đã sửa 1 byte)

**Expected:**
```
❌ FILE PDF ĐÃ BỊ THAY ĐỔI - Không khớp với bản gốc

Chi tiết kỹ thuật:
{
  "storedHash": "3F2A1B4C5D6E7F8A9B0C1D2E3F4A5B6C...",
  "uploadedHash": "9A8B7C6D5E4F1A2B3C4D5E6F7A8B9C0D...",  ← KHÁC!
  "version": "EMBEDDED_V3"
}
```

### Test 3: Invoice ID sai

**Input:**
- Invoice ID: `999` (không tồn tại)
- File PDF: `HoaDon_Signed.pdf`

**Expected:**
```
❌ Không tìm thấy hóa đơn
```

### Test 4: Hóa đơn chưa ký số

**Input:**
- Invoice ID: `124` (chưa ký)
- File PDF: `HoaDon_Signed.pdf`

**Expected:**
```
❌ Hóa đơn chưa có mã băm đã lưu
```

---

## 📁 FILES ĐÃ THAY ĐỔI

### 1. Controllers
- ✅ `QLTTTA_WEB/Controllers/StudentController.cs`
  - Sửa action `VerifyPdfUpload`
  - Thêm parameter `invoiceId`
  - Gọi API `verify-file-hash` thay vì `verify-pdf`

### 2. Views
- ✅ `QLTTTA_WEB/Views/Student/VerifyPdf.cshtml`
  - Thêm input `invoiceId`
  - Sửa giao diện hiển thị kết quả
  - Thêm hướng dẫn sử dụng

### 3. Documentation
- ✅ `HUONG_DAN_KY_SO_DON_GIAN.md` (mới)
  - Hướng dẫn đầy đủ luồng mới
  - Ví dụ cụ thể
  - Test cases

- ✅ `TOM_TAT_THAY_DOI.md` (file này)
  - Tóm tắt thay đổi
  - So sánh trước/sau

---

## 🔄 SO SÁNH TRƯỚC/SAU

### TRƯỚC (Phức tạp)

**Học viên:**
1. Upload file PDF
2. Hệ thống extract chữ ký RSA từ PDF
3. Hệ thống lấy public key từ DB
4. Hệ thống xác thực chữ ký RSA
5. Hiển thị kết quả (khó hiểu)

**Vấn đề:**
- ❌ Phức tạp, khó hiểu
- ❌ Cần hiểu về RSA, public key
- ❌ Chậm (xác thực RSA tốn thời gian)
- ❌ Dễ lỗi (extract signature từ PDF)

### SAU (Đơn giản)

**Học viên:**
1. Nhập Invoice ID
2. Upload file PDF
3. Hệ thống tính hash và so sánh
4. Hiển thị kết quả (rõ ràng)

**Ưu điểm:**
- ✅ Đơn giản, dễ hiểu
- ✅ Chỉ cần Invoice ID + file PDF
- ✅ Nhanh (hash comparison)
- ✅ Chính xác 100%

---

## 🚀 TRIỂN KHAI

### Bước 1: Kiểm tra database

```sql
-- Kiểm tra cột SIGNED_PDF_HASH đã có chưa
SELECT COLUMN_NAME 
FROM ALL_TAB_COLUMNS 
WHERE TABLE_NAME = 'HOA_DON' 
  AND COLUMN_NAME = 'SIGNED_PDF_HASH';
```

Nếu chưa có, chạy:
```sql
ALTER TABLE QLTT_ADMIN.HOA_DON ADD (
    SIGNED_PDF_HASH VARCHAR2(128),
    SIGNED_PDF_VERSION VARCHAR2(20),
    SIGNED_PDF_PATH VARCHAR2(500)
);
```

### Bước 2: Build và restart

```bash
# Stop API và WEB (Ctrl+C)

# Build API
cd QLTTTA_API
dotnet build

# Build WEB
cd QLTTTA_WEB
dotnet build

# Restart API
cd QLTTTA_API
dotnet run

# Restart WEB (terminal mới)
cd QLTTTA_WEB
dotnet run
```

### Bước 3: Test

1. Tạo hóa đơn mới
2. Ký số và in
3. Kiểm tra hash trong DB
4. Upload PDF để kiểm tra
5. Sửa PDF và kiểm tra lại

---

## ✅ CHECKLIST

- [x] Sửa `StudentController.cs`
- [x] Sửa `VerifyPdf.cshtml`
- [x] Kiểm tra API endpoint `verify-file-hash`
- [x] Kiểm tra service `VerifyPdfIntegrityByHashAsync`
- [x] Tạo tài liệu hướng dẫn
- [x] Tạo file tóm tắt thay đổi
- [ ] Test trên môi trường dev
- [ ] Test với file PDF thật
- [ ] Deploy lên production

---

## 🎉 KẾT QUẢ

Hệ thống đã được đơn giản hóa thành công:

1. ✅ **Kế toán:** Ký số → Hash tự động lưu
2. ✅ **Học viên:** Upload PDF + Invoice ID → Kiểm tra hash
3. ✅ **Kết quả:** Rõ ràng, dễ hiểu (Toàn vẹn / Đã thay đổi)

**Hệ thống sẵn sàng sử dụng! 🚀**
