-- Script cập nhật database cho chức năng chữ ký số
-- Chạy script này để thêm các cột cần thiết cho bảng HOA_DON và KE_TOAN

-- Thêm cột chữ ký số cho bảng HOA_DON
ALTER TABLE HOA_DON ADD (
    CHU_KY_BASE64 CLOB NULL,           -- Chữ ký số dạng Base64
    THUAT_TOAN VARCHAR2(50) NULL,      -- Thuật toán ký (RSA-SHA256)
    ID_KE_TOAN_KY NUMBER(10) NULL,     -- ID kế toán đã ký
    NGAY_KY DATE NULL                  -- Ngày ký số
);

-- Thêm comment cho các cột mới
COMMENT ON COLUMN HOA_DON.CHU_KY_BASE64 IS 'Chữ ký số của hóa đơn dạng Base64';
COMMENT ON COLUMN HOA_DON.THUAT_TOAN IS 'Thuật toán mã hóa sử dụng (VD: RSA-SHA256)';
COMMENT ON COLUMN HOA_DON.ID_KE_TOAN_KY IS 'ID kế toán đã ký hóa đơn';
COMMENT ON COLUMN HOA_DON.NGAY_KY IS 'Ngày thời gian ký số hóa đơn';

-- Kiểm tra và thêm cột public key cho bảng KE_TOAN nếu chưa có
-- (Từ code hiện tại có vẻ như đã có cột KHOA_CONG_PEM)
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE KE_TOAN ADD KHOA_CONG_PEM CLOB NULL';
    DBMS_OUTPUT.PUT_LINE('Đã thêm cột KHOA_CONG_PEM vào bảng KE_TOAN');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1430 THEN -- Column already exists
            DBMS_OUTPUT.PUT_LINE('Cột KHOA_CONG_PEM đã tồn tại trong bảng KE_TOAN');
        ELSE
            RAISE;
        END IF;
END;
/

-- Thêm comment cho cột public key
COMMENT ON COLUMN KE_TOAN.KHOA_CONG_PEM IS 'Public key RSA của kế toán dạng PEM';

-- Tạo index cho hiệu suất
CREATE INDEX IDX_HOA_DON_KE_TOAN_KY ON HOA_DON(ID_KE_TOAN_KY);
CREATE INDEX IDX_HOA_DON_NGAY_KY ON HOA_DON(NGAY_KY);

-- Thêm foreign key constraint (tùy chọn)
ALTER TABLE HOA_DON ADD CONSTRAINT FK_HOA_DON_KE_TOAN_KY 
    FOREIGN KEY (ID_KE_TOAN_KY) REFERENCES KE_TOAN(ID_KE_TOAN);

-- Sample data để test (tùy chọn)
-- Cập nhật một kế toán với sample public key để test
/*
UPDATE KE_TOAN 
SET KHOA_CONG_PEM = '-----BEGIN RSA PUBLIC KEY-----
MIIBCgKCAQEA7+7wX9V1rQx...sample_public_key...
-----END RSA PUBLIC KEY-----'
WHERE ID_KE_TOAN = 1;
*/

COMMIT;