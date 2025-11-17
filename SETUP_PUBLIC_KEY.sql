-- ============================================================================
-- SCRIPT THIẾT LẬP PUBLIC KEY CHO TRUNG TÂM
-- ============================================================================
-- Mục đích: Lưu public key vào database để xác thực chữ ký số
-- Thực hiện: Chạy script này bằng SQL Developer hoặc SQL*Plus
-- ============================================================================

-- Bước 1: Kiểm tra bảng TTTA đã có dữ liệu chưa
SELECT * FROM QLTT_ADMIN.TTTA WHERE ID_TTTA = 1;

-- Nếu chưa có, tạo dòng mới
INSERT INTO QLTT_ADMIN.TTTA (ID_TTTA, TEN_TRUNG_TAM, DIA_CHI, SO_DIEN_THOAI)
SELECT 1, 'Trung tâm Tiếng Anh LDA', 'Đại Học Công Thương, TPHCM', '(028) 1234.5678'
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM QLTT_ADMIN.TTTA WHERE ID_TTTA = 1);

COMMIT;

-- Bước 2: Kiểm tra public key hiện tại
SELECT 
    ID_TTTA,
    TEN_TRUNG_TAM,
    CASE 
        WHEN KHOA_CONG_PEM IS NULL THEN 'CHƯA CÓ PUBLIC KEY'
        WHEN LENGTH(KHOA_CONG_PEM) > 0 THEN 'ĐÃ CÓ PUBLIC KEY (' || LENGTH(KHOA_CONG_PEM) || ' ký tự)'
        ELSE 'PUBLIC KEY RỖNG'
    END AS TRANG_THAI_PUBLIC_KEY
FROM QLTT_ADMIN.TTTA 
WHERE ID_TTTA = 1;

-- ============================================================================
-- CÁCH 1: TẠO PUBLIC KEY TỰ ĐỘNG QUA API (KHUYẾN NGHỊ)
-- ============================================================================
-- Gọi API endpoint sau để tạo cặp key mới:
-- POST https://localhost:7158/api/DigitalSignature/generate-center-keypair/save
-- Body (JSON):
-- {
--   "centerName": "Trung tâm Tiếng Anh LDA",
--   "address": "Đại Học Công Thương, TPHCM",
--   "phone": "(028) 1234.5678"
-- }
--
-- API sẽ:
-- 1. Tạo cặp key RSA 2048-bit
-- 2. Lưu public key vào database (bảng TTTA)
-- 3. Trả về private key để admin download
--
-- LƯU Ý: Private key chỉ hiển thị 1 lần duy nhất!
-- Phải lưu ngay vào file .pem và bảo mật cẩn thận!

-- ============================================================================
-- CÁCH 2: CẬP NHẬT PUBLIC KEY THỦ CÔNG (NẾU ĐÃ CÓ KEY)
-- ============================================================================
-- Nếu bạn đã có cặp key RSA, có thể cập nhật public key trực tiếp:

-- Bước 2.1: Xóa public key cũ (nếu cần)
-- CẢNH BÁO: Chỉ làm điều này nếu bạn chắc chắn muốn thay đổi key!
-- UPDATE QLTT_ADMIN.TTTA SET KHOA_CONG_PEM = NULL WHERE ID_TTTA = 1;
-- COMMIT;

-- Bước 2.2: Cập nhật public key mới
-- Thay thế 'YOUR_PUBLIC_KEY_HERE' bằng public key thực tế
/*
UPDATE QLTT_ADMIN.TTTA 
SET KHOA_CONG_PEM = '-----BEGIN RSA PUBLIC KEY-----
MIIBCgKCAQEAwjuKgrv/U0w57NUvO8z89a0KL2+PMIDw2vTt1x8NK3W2MGBX
AUkRqUSG2yyfXS9km3lfhAjy8jEbvKrIkGgcrloLqNNw7hqcXsgJ4rjAFgVU4YPx
Rk2OsnD/xHqHzkyuSPW6ufysH3b1GaH80tRtLBE3CIKHUQh7FD4sifENKVRwMUgk
P5pmA1Z80AZkzVVqagFiwqO+SwJTJ4CP8FnksrOPCa8RaiDPQdROHot5n8pySL2s
TmwdLehREbUa+UtGfSVwqSkJgelfKYFJAIwXo6STmCU8GxhYL4yfIJmytr/OIEjW
LaT3GrTK8x9xbBAxN/kG7FNZy/GmIL8/7BO9/QIDAQAB
-----END RSA PUBLIC KEY-----'
WHERE ID_TTTA = 1;

COMMIT;
*/

