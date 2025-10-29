-- ====================================================================================
-- Proxy Authentication setup (Oracle 12.2+ recommended)
-- Goal: Chỉ cho phép user kết nối thông qua APP_PROXY; chặn đăng nhập trực tiếp.
-- Chạy bằng tài khoản DBA/ADMIN tương ứng (ví dụ: SYS as SYSDBA hoặc tài khoản có quyền).
-- ====================================================================================

-- 1) Tạo tài khoản proxy ứng dụng
--    Đổi mật khẩu mạnh trước khi dùng thật.
CREATE USER APP_PROXY IDENTIFIED BY "CHANGE_ME_STRONG";
GRANT CREATE SESSION TO APP_PROXY;

-- 2) Cấp quyền proxy cho các user đích
--    Nếu bạn đã có sẵn các user Oracle tương ứng với người dùng ứng dụng (ví dụ trùng với TEN_DANG_NHAP),
--    dùng ALTER USER ... GRANT CONNECT THROUGH ... và PROXY ONLY CONNECT để cấm đăng nhập trực tiếp.

-- Ví dụ đơn lẻ:
-- ALTER USER STUDENT01 GRANT CONNECT THROUGH APP_PROXY;
-- ALTER USER STUDENT01 PROXY ONLY CONNECT;

-- Nếu cần tạo user Oracle cho mỗi tài khoản ứng dụng (trường hợp chưa có):
-- (a) Tạo user với mật khẩu ngẫu nhiên mà chỉ DBA biết, không công bố cho người dùng
-- CREATE USER STUDENT01 IDENTIFIED BY "R@nd0m#2025!";
-- GRANT CREATE SESSION TO STUDENT01;
-- (b) Cho phép kết nối qua proxy và chặn đăng nhập trực tiếp
-- ALTER USER STUDENT01 GRANT CONNECT THROUGH APP_PROXY;
-- ALTER USER STUDENT01 PROXY ONLY CONNECT;

-- Tuỳ chọn: cấp các quyền/role cần thiết cho user đích (tối thiểu theo nguyên tắc Least Privilege)
-- GRANT SELECT, INSERT, UPDATE, DELETE ON YOUR_SCHEMA.YOUR_TABLE TO STUDENT01;

-- 3) Kiểm tra session proxy
-- SELECT USER, SYS_CONTEXT('USERENV','SESSION_USER') AS SESSION_USER,
--        SYS_CONTEXT('USERENV','PROXY_USER') AS PROXY_USER,
--        SYS_CONTEXT('USERENV','CLIENT_IDENTIFIER') AS CLIENT_IDENTIFIER
-- FROM DUAL;

-- 4) Thu hồi quyền proxy khi không cần nữa
-- ALTER USER STUDENT01 REVOKE CONNECT THROUGH APP_PROXY;

-- Ghi chú:
-- - PROXY ONLY CONNECT đảm bảo user không thể đăng nhập trực tiếp (password-based) mà chỉ được thông qua APP_PROXY.
-- - Nếu phiên bản Oracle không hỗ trợ PROXY ONLY CONNECT, có thể dùng giải pháp thay thế: để mật khẩu không biết, account lock + password versions,
--   hoặc network ACL chỉ cho phép truy cập từ IP của App Server; nhưng khuyến nghị dùng phiên bản hỗ trợ PROXY ONLY CONNECT.
