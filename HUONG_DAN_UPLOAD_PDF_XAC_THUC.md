# 🎯 HƯỚNG DẪN UPLOAD PDF VÀ XÁC THỰC THÀNH CÔNG

## 📋 TỔNG QUAN

Hệ thống đã có **private key** trong code: `private_key_20251103150352.pem`

Để upload PDF lên xác thực thành công, bạn cần:
1. ✅ Tạo hóa đơn mới
2. ✅ Ký số hóa đơn bằng private key đã có
3. ✅ In và gửi PDF qua email
4. ✅ Upload PDF để xác thực

---

## 🔑 PRIVATE KEY ĐÃ CÓ

**File:** `private_key_20251103150352.pem`

```
-----BEGIN RSA PRIVATE KEY-----
MIIEogIBAAKCAQEAwjuKgrv/U0w57NUvO8z89a0KL2+PMIDw2vTt1x8NK3W2MGBX
AUkRqUSG2yyfXS9km3lfhAjy8jEbvKrIkGgcrloLqNNw7hqcXsgJ4rjAFgVU4YPx
Rk2OsnD/xHqHzkyuSPW6ufysH3b1GaH80tRtLBE3CIKHUQh7FD4sifENKVRwMUgk
P5pmA1Z80AZkzVVqagFiwqO+SwJTJ4CP8FnksrOPCa8RaiDPQdROHot5n8pySL2s
TmwdLehREbUa+UtGfSVwqSkJgelfKYFJAIwXo6STmCU8GxhYL4yfIJmytr/OIEjW
LaT3GrTK8x9xbBAxN/kG7FNZy/GmIL8/7BO9/QIDAQABAoIBAH++pz+Ko8fGJ4bD
Q1iCXpC6KSu/pJ5S/5YSZucITiIaPiQdCLwYsZvxLPyzoXCpPfMfZZmyRQ7TC5oP
fO+0+cAWCvsTbX+8UsHnsNDDj9or0YKsw5/oXISx2xX/PJiLSElGDHRMYWwkUdl0
95I2EkNcYySerJ64BDLxHVvwBSb7c0B33cUjtPqKvxVG2v1GZV6XiZcke2R8JBO+
VVF6E7EKxkgzeHiwm8AFmBCLdlzIfHb8I8U2/8DX2iSMl4pmAhcjO4xLUNDQwzgI
utHwgy6ilctfD91laZPh2EcA6yGS74Bo/J8pcGQ7Nx04unQc4N9voj1oLHjLgitg
SuFCZYUCgYEA6xOZtXttY4zAmuuxl2pMyjOwyRPArOpViJ4VOYeKkwZ4FtL5fAzh
G4UiUqGpYVN563Z80/+JQC9qIfYc5KodLE2smeVddmD820ilylRv+4S9SAi/Gntz
SOlLJOLMGDXjZFBkUBoJCSFsk8YZi62sSifE+d+i0Fmjv4+ixvZevL8CgYEA04VH
euf4CubElD9wnUEZtAyjvK3e9e64m2E2x9gxQadlHR+W2AH8VJeh2Ep6vxI0qgI1
kVf/lfGTzZIjTBg1fmdXOwKv1nSOoIlXuir4cBtggEV3xoZFRXr21n2twaPiaVpK
2eDyY9ux7Sh/jaK9ssi3IeVpKl743M2qSeZVqEMCgYBoKSwvlPw4YxKo0ozDSc+y
vMq1njH+rGqv+VPwRNWrJe+qNVtkkxRfrFM0B/vUazeXlM3k5dJ8BUZiu/m7fIEm
s1gqbM5H+NuxknQbveRMr1lrhKyg4FiJ0w1/z5qdk6spNNHuCEs+p3fD1sBU+uRf
i1WlXml3JnD/HXcD8AC0YwKBgAsgDHVrdAFmx9ogSBUNUoPE0mvfHUYEK1OI1m/G
cDjKzCeu/KkZ2aK9YvbUXAZmt7xlZ1ngrgG99g8u64paD545Yz4oUwVNlh7dem7B
SdXjqry0aqtXbpdL82WusI/pxcPSyvMQwM79xCr8IVFayO15XyB6R4DCBAbhDl7a
rZCvAoGAMcadXy/lrg94kyeZ1m3GV1Hmb+AFdOInrrqJCHChLWGkbWZTUZMKcTm7
OplJKVwx/HZ0F9n48Gkny4T0W4jgVAiBZcPbPzinH/Nu+ECIsUu3WKbNjNlQLzhb
hJC1Cp7c6sghgx4tjLnCITLxMr/alsIlrw6N2MoJ5v7Fw0i15WI=
-----END RSA PRIVATE KEY-----
```

