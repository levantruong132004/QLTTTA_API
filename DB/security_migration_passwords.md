# Di trú mật khẩu trong `TAI_KHOAN`

Mục tiêu: Không lưu mật khẩu thuần (plaintext). Chỉ lưu hash mạnh (Argon2id khuyến nghị, hoặc bcrypt).

## Lựa chọn hash

- Ưu tiên: Argon2id (memory-hard; tham số ví dụ: m=64MB, t=3, p=1)
- Thay thế: bcrypt (cost >= 12)

Cột hiện tại `TAI_KHOAN.MAT_KHAU` là `VARCHAR2(256)` – đủ cho hầu hết chuỗi hash (bcrypt ~60 ký tự, Argon2 ~95-128 ký tự). Không cần đổi cấu trúc.

## Quy trình di trú

1. Thêm cờ hệ thống để nhận biết đã hash (nếu cần):
   - Tuỳ chọn: thêm cột `MAT_KHAU_DA_HASH` NUMBER(1) DEFAULT 0
2. Viết job ứng dụng:
   - Đọc từng tài khoản có `MAT_KHAU_DA_HASH = 0`
   - Hash `MAT_KHAU` bằng Argon2id/bcrypt
   - Ghi đè lại `MAT_KHAU` = `<hash>` và đặt `MAT_KHAU_DA_HASH = 1`
3. Khi đăng nhập:
   - Không bao giờ so sánh plaintext; luôn verify bằng hàm verify hash tương ứng

## Gợi ý cài đặt (C#)

- Dùng thư viện:
  - Argon2: `Isopoh.Cryptography.Argon2`
  - Bcrypt: `BCrypt.Net-Next`

Ví dụ (bcrypt):

```csharp
using BCrypt.Net;

string hash = BCrypt.HashPassword(plainPassword, workFactor: 12);
bool ok = BCrypt.Verify(plainPassword, hash);
```

## Lưu ý

- Không gửi mật khẩu người dùng xuống DB; dùng Proxy Auth (APP_PROXY) để vào DB.
- Bảo vệ khóa/secret của ứng dụng bằng Key Vault/DPAPI.
- Thêm rate limiting, lockout, MFA/OTP nếu cần tăng cường an toàn.
