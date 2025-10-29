# Kết nối Oracle an toàn cho QLTTTA_API

## Mục tiêu
- Người dùng đăng nhập bằng tài khoản ứng dụng (web/mobile) nhưng không thể dùng tài khoản đó để kết nối trực tiếp Oracle.
- Trên đường truyền, không lộ user/password.

## Giải pháp
- Dùng Proxy Authentication: tài khoản `APP_PROXY` của ứng dụng kết nối đến Oracle và đại diện (proxy) cho user đích.
- Chặn đăng nhập trực tiếp của user đích bằng `PROXY ONLY CONNECT`.
- Mã hóa đường truyền DB bằng NNE hoặc TCPS.
- Lưu mật khẩu ứng dụng dạng hash mạnh (Argon2/bcrypt), không lưu plaintext.

## Áp dụng trong code
- `OracleProxyConnectionProvider` mở kết nối bằng `APP_PROXY` và `ProxyUserId = <TEN_DANG_NHAP>`.
- Bật qua cấu hình `Oracle:ConnectionMode = "Proxy"`.
- Cấu hình thêm: `Oracle:ProxyUser`, `Oracle:ProxyPassword`.

## Cấu hình DB
- Xem `DB/proxy_auth_setup.sql` để tạo `APP_PROXY` và phân quyền proxy.
- Xem `ops/oracle-network-security/README.md` để bật mã hóa kênh.

## Di trú mật khẩu
- Xem `DB/security_migration_passwords.md`.
