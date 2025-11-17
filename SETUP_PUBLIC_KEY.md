# 🔑 SETUP PUBLIC KEY ĐỂ XÁC THỰC HỢP LỆ

## 🎯 VẤN ĐỀ

**Xác thực luôn KHÔNG HỢP LỆ** vì:
1. ❌ Private key dùng để ký KHÔNG KHỚP với Public key trong database
2. ❌ Hoặc chưa có Public key trong database

## ✅ GIẢI PHÁP

Cần đảm bảo:
- Private key (dùng để ký) và Public key (trong database) là **CÙNG 1 CẶP**

## 🔧 CÁCH SETUP

### Bước 1: Tạo cặp key mới

#### Option A: Dùng API (Khuyến nghị)
```bash
POST http://localhost:5165/api/DigitalSignature/generate-center-keypair/save
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
  "message": "Đã tạo và lưu public key cho trung tâm",
  "data": {
    "publicKey": "-----BEGIN RSA PUBLIC KEY-----\n...\n-----END RSA PUBLIC KEY-----",
    "privateKey": "-----BEGIN RSA PRIVATE KEY-----\n...\n-----END RSA PRIVATE KEY-----",
    "centerName": "Trung tâm Tiếng Anh LDA"
  }
}
```

**Lưu ý:**
- ✅ Public key tự động lưu vào database (bảng TTTA)
- ⚠️ **QUAN TRỌNG:** Copy `privateKey` và lưu vào file `private_key.pem`
- ⚠️ **BẢO MẬT:** Không chia sẻ private key!

#### Option B: Dùng OpenSSL
```bash
# 1. Tạo private key
openssl genrsa -out private_key.pem 2048

# 2. Trích xuất public key
openssl rsa -in private_key.pem -pubout -out public_key.pem

# 3. Xem public key
cat public_key.pem
```

### Bước 2: Lưu Public Key vào Database

```sql
-- Kết nối Oracle với user QLTT_ADMIN
sqlplus QLTT_ADMIN/123456@orclpdb

-- Kiểm tra bảng TTTA có tồn tại không
SELECT * FROM TTTA;

-- Nếu chưa có, tạo bảng
CREATE TABLE TTTA (
    ID_TTTA NUMBER PRIMARY KEY,
    TEN_TRUNG_TAM VARCHAR2(200),
    DIA_CHI VARCHAR2(500),
    DIEN_THOAI VARCHAR2(20),
    KHOA_CONG_PEM CLOB
);

-- Tạo sequence
CREATE SEQUENCE TTTA_SEQ START WITH 1 INCREMENT BY 1;

-- Insert public key (thay <PUBLIC_KEY_PEM> bằng public key thực tế)
INSERT INTO TTTA (ID_TTTA, TEN_TRUNG_TAM, DIA_CHI, DIEN_THOAI, KHOA_CONG_PEM)
VALUES (
    1,
    'Trung tâm Tiếng Anh LDA',
    'Đại Học Công Thương, TPHCM',
    '(028) 1234.5678',
    '-----BEGIN RSA PUBLIC KEY-----
MIIBCgKCAQEA...
...
-----END RSA PUBLIC KEY-----'
);

COMMIT;

-- Kiểm tra
SELECT ID_TTTA, TEN_TRUNG_TAM, LENGTH(KHOA_CONG_PEM) as KEY_LENGTH 
FROM TTTA;
```

### Bước 3: Lưu Private Key vào File

```bash
# Tạo thư mục keys (nếu chưa có)
mkdir keys

# Lưu private key vào file
# Copy nội dung private key từ API response hoặc OpenSSL
notepad keys/private_key.pem
```

**Nội dung file `private_key.pem`:**
```
-----BEGIN RSA PRIVATE KEY-----
MIIEpAIBAAKCAQEA...
...
-----END RSA PRIVATE KEY-----
```

### Bước 4: Test Ký Số

1. Tạo hóa đơn mới
2. Ký số với private key vừa tạo:
   - Upload file `keys/private_key.pem`
3. Kiểm tra log:
   ```
   ✅ Đã ký PDF styled cho hóa đơn...
   ```

### Bước 5: Test Xác Thực

