# 🔐 HƯỚNG DẪN KÝ SỐ VÀ KIỂM TRA TÍNH TOÀN VẸN PDF - ĐƠN GIẢN

## 📋 TỔNG QUAN

Hệ thống sử dụng **chữ ký số tích hợp** và **hash comparison** để đảm bảo tính toàn vẹn của file PDF.

### Luồng hoạt động:

```
┌─────────────────────────────────────────────────────────────┐
│                    LUỒNG ĐƠN GIẢN HÓA                       │
└─────────────────────────────────────────────────────────────┘

1. KẾ TOÁN TẠO HÓA ĐƠN
   └─ Hệ thống tạo base PDF

2. KẾ TOÁN KÝ SỐ VÀ IN
   ├─ Upload private key
   ├─ Hệ thống ký số metadata
   ├─ Nhúng chữ ký vào PDF
   ├─ Tính hash SHA-256 của toàn bộ file PDF
   ├─ Lưu hash vào database (SIGNED_PDF_HASH)
   └─ Gửi PDF qua email cho học viên

3. HỌC VIÊN KIỂM TRA
   ├─ Upload file PDF đã nhận
   ├─ Nhập Invoice ID
   ├─ Hệ thống tính hash của file upload
   ├─ So sánh với hash trong database
   └─ Trả kết quả: TOÀN VẸN / ĐÃ THAY ĐỔI
```

---

## 🚀 BƯỚC 1: THIẾT LẬP BAN ĐẦU

### 1.1. Tạo cặp khóa RSA (chỉ làm 1 lần)

**Cách 1: Qua API (Khuyến nghị)**

```bash
POST https://localhost:7158/api/DigitalSignature/generate-center-keypair/save
Content-Type: application/json

{
  "centerName": "Trung tâm Tiếng Anh LDA",
  "address": "Đại Học Công Thương, TPHCM",
  "phone": "(028) 1234.5678"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Đã tạo và lưu public key trung tâm thành công",
  "data": {
    "publicKey": "-----BEGIN RSA PUBLIC KEY-----\nMIIB...",
    "privateKey": "-----BEGIN RSA PRIVATE KEY-----\nMIIE...",
    "centerName": "Trung tâm Tiếng Anh LDA"
  }
}
```

**⚠️ QUAN TRỌNG:**
- **Public key** đã được lưu tự động vào database (bảng TTTA)
- **Private key** chỉ hiển thị 1 lần duy nhất!
- Phải lưu ngay private key vào file `.pem` và bảo mật cẩn thận!

**Lưu private key:**
```bash
# Tạo file private_key.pem
notepad private_key_20251117.pem

# Copy nội dung private key từ response và paste vào file
# Lưu file vào nơi an toàn
```

### 1.2. Kiểm tra public key đã được lưu

```sql
SELECT 
    TEN_TRUNG_TAM,
    CASE 
        WHEN KHOA_CONG_PEM IS NULL THEN '❌ CHƯA CÓ'
        WHEN LENGTH(KHOA_CONG_PEM) > 400 THEN '✅ ĐÃ CÓ'
        ELSE '⚠️ LỖI'
    END AS TRANG_THAI
FROM QLTT_ADMIN.TTTA 
WHERE ID_TTTA = 1;
```

---

## 📝 BƯỚC 2: TẠO HÓA ĐƠN

### 2.1. Kế toán tạo hóa đơn

**Qua API:**
```bash
POST https://localhost:7158/api/invoices
Content-Type: application/json

{
  "registrationId": 1,
  "dueDate": "2025-12-31",
  "amount": 1900000
}
```

**Response:**
```json
{
  "success": true,
  "message": "Tạo hóa đơn thành công",
  "data": {
    "invoiceId": 123,
    "invoiceCode": "HD_20251117123456_7890",
    "amount": 1900000,
    "status": "Chưa thanh toán"
  }
}
```

**Lưu lại:** `invoiceId = 123`

---

## 🔐 BƯỚC 3: KÝ SỐ VÀ IN HÓA ĐƠN

