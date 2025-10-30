# Hệ thống Chữ ký Số RSA cho Hóa đơn

## Tổng quan

Hệ thống chữ ký số này được thiết kế theo tiêu chuẩn RSA với các đặc điểm:

- **Thuật toán**: RSA-2048 với SHA-256
- **Public Key**: Lưu trữ trong database (bảng KE_TOAN)
- **Private Key**: Lưu trữ tại máy cá nhân của kế toán (file .pem hoặc .key)
- **Chữ ký**: Lưu trữ dạng Base64 trong database (bảng HOA_DON)

## Cấu trúc Database

### Bảng HOA_DON (thêm các cột mới):
- `CHU_KY_BASE64` (CLOB): Chữ ký số dạng Base64
- `THUAT_TOAN` (VARCHAR2): Thuật toán sử dụng (RSA-SHA256)
- `ID_KE_TOAN_KY` (NUMBER): ID kế toán đã ký
- `NGAY_KY` (DATE): Ngày ký số

### Bảng KE_TOAN (thêm cột):
- `KHOA_CONG_PEM` (CLOB): Public key RSA dạng PEM

## API Endpoints

### 1. Tạo cặp khóa RSA
```
POST /api/DigitalSignature/generate-keypair
```

**Response:**
```json
{
  "success": true,
  "message": "Tạo cặp khóa RSA thành công",
  "data": {
    "publicKey": "-----BEGIN RSA PUBLIC KEY-----\n...\n-----END RSA PUBLIC KEY-----",
    "privateKey": "-----BEGIN RSA PRIVATE KEY-----\n...\n-----END RSA PRIVATE KEY-----",
    "instructions": {
      "publicKey": "Lưu public key này vào database (bảng KE_TOAN, cột KHOA_CONG_PEM)",
      "privateKey": "Lưu private key này vào file trên máy cá nhân (.pem hoặc .key) và bảo mật cẩn thận"
    }
  }
}
```

### 2. Ký số hóa đơn
```
POST /api/DigitalSignature/sign-invoice
```

**Request Body:**
```json
{
  "invoiceId": 123,
  "privateKeyPath": "C:\\Users\\ketoan\\keys\\private_key.pem",
  "accountantId": 1
}
```

### 3. Xác thực chữ ký hóa đơn
```
GET /api/DigitalSignature/verify-invoice/{invoiceId}
```

**Response:**
```json
{
  "success": true,
  "message": "Chữ ký hợp lệ",
  "data": {
    "isValid": true,
    "signedBy": "Nguyễn Văn A",
    "signedDate": "2025-10-30T10:30:00",
    "invoiceData": "{\"invoiceCode\":\"HD_20251030103000_1234\",...}"
  }
}
```

### 4. Tạo và ký hóa đơn cùng lúc
```
POST /api/Invoices/create-and-sign
```

**Request Body:**
```json
{
  "registrationId": 456,
  "dueDate": "2025-11-30T00:00:00",
  "amount": 5000000,
  "privateKeyPath": "C:\\Users\\ketoan\\keys\\private_key.pem",
  "accountantId": 1
}
```

### 5. Lấy hóa đơn với thông tin chữ ký
```
GET /api/Invoices/with-signature/{invoiceId}
```

### 6. Xuất public key của kế toán
```
GET /api/DigitalSignature/export-public-key/{accountantId}
```

## Quy trình sử dụng

### Bước 1: Thiết lập ban đầu

1. **Chạy script cập nhật database:**
   ```sql
   -- Chạy file DB/digital_signature_schema.sql
   ```

2. **Tạo cặp khóa cho kế toán:**
   ```bash
   POST /api/DigitalSignature/generate-keypair
   ```

3. **Lưu trữ khóa:**
   - **Public Key**: Cập nhật vào database bảng KE_TOAN
   ```sql
   UPDATE KE_TOAN 
   SET KHOA_CONG_PEM = '-----BEGIN RSA PUBLIC KEY-----...'
   WHERE ID_KE_TOAN = 1;
   ```
   
   - **Private Key**: Lưu vào file trên máy kế toán
   ```
   C:\Users\ketoan\keys\private_key.pem
   ```

### Bước 2: Ký số hóa đơn

**Tùy chọn A - Ký riêng biệt:**
1. Tạo hóa đơn: `POST /api/Invoices`
2. Ký hóa đơn: `POST /api/DigitalSignature/sign-invoice`

**Tùy chọn B - Tạo và ký cùng lúc:**
1. `POST /api/Invoices/create-and-sign`

### Bước 3: Xác thực chữ ký

1. **Xác thực tự động khi xem hóa đơn:**
   ```bash
   GET /api/Invoices/with-signature/{invoiceId}
   ```

2. **Xác thực riêng biệt:**
   ```bash
   GET /api/DigitalSignature/verify-invoice/{invoiceId}
   ```

## Bảo mật

### Private Key:
- **QUAN TRỌNG**: Private key phải được bảo mật tuyệt đối
- Lưu trữ tại máy cá nhân của kế toán
- Không chia sẻ hoặc upload lên server
- Nên backup an toàn và mã hóa

### Public Key:
- Lưu trữ trong database
- Có thể chia sẻ công khai
- Dùng để xác thực chữ ký

## Đặc điểm kỹ thuật

### Thuật toán:
- **RSA**: 2048-bit key size
- **Hash**: SHA-256
- **Padding**: PKCS#1 v1.5

### Dữ liệu được ký:
```json
{
  "invoiceCode": "HD_20251030103000_1234",
  "createdDate": "2025-10-30 10:30:00",
  "dueDate": "2025-11-30 00:00:00",
  "amount": 5000000,
  "registrationId": 456
}
```

### Định dạng chữ ký:
- **Input**: JSON string của dữ liệu hóa đơn
- **Process**: SHA-256 hash → RSA signature
- **Output**: Base64 encoded signature

## Tuân thủ tiêu chuẩn

Hệ thống này tuân thủ các tiêu chuẩn chữ ký số:

1. **Tính xác thực (Authentication)**: Xác định được người ký
2. **Tính toàn vẹn (Integrity)**: Phát hiện được thay đổi dữ liệu
3. **Tính không thể chối bỏ (Non-repudiation)**: Người ký không thể phủ nhận
4. **Thuật toán chuẩn**: RSA với SHA-256 được công nhận quốc tế

## Test và Debug

### Test cơ bản:
1. Tạo cặp khóa test
2. Tạo hóa đơn mẫu
3. Ký và xác thực
4. Kiểm tra kết quả

### Log và Debug:
- Các lỗi được log chi tiết
- Kiểm tra file log để debug
- Sử dụng Swagger UI để test API

## Lưu ý quan trọng

1. **Backup Private Key**: Luôn backup private key an toàn
2. **Không chia sẻ Private Key**: Tuyệt đối không chia sẻ private key
3. **Kiểm tra đường dẫn**: Đảm bảo đường dẫn file private key chính xác
4. **Quyền truy cập**: Chỉ kế toán mới có quyền ký hóa đơn
5. **Xác thực định kỳ**: Thường xuyên xác thực các hóa đơn đã ký