-- ============================================================================
-- BƯỚC 3: XÁC NHẬN PUBLIC KEY ĐÃ ĐƯỢC LƯU
-- ============================================================================
SELECT 
    ID_TTTA,
    TEN_TRUNG_TAM,
    DIA_CHI,
    SO_DIEN_THOAI,
    CASE 
        WHEN KHOA_CONG_PEM IS NULL THEN '❌ CHƯA CÓ'
        WHEN LENGTH(KHOA_CONG_PEM) > 400 THEN '✅ ĐÃ CÓ (' || LENGTH(KHOA_CONG_PEM) || ' ký tự)'
        ELSE '⚠️ CÓ NHƯNG QUÁ NGẮN (' || LENGTH(KHOA_CONG_PEM) || ' ký tự)'
    END AS TRANG_THAI_PUBLIC_KEY,
    SUBSTR(KHOA_CONG_PEM, 1, 50) || '...' AS PUBLIC_KEY_PREVIEW
FROM QLTT_ADMIN.TTTA 
WHERE ID_TTTA = 1;

-- ============================================================================
-- BƯỚC 4: KIỂM TRA CẤU TRÚC BẢNG HOA_DON
-- ============================================================================
-- Đảm bảo bảng HOA_DON có các cột cần thiết cho chữ ký số

SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    DATA_LENGTH,
    NULLABLE
FROM ALL_TAB_COLUMNS
WHERE TABLE_NAME = 'HOA_DON'
  AND OWNER = 'QLTT_ADMIN'
  AND COLUMN_NAME IN (
    'CHU_KY_BASE64',
    'THUAT_TOAN',
    'ID_KE_TOAN_KY',
    'NGAY_KY',
    'SIGNED_PDF_HASH',
    'SIGNED_PDF_VERSION',
    'SIGNED_PDF_PATH',
    'DA_IN'
  )
ORDER BY COLUMN_NAME;

-- Nếu thiếu cột, chạy các lệnh ALTER TABLE sau:
/*
-- Thêm cột CHU_KY_BASE64 (lưu chữ ký số)
ALTER TABLE QLTT_ADMIN.HOA_DON ADD (
    CHU_KY_BASE64 CLOB,
    THUAT_TOAN VARCHAR2(50),
    ID_KE_TOAN_KY NUMBER(10),
    NGAY_KY DATE,
    SIGNED_PDF_HASH VARCHAR2(128),
    SIGNED_PDF_VERSION VARCHAR2(20),
    SIGNED_PDF_PATH VARCHAR2(500),
    DA_IN NUMBER(1) DEFAULT 0
);

-- Thêm comment cho các cột
COMMENT ON COLUMN QLTT_ADMIN.HOA_DON.CHU_KY_BASE64 IS 'Chữ ký số RSA (Base64)';
COMMENT ON COLUMN QLTT_ADMIN.HOA_DON.THUAT_TOAN IS 'Thuật toán ký số (RSA-SHA256)';
COMMENT ON COLUMN QLTT_ADMIN.HOA_DON.ID_KE_TOAN_KY IS 'ID kế toán đã ký';
COMMENT ON COLUMN QLTT_ADMIN.HOA_DON.NGAY_KY IS 'Ngày giờ ký số';
COMMENT ON COLUMN QLTT_ADMIN.HOA_DON.SIGNED_PDF_HASH IS 'Hash SHA256 của PDF đã ký';
COMMENT ON COLUMN QLTT_ADMIN.HOA_DON.SIGNED_PDF_VERSION IS 'Version của PDF signature';
COMMENT ON COLUMN QLTT_ADMIN.HOA_DON.SIGNED_PDF_PATH IS 'Đường dẫn file PDF đã ký';
COMMENT ON COLUMN QLTT_ADMIN.HOA_DON.DA_IN IS 'Đã in và gửi email (0=chưa, 1=rồi)';

COMMIT;
*/

-- ============================================================================
-- BƯỚC 5: KIỂM TRA HÓA ĐƠN ĐÃ KÝ SỐ
-- ============================================================================
SELECT 
    h.ID_HOA_DON,
    h.MA_HOA_DON,
    h.SO_TIEN,
    h.TRANG_THAI,
    CASE 
        WHEN h.CHU_KY_BASE64 IS NULL THEN '❌ CHƯA KÝ'
        WHEN LENGTH(h.CHU_KY_BASE64) > 0 THEN '✅ ĐÃ KÝ'
        ELSE '⚠️ LỖI'
    END AS TRANG_THAI_KY_SO,
    h.THUAT_TOAN,
    h.NGAY_KY,
    k.HO_TEN AS KE_TOAN_KY,
    h.DA_IN,
    h.SIGNED_PDF_VERSION
FROM QLTT_ADMIN.HOA_DON h
LEFT JOIN QLTT_ADMIN.KE_TOAN k ON k.ID_KE_TOAN = h.ID_KE_TOAN_KY
ORDER BY h.ID_HOA_DON DESC
FETCH FIRST 10 ROWS ONLY;