---

## 🚀 BƯỚC 1: KHỞI ĐỘNG HỆ THỐNG

### 1.1. Khởi động API
```bash
cd QLTTTA_API
dotnet run
```

**Kết quả:** API chạy tại `https://localhost:7158`

### 1.2. Khởi động WEB
```bash
cd QLTTTA_WEB
dotnet run
```

**Kết quả:** WEB chạy tại `https://localhost:7158` (hoặc port khác)

---

## 📝 BƯỚC 2: TẠO HÓA ĐƠN MỚI

### 2.1. Đăng nhập với tài khoản kế toán

**URL:** `https://localhost:7158/Auth/Login`

**Thông tin đăng nhập:**
- Username: `ketoan1` (hoặc tài khoản kế toán khác)
- Password: `123456`

### 2.2. Tạo hóa đơn cho đơn đăng ký đã duyệt

**Cách 1: Qua giao diện WEB**
1. Vào menu **Kế toán** → **Quản lý hóa đơn**
2. Chọn đơn đăng ký đã duyệt
3. Nhấn **Tạo hóa đơn**
4. Nhập:
   - Số tiền: `1900000` (VNĐ)
   - Hạn thanh toán: `2025-12-31`
5. Nhấn **Xác nhận**

**Cách 2: Qua API (Postman/cURL)**
```bash
POST https://localhost:7158/api/invoices
Content-Type: application/json

{
  "registrationId": 1,
  "dueDate": "2025-12-31",
  "amount": 1900000
}
```

**Kết quả:**
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

**Lưu lại:** `invoiceId = 123` (để dùng ở bước sau)

---

## 🔐 BƯỚC 3: KÝ SỐ HÓA ĐƠN

### 3.1. Chuẩn bị file private key

**Đường dẫn:** `C:\path\to\private_key_20251103150352.pem`

**Lưu ý:** Thay `C:\path\to\` bằng đường dẫn thực tế trên máy bạn

### 3.2. Ký số qua API

**Endpoint:** `POST /api/invoices/print-sign-email`

**Cách gọi:**

#### Qua Postman:
1. Method: `POST`
2. URL: `https://localhost:7158/api/invoices/print-sign-email`
3. Body → form-data:
   - `invoiceId`: `123` (type: Text)
   - `accountantId`: `1` (type: Text)
   - `privateKey`: Chọn file `private_key_20251103150352.pem` (type: File)
4. Nhấn **Send**

#### Qua cURL:
```bash
curl -X POST "https://localhost:7158/api/invoices/print-sign-email" \
  -F "invoiceId=123" \
  -F "accountantId=1" \
  -F "privateKey=@C:\path\to\private_key_20251103150352.pem"
```

**Kết quả:**
- File PDF được trả về: `HoaDon_HD_20251117123456_7890_20251117_143022_Signed.pdf`
- Email được gửi tới học viên (nếu có email)
- Response headers:
  - `X-Email-Sent: true`
  - `X-Already-Signed: false`

### 3.3. Lưu file PDF

**Lưu file PDF vừa tải về vào:** `C:\Downloads\HoaDon_Signed.pdf`