### 3.1. Kế toán ký số và gửi PDF

**Endpoint:** `POST /api/invoices/print-sign-email`

**Qua Postman:**
1. Method: `POST`
2. URL: `https://localhost:7158/api/invoices/print-sign-email`
3. Body → form-data:
   - `invoiceId`: `123` (Text)
   - `accountantId`: `1` (Text)
   - `privateKey`: Chọn file `private_key_20251117.pem` (File)
4. Send

**Qua cURL:**
```bash
curl -X POST "https://localhost:7158/api/invoices/print-sign-email" \
  -F "invoiceId=123" \
  -F "accountantId=1" \
  -F "privateKey=@C:\path\to\private_key_20251117.pem"
```

### 3.2. Hệ thống xử lý

```
1. Đọc private key
2. Ký số metadata hóa đơn (JSON)
3. Lưu chữ ký vào DB (CHU_KY_BASE64)
4. Tạo PDF có chữ ký nhúng
5. Tính hash SHA-256 của toàn bộ file PDF
6. Lưu hash vào DB (SIGNED_PDF_HASH)
7. Gửi PDF qua email cho học viên
8. Trả về file PDF cho kế toán
```

**Kết quả:**
- File PDF: `HoaDon_HD_20251117123456_7890_20251117_143022_Signed.pdf`
- Email đã gửi: `true`
- Hash đã lưu trong DB

### 3.3. Kiểm tra hash trong database

```sql
SELECT 
    ID_HOA_DON,
    MA_HOA_DON,
    SIGNED_PDF_HASH,
    SIGNED_PDF_VERSION,
    NGAY_KY
FROM QLTT_ADMIN.HOA_DON
WHERE ID_HOA_DON = 123;
```

**Kết quả:**
```
ID_HOA_DON: 123
MA_HOA_DON: HD_20251117123456_7890
SIGNED_PDF_HASH: 3F2A1B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A
SIGNED_PDF_VERSION: EMBEDDED_V3
NGAY_KY: 2025-11-17 14:30:22
```

---

## ✅ BƯỚC 4: HỌC VIÊN KIỂM TRA TÍNH TOÀN VẸN

### 4.1. Học viên nhận email

Email chứa:
- File PDF đính kèm: `HoaDon_HD_20251117123456_7890_20251117_143022_Signed.pdf`
- Thông tin hóa đơn: Mã, số tiền, hạn thanh toán
- **Invoice ID: 123** (quan trọng - dùng để kiểm tra)

### 4.2. Học viên lưu file PDF

Lưu file PDF vào máy: `C:\Downloads\HoaDon_Signed.pdf`

### 4.3. Học viên kiểm tra tính toàn vẹn

**Qua giao diện WEB:**

1. Đăng nhập: `https://localhost:7158/Auth/Login`
2. Vào: `https://localhost:7158/Student/VerifyPdf`
3. Nhập **Invoice ID**: `123`
4. Chọn file PDF: `C:\Downloads\HoaDon_Signed.pdf`
5. Nhấn **"Kiểm tra tính toàn vẹn"**

**Qua API:**

```bash
POST https://localhost:7158/api/DigitalSignature/verify-file-hash
Content-Type: multipart/form-data

Form data:
- invoiceId: 123
- pdf: [file HoaDon_Signed.pdf]
```

### 4.4. Kết quả kiểm tra

#### ✅ Trường hợp 1: File TOÀN VẸN (không bị sửa)

**Giao diện:**
```
┌─────────────────────────────────────────────────────┐
│ ✅ FILE PDF TOÀN VẸN - Không bị thay đổi          │
│                                                     │
│ File PDF này khớp với bản gốc trong hệ thống.     │
│ Bạn có thể yên tâm sử dụng.                       │
│                                                     │
│ Chi tiết kỹ thuật:                                 │
│ {                                                   │
│   "storedHash": "3F2A1B4C5D6E...",                │
│   "uploadedHash": "3F2A1B4C5D6E...",              │
│   "version": "EMBEDDED_V3",                        │
│   "filePath": "Invoice_123_...pdf"                │
│ }                                                   │
└─────────────────────────────────────────────────────┘
```

