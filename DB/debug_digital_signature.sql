-- Script kiểm tra và debug vấn đề chữ ký số
-- Chạy script này để tìm ra nguyên nhân lỗi

-- 1. Kiểm tra ID thực tế của user minhtien
SELECT 'ID thực tế của minhtien:' as INFO, ID_NGUOI_DUNG as ID_KE_TOAN
FROM TAI_KHOAN 
WHERE TEN_DANG_NHAP = 'minhtien';

-- 2. Kiểm tra dữ liệu trong bảng KE_TOAN
SELECT 'Dữ liệu KE_TOAN:' as INFO, 
       ID_KE_TOAN, HO_TEN, MA_NHAN_VIEN,
       CASE 
           WHEN KHOA_CONG_PEM IS NULL THEN 'CHƯA CÓ PUBLIC KEY'
           WHEN LENGTH(KHOA_CONG_PEM) < 100 THEN 'PUBLIC KEY QUÁ NGẮN'
           ELSE 'CÓ PUBLIC KEY (' || LENGTH(KHOA_CONG_PEM) || ' ký tự)'
       END as TRANG_THAI_KHOA
FROM KE_TOAN;

-- 3. Kiểm tra kết nối giữa TAI_KHOAN và KE_TOAN
SELECT 'Kết nối TAI_KHOAN <-> KE_TOAN:' as INFO,
       tk.TEN_DANG_NHAP, tk.ID_NGUOI_DUNG as TAI_KHOAN_ID, 
       kt.ID_KE_TOAN, kt.HO_TEN
FROM TAI_KHOAN tk
LEFT JOIN KE_TOAN kt ON tk.ID_NGUOI_DUNG = kt.ID_KE_TOAN
WHERE tk.TEN_DANG_NHAP = 'minhtien';

-- 4. Hiển thị public key của minhtien (50 ký tự đầu)
SELECT 'Public Key của minhtien:' as INFO,
       SUBSTR(kt.KHOA_CONG_PEM, 1, 50) || '...' as PUBLIC_KEY_PREVIEW
FROM KE_TOAN kt
JOIN TAI_KHOAN tk ON kt.ID_KE_TOAN = tk.ID_NGUOI_DUNG
WHERE tk.TEN_DANG_NHAP = 'minhtien'
AND kt.KHOA_CONG_PEM IS NOT NULL;

-- 5. Test query giống như trong DigitalSignatureService
DECLARE
    v_accountant_id NUMBER;
    v_public_key CLOB;
BEGIN
    -- Lấy ID kế toán của minhtien
    SELECT ID_NGUOI_DUNG INTO v_accountant_id
    FROM TAI_KHOAN 
    WHERE TEN_DANG_NHAP = 'minhtien';
    
    DBMS_OUTPUT.PUT_LINE('AccountantId của minhtien: ' || v_accountant_id);
    
    -- Test query trong GetAccountantPublicKeyAsync
    SELECT KHOA_CONG_PEM INTO v_public_key
    FROM KE_TOAN 
    WHERE ID_KE_TOAN = v_accountant_id;
    
    IF v_public_key IS NULL THEN
        DBMS_OUTPUT.PUT_LINE('❌ KHOA_CONG_PEM là NULL');
    ELSIF LENGTH(v_public_key) = 0 THEN
        DBMS_OUTPUT.PUT_LINE('❌ KHOA_CONG_PEM là chuỗi rỗng');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✅ Có public key, độ dài: ' || LENGTH(v_public_key) || ' ký tự');
        DBMS_OUTPUT.PUT_LINE('Preview: ' || SUBSTR(v_public_key, 1, 50) || '...');
    END IF;
    
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        DBMS_OUTPUT.PUT_LINE('❌ Không tìm thấy dữ liệu cho AccountantId: ' || v_accountant_id);
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('❌ Lỗi: ' || SQLERRM);
END;
/

-- 6. Khuyến nghị sửa lỗi
SELECT '=== KHUYẾN NGHỊ SỬA LỖI ===' as INFO FROM DUAL
UNION ALL
SELECT '1. Sửa AccountantId trong web thành ID thực tế của minhtien' FROM DUAL
UNION ALL
SELECT '2. Đảm bảo public key không chứa ký tự đặc biệt' FROM DUAL
UNION ALL  
SELECT '3. Kiểm tra connection string và quyền truy cập' FROM DUAL;