---

## ✅ BƯỚC 4: XÁC THỰC PDF

### 4.1. Xác thực qua giao diện WEB

**URL:** `https://localhost:7158/Student/VerifyPdf`

**Các bước:**
1. Nhấn **Chọn file PDF**
2. Chọn file: `C:\Downloads\HoaDon_Signed.pdf`
3. Nhấn **Xác thực**

**Kết quả mong đợi:**
```
┌─────────────────────────────────────────────────────┐
│ ✓ Xác thực hợp lệ                                  │
│                                                     │
│ ✅ CHỮ KÝ VÀ PDF HOÀN TOÀN HỢP LỆ                 │
│                                                     │
│ 📄 THÔNG TIN HÓA ĐƠN:                             │
│ Mã hóa đơn: HD_20251117123456_7890                │
│ Học viên: Nguyễn Văn A                            │
│ Khóa học: Tiếng Anh Giao Tiếp                     │
│ Lớp học: TA01                                      │
│ Số tiền: 1,900,000 VNĐ                            │
│ Ngày tạo: 2025-11-17 14:30:22                     │
│                                                     │
│ 🛡️ CHI TIẾT XÁC THỰC:                             │
│ 🔐 Chữ ký RSA: ✅ HỢP LỆ                          │
│ 📄 Tính toàn vẹn PDF: ✅ KHÔNG BỊ THAY ĐỔI       │
│ 🕐 Thời gian ký: 2025-11-17 14:30:22              │
│ 🔒 Thuật toán: RSA-SHA256-PDF-EMBEDDED             │
│ 🏛️ Đơn vị phát hành: Trung tâm Tiếng Anh LDA     │
│ 📋 Version: 3.0 Enhanced                           │
│ ✓ File PDF này là tài liệu chính thức             │
└─────────────────────────────────────────────────────┘
```

### 4.2. Xác thực qua API

**Endpoint:** `POST /api/DigitalSignature/verify-pdf`

**Cách gọi:**

#### Qua Postman:
1. Method: `POST`
2. URL: `https://localhost:7158/api/DigitalSignature/verify-pdf`
3. Body → form-data:
   - `pdf`: Chọn file `HoaDon_Signed.pdf` (type: File)
4. Nhấn **Send**

#### Qua cURL:
```bash
curl -X POST "https://localhost:7158/api/DigitalSignature/verify-pdf" \
  -F "pdf=@C:\Downloads\HoaDon_Signed.pdf"
```

**Response:**
```json
{
  "success": true,
  "message": "CHỮ KÝ VÀ PDF HOÀN TOÀN HỢP LỆ\n\n📄 THÔNG TIN HÓA ĐƠN:\nMã hóa đơn: HD_20251117123456_7890\n...",
  "data": {
    "isValid": true,
    "invoiceData": "{\"InvoiceCode\":\"HD_20251117123456_7890\",...}"
  }
}
```

---

## 🧪 BƯỚC 5: TEST PDF BỊ SỬA ĐỔI

### 5.1. Sửa PDF bằng Notepad++

1. Mở file `HoaDon_Signed.pdf` bằng **Notepad++**
2. Tìm kiếm: `1,900,000` (số tiền)
3. Thay đổi thành: `1,000,000`
4. Lưu file

### 5.2. Upload PDF đã sửa để xác thực

**URL:** `https://localhost:7158/Student/VerifyPdf`

**Kết quả mong đợi:**
```
┌─────────────────────────────────────────────────────┐
│ ✗ Xác thực không hợp lệ                            │
│                                                     │
│ ❌ PDF ĐÃ BỊ THAY ĐỔI                              │
│                                                     │
│ ⚠️ Chi tiết vấn đề:                                │
│ 🔐 Chữ ký RSA: ✅ Hợp lệ                           │
│ 📄 Tính toàn vẹn PDF: ❌ Đã thay đổi              │
│ Expected Hash: 3F2A1B4C5D6E...                     │
│ Current Hash:  9A8B7C6D5E4F...                     │
│                                                     │
│ ❗ KHÔNG SỬ DỤNG FILE NÀY CHO GIAO DỊCH           │
└─────────────────────────────────────────────────────┘
```