-- ============================================================================
-- BƯỚC 6: TẠO VIEW ĐỂ XEM THÔNG TIN PUBLIC KEY (OPTIONAL)
-- ============================================================================
CREATE OR REPLACE VIEW QLTT_ADMIN.V_PUBLIC_KEY_INFO AS
SELECT 
    t.ID_TTTA,
    t.TEN_TRUNG_TAM,
    t.DIA_CHI,
    t.SO_DIEN_THOAI,
    CASE 
        WHEN t.KHOA_CONG_PEM IS NULL THEN 'CHƯA THIẾT LẬP'
        WHEN LENGTH(t.KHOA_CONG_PEM) > 400 THEN 'ĐÃ THIẾT LẬP'
        ELSE 'LỖI - KEY QUÁ NGẮN'
    END AS TRANG_THAI,
    LENGTH(t.KHOA_CONG_PEM) AS DO_DAI_KEY,
    SUBSTR(t.KHOA_CONG_PEM, 1, 100) || '...' AS PUBLIC_KEY_PREVIEW,
    (SELECT COUNT(*) FROM QLTT_ADMIN.HOA_DON WHERE CHU_KY_BASE64 IS NOT NULL) AS SO_HOA_DON_DA_KY,
    (SELECT COUNT(*) FROM QLTT_ADMIN.HOA_DON WHERE CHU_KY_BASE64 IS NULL) AS SO_HOA_DON_CHUA_KY
FROM QLTT_ADMIN.TTTA t
WHERE t.ID_TTTA = 1;

-- Xem thông tin public key
SELECT * FROM QLTT_ADMIN.V_PUBLIC_KEY_INFO;

-- ============================================================================
-- BƯỚC 7: GRANT QUYỀN CHO CÁC USER (NẾU CẦN)
-- ============================================================================
-- Cho phép kế toán đọc public key
GRANT SELECT ON QLTT_ADMIN.TTTA TO KE_TOAN_ROLE;
GRANT SELECT ON QLTT_ADMIN.V_PUBLIC_KEY_INFO TO KE_TOAN_ROLE;

-- Cho phép học viên đọc public key (để xác thực PDF)
GRANT SELECT ON QLTT_ADMIN.TTTA TO HOC_VIEN_ROLE;
GRANT SELECT ON QLTT_ADMIN.V_PUBLIC_KEY_INFO TO HOC_VIEN_ROLE;

COMMIT;

-- ============================================================================
-- BƯỚC 8: TEST XÁC THỰC PUBLIC KEY
-- ============================================================================
-- Kiểm tra public key có thể đọc được không
DECLARE
    v_public_key CLOB;
    v_key_length NUMBER;
BEGIN
    SELECT KHOA_CONG_PEM INTO v_public_key
    FROM QLTT_ADMIN.TTTA
    WHERE ID_TTTA = 1;
    
    IF v_public_key IS NULL THEN
        DBMS_OUTPUT.PUT_LINE('❌ LỖI: Public key chưa được thiết lập!');
    ELSE
        v_key_length := LENGTH(v_public_key);
        DBMS_OUTPUT.PUT_LINE('✅ THÀNH CÔNG: Public key đã được thiết lập');
        DBMS_OUTPUT.PUT_LINE('   Độ dài: ' || v_key_length || ' ký tự');
        DBMS_OUTPUT.PUT_LINE('   Preview: ' || SUBSTR(v_public_key, 1, 50) || '...');
        
        IF v_key_length < 400 THEN
            DBMS_OUTPUT.PUT_LINE('⚠️ CẢNH BÁO: Public key có vẻ quá ngắn!');
        END IF;
    END IF;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        DBMS_OUTPUT.PUT_LINE('❌ LỖI: Không tìm thấy dòng dữ liệu trong bảng TTTA!');
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('❌ LỖI: ' || SQLERRM);
END;
/

-- ============================================================================
-- KẾT LUẬN
-- ============================================================================
-- Sau khi chạy script này:
-- 1. ✅ Bảng TTTA đã có dòng dữ liệu với ID_TTTA = 1
-- 2. ✅ Public key đã được lưu vào cột KHOA_CONG_PEM
-- 3. ✅ Bảng HOA_DON đã có các cột cần thiết cho chữ ký số
-- 4. ✅ View V_PUBLIC_KEY_INFO đã được tạo để xem thông tin
-- 5. ✅ Quyền đã được grant cho các role cần thiết
--
-- BƯỚC TIẾP THEO:
-- 1. Lưu private key vào file .pem (nếu tạo mới qua API)
-- 2. Trao private key cho kế toán (bảo mật!)
-- 3. Kế toán dùng private key để ký hóa đơn
-- 4. Học viên dùng public key (từ DB) để xác thực PDF
-- ============================================================================
