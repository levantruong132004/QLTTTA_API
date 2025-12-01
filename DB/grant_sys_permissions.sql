-- Script cấp quyền hệ thống cho QLTT_ADMIN
-- QUAN TRỌNG: BẮT BUỘC CHẠY BẰNG TÀI KHOẢN SYS (Connect as SYSDBA)
-- Nếu bạn chạy bằng SYSTEM mà bị lỗi ORA-00942, hãy thử chạy bằng SYS.

-- Cách 1: Cấp quyền cụ thể (Khuyên dùng, cần chạy bằng SYS)
GRANT SELECT ON V_$SESSION TO QLTT_ADMIN;
GRANT ALTER SYSTEM TO QLTT_ADMIN;

-- Cách 2: Nếu Cách 1 vẫn lỗi (do synonym), hãy thử cấp quyền trên view gốc:
-- GRANT SELECT ON SYS.V_$SESSION TO QLTT_ADMIN;

-- Cách 3: Nếu vẫn không được, dùng quyền mạnh hơn (SELECT ANY DICTIONARY):
-- GRANT SELECT ANY DICTIONARY TO QLTT_ADMIN;

-- Kiểm tra quyền sau khi cấp:
-- SELECT * FROM DBA_TAB_PRIVS WHERE GRANTEE = 'QLTT_ADMIN' AND TABLE_NAME LIKE '%SESSION%';