---

## 🔍 KIỂM TRA LOG

### API Log (Terminal chạy API)

**Khi ký số thành công:**
```
info: QLTTTA_API.Controllers.InvoicesController[0]
      🔄 Bắt đầu tạo PDF có chữ ký cho hóa đơn 123
info: QLTTTA_API.Services.PdfSignatureService[0]
      Đã tạo base PDF (styled) cho hóa đơn 123 tại C:\...\InvoicesPdf\Invoice_123_base.pdf
info: QLTTTA_API.Services.PdfSignatureService[0]
      📄 Đã tạo final PDF bytes, size = 245678 bytes
info: QLTTTA_API.Services.PdfSignatureService[0]
      💾 Đã lưu signed PDF tại: C:\...\InvoicesPdf\Invoice_123_HD_20251117123456_7890_20251117_143022_signed.pdf
info: QLTTTA_API.Services.PdfSignatureService[0]
      ✅ Đã ký PDF styled cho hóa đơn 123 - HD_20251117123456_7890 - Hash 3F2A1B4C5D6E...
info: QLTTTA_API.Controllers.InvoicesController[0]
      ✅ Đã tạo PDF có chữ ký cho hóa đơn 123, size = 245678 bytes
```

**Khi xác thực thành công:**
```
info: QLTTTA_API.Services.DigitalSignatureService[0]
      🔍 Bắt đầu xác thực PDF có chữ ký số tích hợp Version 3.0
info: QLTTTA_API.Services.DigitalSignatureService[0]
      ✅ Hoàn thành xác thực PDF Version 3.0 - Kết quả: True
```

**Khi xác thực thất bại (PDF bị sửa):**
```
warn: QLTTTA_API.Services.DigitalSignatureService[0]
      ⚠️ PDF đã bị thay đổi - Hash không khớp
      Expected: 3F2A1B4C5D6E...
      Current:  9A8B7C6D5E4F...
```

---

## 📊 LUỒNG KÝ SỐ HOÀN CHỈNH

```
┌─────────────────────────────────────────────────────────────┐
│                    LUỒNG KÝ SỐ VERSION 3.0                  │
└─────────────────────────────────────────────────────────────┘

1. TẠO HÓA ĐƠN
   ├─ Kế toán tạo hóa đơn cho đơn đăng ký đã duyệt
   ├─ Hệ thống tạo base PDF (chưa ký)
   └─ Lưu vào: InvoicesPdf/Invoice_{id}_base.pdf

2. KÝ SỐ HÓA ĐƠN
   ├─ Kế toán upload private key
   ├─ Hệ thống ký metadata hóa đơn (JSON)
   ├─ Lưu chữ ký vào DB (CHU_KY_BASE64)
   └─ Cập nhật NGAY_KY, ID_KE_TOAN_KY

3. TẠO PDF CÓ CHỮ KÝ
   ├─ Đọc base PDF
   ├─ Tính hash của base PDF (SHA256)
   ├─ Tạo signature metadata:
   │  ├─ InvoiceCode, StudentName, Amount, ...
   │  ├─ OriginalPdfHash (hash của base PDF)
   │  ├─ SignatureAlgorithm: RSA-SHA256-PDF-EMBEDDED
   │  └─ SignatureVersion: 3.0
   ├─ Ký metadata bằng private key
   ├─ Nhúng signature vào PDF:
   │  ├─ %%LDA-SIGNATURE-BOUNDARY-START%%
   │  ├─ --BEGIN-LDA-INVOICE-HASH--
   │  ├─ Data: {base64 của metadata}
   │  ├─ Signature: {base64 của chữ ký RSA}
   │  ├─ Timestamp, Algorithm, Version, ...
   │  └─ --END-LDA-INVOICE-HASH--
   ├─ Lưu signed PDF
   └─ Lưu hash vào DB (SIGNED_PDF_HASH)

4. GỬI EMAIL
   ├─ Đính kèm signed PDF
   └─ Gửi tới email học viên

5. XÁC THỰC PDF
   ├─ Học viên upload PDF
   ├─ Hệ thống tách signature metadata
   ├─ Tính hash của PDF thuần (trước signature boundary)
   ├─ So sánh với OriginalPdfHash trong metadata
   ├─ Xác thực chữ ký RSA bằng public key
   └─ Trả kết quả: HỢP LỆ / KHÔNG HỢP LỆ
```