**API Response:**
```json
{
  "success": true,
  "message": "✅ File PDF trùng khớp mã băm đã lưu",
  "data": {
    "isIntact": true,
    "details": "{\"storedHash\":\"3F2A1B4C5D6E...\",\"uploadedHash\":\"3F2A1B4C5D6E...\"}"
  }
}
```

#### ❌ Trường hợp 2: File ĐÃ BỊ THAY ĐỔI

**Giao diện:**
```
┌─────────────────────────────────────────────────────┐
│ ❌ FILE PDF ĐÃ BỊ THAY ĐỔI - Không khớp bản gốc   │
│                                                     │
│ File PDF này không khớp với bản gốc.              │
│ Có thể đã bị chỉnh sửa hoặc hư hỏng.             │
│                                                     │
│ Chi tiết kỹ thuật:                                 │
│ {                                                   │
│   "storedHash": "3F2A1B4C5D6E...",                │
│   "uploadedHash": "9A8B7C6D5E4F...",  ← KHÁC!     │
│   "version": "EMBEDDED_V3"                         │
│ }                                                   │
└─────────────────────────────────────────────────────┘
```

**API Response:**
```json
{
  "success": true,
  "message": "❌ Mã băm của file không trùng với dữ liệu đã lưu",
  "data": {
    "isIntact": false,
    "details": "{\"storedHash\":\"3F2A1B4C5D6E...\",\"uploadedHash\":\"9A8B7C6D5E4F...\"}"
  }
}
```

---

## 🧪 BƯỚC 5: TEST FILE BỊ SỬA ĐỔI

### 5.1. Sửa file PDF

```bash
# Mở file bằng Notepad++
notepad++ C:\Downloads\HoaDon_Signed.pdf

# Tìm kiếm: 1,900,000
# Thay đổi thành: 1,000,000
# Lưu file
```

### 5.2. Kiểm tra lại

Upload file đã sửa lên hệ thống → Kết quả: **❌ ĐÃ BỊ THAY ĐỔI**

---

## 🔍 CẤU TRÚC DATABASE

### Bảng HOA_DON

```sql
CREATE TABLE QLTT_ADMIN.HOA_DON (
    ID_HOA_DON NUMBER(10) PRIMARY KEY,
    MA_HOA_DON VARCHAR2(50),
    NGAY_TAO DATE,
    NGAY_HET_HAN DATE,
    SO_TIEN NUMBER(10),
    TRANG_THAI VARCHAR2(50),
    ID_DANG_KY NUMBER(10),
    
    -- Chữ ký số
    CHU_KY_BASE64 CLOB,              -- Chữ ký RSA (Base64)
    THUAT_TOAN VARCHAR2(50),         -- RSA-SHA256
    ID_KE_TOAN_KY NUMBER(10),        -- ID kế toán đã ký
    NGAY_KY DATE,                    -- Ngày giờ ký số
    
    -- Hash để kiểm tra tính toàn vẹn
    SIGNED_PDF_HASH VARCHAR2(128),   -- Hash SHA-256 của PDF
    SIGNED_PDF_VERSION VARCHAR2(20), -- Version (EMBEDDED_V3)
    SIGNED_PDF_PATH VARCHAR2(500),   -- Đường dẫn file PDF
    
    -- Trạng thái in
    DA_IN NUMBER(1) DEFAULT 0        -- 0=chưa in, 1=đã in
);
```

### Bảng TTTA (Trung tâm)

```sql
CREATE TABLE QLTT_ADMIN.TTTA (
    ID_TTTA NUMBER(10) PRIMARY KEY,
    TEN_TRUNG_TAM VARCHAR2(200),
    DIA_CHI VARCHAR2(500),
    SO_DIEN_THOAI VARCHAR2(20),
    KHOA_CONG_PEM CLOB              -- Public key RSA
);
```

---

## 📊 SO SÁNH PHƯƠNG PHÁP

