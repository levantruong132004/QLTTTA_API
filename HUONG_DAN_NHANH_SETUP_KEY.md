# ⚡ HƯỚNG DẪN NHANH - SETUP KEY ĐỂ XÁC THỰC HỢP LỆ

## 🎯 VẤN ĐỀ

**PDF gốc (không sửa) bị xác thực KHÔNG HỢP LỆ**

**Nguyên nhân:** Private key và Public key không khớp cặp!

## ✅ GIẢI PHÁP NHANH (5 PHÚT)

### Bước 1: Gọi API Tạo Key

**Dùng Postman hoặc curl:**

```bash
POST http://localhost:5165/api/DigitalSignature/generate-center-keypair/save
Content-Type: application/json

{
  "centerName": "Trung tâm Tiếng Anh LDA",
  "address": "Đại Học Công Thương, TPHCM",
  "phone": "(028) 1234.5678"
}
```

**Hoặc dùng PowerShell:**
```powershell
$body = @{
    centerName = "Trung tâm Tiếng Anh LDA"
    address = "Đại Học Công Thương, TPHCM"
    phone = "(028) 1234.5678"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5165/api/DigitalSignature/generate-center-keypair/save" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"
```

### Bước 2: Lưu Private Key

**Response sẽ trả về:**
```json
{
  "success": true,
  "message": "Đã tạo và lưu public key cho trung tâm",
  "data": {
    "publicKey": "-----BEGIN RSA PUBLIC KEY-----\n...",
    "privateKey": "-----BEGIN RSA PRIVATE KEY-----\n...",
    "centerName": "Trung tâm Tiếng Anh LDA"
  }
}
```

**Copy `privateKey` và lưu vào file:**
```bash
# Tạo thư mục
mkdir keys

# Tạo file private_key.pem
notepad keys/private_key.pem
```

**Paste nội dung private key vào file và lưu**

### Bước 3: Test

#### 3.1. Tạo hóa đơn mới
1. Đăng nhập kế toán
2. Tạo hóa đơn cho đơn đã duyệt

#### 3.2. Ký số
1. Nhấn "Ký số & In"
2. Upload file `keys/private_key.pem`
3. Chờ thông báo thành công

#### 3.3. Xác thực
1. Lưu PDF từ email
2. Vào: http://localhost:7158/Student/VerifyPdf
3. Upload PDF
4. **Kết quả:** ✅ "Xác thực hợp lệ"

## 🔍 KIỂM TRA

### Kiểm tra Public Key đã lưu vào Database

```sql
-- Kết nối Oracle
sqlplus QLTT_ADMIN/123456@orclpdb

-- Kiểm tra
SELECT 
    ID_TTTA,
    TEN_TRUNG_TAM,
    SUBSTR(KHOA_CONG_PEM, 1, 30) as KEY_START,
    LENGTH(KHOA_CONG_PEM) as KEY_LENGTH
FROM TTTA;
```

**Kết quả mong đợi:**
```
ID_TTTA | TEN_TRUNG_TAM              | KEY_START                  | KEY_LENGTH
--------|----------------------------|----------------------------|------------
1       | Trung tâm Tiếng Anh LDA   | -----BEGIN RSA PUBLIC KEY- | 450-500
```

### Nếu không có dữ liệu

**Có thể bảng TTTA chưa tồn tại. Tạo bảng:**

```sql
-- Tạo bảng TTTA
CREATE TABLE QLTT_ADMIN.TTTA (
    ID_TTTA NUMBER PRIMARY KEY,
    TEN_TRUNG_TAM VARCHAR2(200),
    DIA_CHI VARCHAR2(500),
    DIEN_THOAI VARCHAR2(20),
    KHOA_CONG_PEM CLOB
);

-- Tạo sequence
CREATE SEQUENCE QLTT_ADMIN.TTTA_SEQ START WITH 1 INCREMENT BY 1;

-- Grant quyền
GRANT SELECT, INSERT, UPDATE ON QLTT_ADMIN.TTTA TO PUBLIC;

COMMIT;
```

**Sau đó gọi lại API ở Bước 1**

## ⚠️ LƯU Ý

### 1. Bảo mật Private Key
- ❌ KHÔNG commit vào Git
- ❌ KHÔNG chia sẻ
- ✅ Lưu ở máy cá nhân
- ✅ Backup an toàn

### 2. Nếu mất Private Key
- Tạo cặp key mới (gọi lại API)
- Hóa đơn cũ sẽ xác thực KHÔNG HỢP LỆ
- Cần ký lại tất cả hóa đơn

### 3. Nếu vẫn không hợp lệ
- Kiểm tra log API khi xác thực
- Kiểm tra public key trong database
- Kiểm tra private key file đúng format
- Đảm bảo dùng đúng file private key khi ký

## 🎯 CHECKLIST

- [ ] Gọi API tạo key
- [ ] Lưu private key vào file `keys/private_key.pem`
- [ ] Kiểm tra public key trong database
- [ ] Tạo hóa đơn mới
- [ ] Ký với private key vừa tạo
- [ ] Upload PDF → ✅ Xác thực hợp lệ

## 🚀 TÓM TẮT

```
1. POST /api/DigitalSignature/generate-center-keypair/save
   → Lấy privateKey từ response

2. Lưu privateKey vào keys/private_key.pem

3. Ký hóa đơn với file này

4. Upload PDF → ✅ Hợp lệ
```

---

**Thời gian:** ~5 phút
**Kết quả:** Xác thực hoạt động đúng! 🎉