---

## 🛡️ BẢO MẬT

### Private Key
- ✅ **KHÔNG BAO GIỜ** commit private key vào Git
- ✅ Lưu private key ở nơi an toàn (USB, vault, ...)
- ✅ Chỉ kế toán có quyền truy cập private key
- ✅ Backup private key ở nhiều nơi

### Public Key
- ✅ Lưu trong database (bảng TTTA, cột KHOA_CONG_PEM)
- ✅ Công khai cho mọi người xác thực
- ✅ Không cần bảo mật

### Chữ ký số
- ✅ Lưu trong database (bảng HOA_DON, cột CHU_KY_BASE64)
- ✅ Nhúng trực tiếp vào PDF
- ✅ Không thể giả mạo nếu không có private key

---

## ❓ TROUBLESHOOTING

### Lỗi: "Không tìm thấy file private key"
**Nguyên nhân:** Đường dẫn file sai

**Giải pháp:**
```bash
# Kiểm tra file tồn tại
dir C:\path\to\private_key_20251103150352.pem

# Hoặc dùng đường dẫn tuyệt đối
C:\Users\YourName\Documents\private_key_20251103150352.pem
```

### Lỗi: "Không thể import public key"
**Nguyên nhân:** Public key chưa được lưu vào database

**Giải pháp:**
```sql
-- Kiểm tra public key trong DB
SELECT KHOA_CONG_PEM FROM QLTT_ADMIN.TTTA WHERE ID_TTTA = 1;

-- Nếu NULL, cần tạo và lưu public key
```

### Lỗi: "Xác thực không hợp lệ" (nhưng PDF chưa sửa)
**Nguyên nhân:** Private key và public key không khớp

**Giải pháp:**
1. Tạo lại cặp key mới
2. Lưu public key vào DB
3. Ký lại hóa đơn bằng private key mới

### Lỗi: "Email không được gửi"
**Nguyên nhân:** SMTP settings sai

**Giải pháp:**
```json
// Kiểm tra appsettings.json
{
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderEmail": "your-email@gmail.com",
    "SenderPassword": "your-app-password",
    "EnableSsl": true
  }
}
```

---

## 🎉 KẾT LUẬN

Sau khi hoàn thành các bước trên, bạn đã:

1. ✅ Tạo hóa đơn mới
2. ✅ Ký số hóa đơn bằng private key
3. ✅ Tạo PDF có chữ ký số nhúng (Version 3.0)
4. ✅ Gửi PDF qua email cho học viên
5. ✅ Upload PDF lên xác thực thành công
6. ✅ Kiểm tra PDF bị sửa đổi (xác thực thất bại)

**Hệ thống ký số hoàn chỉnh và an toàn! 🎉**

---

## 📞 HỖ TRỢ

Nếu gặp vấn đề, kiểm tra:
1. Log API (terminal chạy `dotnet run`)
2. Log WEB (terminal chạy `dotnet run`)
3. Database (kiểm tra bảng HOA_DON, TTTA)
4. File PDF (mở bằng Notepad++ để xem signature metadata)

**Chúc bạn thành công! 🚀**