| Tính năng | Xác thực chữ ký RSA | Hash Comparison |
|-----------|---------------------|-----------------|
| **Độ phức tạp** | Cao | Thấp |
| **Tốc độ** | Chậm | Nhanh |
| **Bảo mật** | Rất cao | Cao |
| **Dễ hiểu** | Khó | Dễ |
| **Yêu cầu** | Public key | Invoice ID + Hash |
| **Phù hợp** | Xác thực pháp lý | Kiểm tra toàn vẹn |

**Kết luận:** Hệ thống sử dụng **cả 2 phương pháp**:
- **Chữ ký RSA:** Nhúng trong PDF để xác thực pháp lý
- **Hash Comparison:** Cho học viên kiểm tra đơn giản

---

## 🛡️ BẢO MẬT

### Private Key
- ✅ Lưu ở nơi an toàn (USB, vault)
- ✅ Chỉ kế toán có quyền truy cập
- ✅ Backup nhiều nơi
- ❌ KHÔNG commit vào Git
- ❌ KHÔNG gửi qua email

### Public Key
- ✅ Lưu trong database (TTTA.KHOA_CONG_PEM)
- ✅ Công khai cho mọi người
- ✅ Không cần bảo mật

### Hash
- ✅ Lưu trong database (HOA_DON.SIGNED_PDF_HASH)
- ✅ Dùng SHA-256 (an toàn)
- ✅ Không thể đảo ngược

---

## ❓ TROUBLESHOOTING

### Lỗi: "Không tìm thấy hóa đơn"
**Nguyên nhân:** Invoice ID sai

**Giải pháp:**
```sql
-- Kiểm tra Invoice ID
SELECT ID_HOA_DON, MA_HOA_DON 
FROM QLTT_ADMIN.HOA_DON 
WHERE ID_DANG_KY = 1;
```

### Lỗi: "Hóa đơn chưa có mã băm đã lưu"
**Nguyên nhân:** Hóa đơn chưa được ký số và in

**Giải pháp:**
1. Kế toán phải ký số và in hóa đơn trước
2. Kiểm tra:
```sql
SELECT SIGNED_PDF_HASH 
FROM QLTT_ADMIN.HOA_DON 
WHERE ID_HOA_DON = 123;
```

### Lỗi: "File PDF không khớp" (nhưng chưa sửa)
**Nguyên nhân:** 
- File bị hư hỏng khi download
- File bị antivirus sửa đổi
- File bị email client thay đổi

**Giải pháp:**
1. Download lại file PDF từ email
2. Tắt antivirus tạm thời
3. Dùng email client khác

---

## 🎯 CHECKLIST

### Thiết lập ban đầu (1 lần)
- [ ] Tạo cặp khóa RSA
- [ ] Lưu private key vào file .pem
- [ ] Kiểm tra public key đã lưu trong DB
- [ ] Thêm cột SIGNED_PDF_HASH vào bảng HOA_DON

### Kế toán tạo hóa đơn
- [ ] Tạo hóa đơn cho đơn đăng ký đã duyệt
- [ ] Ký số bằng private key
- [ ] In và gửi PDF qua email
- [ ] Kiểm tra hash đã lưu trong DB

### Học viên kiểm tra
- [ ] Nhận email có file PDF
- [ ] Lưu file PDF vào máy
- [ ] Vào trang kiểm tra
- [ ] Nhập Invoice ID
- [ ] Upload file PDF
- [ ] Xem kết quả

---

## 🎉 KẾT LUẬN

Hệ thống đã được đơn giản hóa:

1. ✅ **Kế toán:** Ký số và in → Hash được lưu tự động
2. ✅ **Học viên:** Upload PDF + Invoice ID → Kiểm tra hash
3. ✅ **Kết quả:** Toàn vẹn / Đã thay đổi (rõ ràng, dễ hiểu)

**Ưu điểm:**
- Đơn giản, dễ sử dụng
- Nhanh chóng
- Không cần hiểu về RSA, public key
- Chỉ cần Invoice ID và file PDF

**Hệ thống hoạt động hoàn hảo! 🚀**