1. Lưu PDF từ email
2. Vào: http://localhost:7158/Student/VerifyPdf
3. Upload PDF
4. **Kết quả mong đợi:** ✅ "Xác thực hợp lệ"

## 🔍 KIỂM TRA

### Kiểm tra Public Key trong Database
```sql
SELECT 
    ID_TTTA,
    TEN_TRUNG_TAM,
    SUBSTR(KHOA_CONG_PEM, 1, 50) as PUBLIC_KEY_START,
    LENGTH(KHOA_CONG_PEM) as KEY_LENGTH
FROM TTTA;
```

**Kết quả mong đợi:**
```
ID_TTTA | TEN_TRUNG_TAM              | PUBLIC_KEY_START                    | KEY_LENGTH
--------|----------------------------|-------------------------------------|------------
1       | Trung tâm Tiếng Anh LDA   | -----BEGIN RSA PUBLIC KEY-----\nMII | 450-500
```

### Kiểm tra Private Key File
```bash
# Xem private key
cat keys/private_key.pem

# Kiểm tra format
openssl rsa -in keys/private_key.pem -check -noout
```

**Kết quả mong đợi:**
```
RSA key ok
```

### Kiểm tra Cặp Key Khớp Nhau
```bash
# 1. Lấy public key từ private key
openssl rsa -in keys/private_key.pem -pubout -out temp_public.pem

# 2. So sánh với public key trong database
# (Copy public key từ database ra file db_public.pem)

# 3. So sánh 2 file
diff temp_public.pem db_public.pem
```

**Kết quả mong đợi:** Không có khác biệt

## ⚠️ LƯU Ý QUAN TRỌNG

### 1. Bảo mật Private Key
- ❌ **KHÔNG** commit vào Git
- ❌ **KHÔNG** chia sẻ qua email
- ❌ **KHÔNG** lưu trên server public
- ✅ Lưu ở máy cá nhân, mã hóa
- ✅ Backup an toàn

### 2. Đồng bộ Key
- ⚠️ Nếu tạo private key mới → Phải cập nhật public key trong database
- ⚠️ Nếu mất private key → Tất cả hóa đơn cũ không thể ký lại
- ⚠️ Nếu đổi public key → Hóa đơn cũ sẽ xác thực KHÔNG HỢP LỆ

### 3. Quy trình Đúng
```
1. Tạo cặp key (private + public)
2. Lưu public key vào database
3. Lưu private key vào file an toàn
4. Dùng private key để ký hóa đơn
5. Hệ thống tự động dùng public key từ database để xác thực
```

## 🎯 CHECKLIST

- [ ] Tạo cặp key (private + public)
- [ ] Lưu public key vào database (bảng TTTA)
- [ ] Lưu private key vào file `keys/private_key.pem`
- [ ] Kiểm tra public key trong database
- [ ] Kiểm tra private key file hợp lệ
- [ ] Test ký hóa đơn với private key
- [ ] Test xác thực PDF → ✅ Hợp lệ
- [ ] Backup private key an toàn

## 🚀 NHANH CHÓNG

### Script Tự Động (PowerShell)
```powershell
# 1. Tạo key
openssl genrsa -out keys/private_key.pem 2048
openssl rsa -in keys/private_key.pem -pubout -out keys/public_key.pem

# 2. Hiển thị public key để copy vào database
Write-Host "=== PUBLIC KEY - Copy vào database ===" -ForegroundColor Green
Get-Content keys/public_key.pem

# 3. Hướng dẫn
Write-Host "`n=== BƯỚC TIẾP THEO ===" -ForegroundColor Yellow
Write-Host "1. Copy public key ở trên"
Write-Host "2. Chạy SQL: INSERT INTO TTTA (...) VALUES (...)"
Write-Host "3. Test ký hóa đơn với file keys/private_key.pem"
```

## ✅ KẾT QUẢ

Sau khi setup đúng:
1. ✅ Ký hóa đơn với private key → Thành công
2. ✅ Upload PDF gốc → Xác thực HỢP LỆ
3. ✅ Upload PDF đã sửa → Xác thực KHÔNG HỢP LỆ
4. ✅ Hệ thống an toàn và đáng tin cậy

---

**Lưu ý:** Đây là bước **QUAN TRỌNG NHẤT** để hệ thống xác thực hoạt động đúng!
