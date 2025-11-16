-- Script xóa hết tất cả hóa đơn và dữ liệu liên quan
-- Chạy script này để reset dữ liệu hóa đơn về trạng thái ban đầu

-- Bật output để xem kết quả
SET SERVEROUTPUT ON;

-- Kiểm tra dữ liệu trước khi xóa
SELECT 'TRƯỚC KHI XÓA:' as THONG_TIN FROM DUAL;

SELECT 'Số lượng hóa đơn:' as LOAI, COUNT(*) as SO_LUONG FROM HOA_DON
UNION ALL
SELECT 'Số lượng thanh toán:' as LOAI, COUNT(*) as SO_LUONG FROM PHIEU_THANH_TOAN;

-- Backup dữ liệu trước khi xóa (tùy chọn)
-- CREATE TABLE HOA_DON_BACKUP AS SELECT * FROM HOA_DON;
-- CREATE TABLE PHIEU_THANH_TOAN_BACKUP AS SELECT * FROM PHIEU_THANH_TOAN;

DECLARE
    v_count_invoices NUMBER;
    v_count_payments NUMBER;
BEGIN
    -- Đếm số lượng dữ liệu hiện tại
    SELECT COUNT(*) INTO v_count_invoices FROM HOA_DON;
    SELECT COUNT(*) INTO v_count_payments FROM PHIEU_THANH_TOAN;
    
    DBMS_OUTPUT.PUT_LINE('=== BẮT ĐẦU XÓA DỮ LIỆU ===');
    DBMS_OUTPUT.PUT_LINE('Số hóa đơn cần xóa: ' || v_count_invoices);
    DBMS_OUTPUT.PUT_LINE('Số phiếu thanh toán cần xóa: ' || v_count_payments);
    
    -- Bước 1: Xóa tất cả phiếu thanh toán trước (vì có foreign key đến HOA_DON)
    IF v_count_payments > 0 THEN
        DELETE FROM PHIEU_THANH_TOAN;
        DBMS_OUTPUT.PUT_LINE('✅ Đã xóa ' || v_count_payments || ' phiếu thanh toán');
    ELSE
        DBMS_OUTPUT.PUT_LINE('ℹ️ Không có phiếu thanh toán nào để xóa');
    END IF;
    
    -- Bước 2: Xóa tất cả hóa đơn
    IF v_count_invoices > 0 THEN
        DELETE FROM HOA_DON;
        DBMS_OUTPUT.PUT_LINE('✅ Đã xóa ' || v_count_invoices || ' hóa đơn');
    ELSE
        DBMS_OUTPUT.PUT_LINE('ℹ️ Không có hóa đơn nào để xóa');
    END IF;
    
    -- Reset sequence (nếu có)
    -- Lưu ý: Thay đổi tên sequence nếu khác
    -- EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_HOA_DON';
    -- EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_HOA_DON START WITH 1 INCREMENT BY 1';
    
    COMMIT;
    
    DBMS_OUTPUT.PUT_LINE('=== HOÀN THÀNH XÓA DỮ LIỆU ===');
    
    -- Kiểm tra kết quả sau khi xóa
    SELECT COUNT(*) INTO v_count_invoices FROM HOA_DON;
    SELECT COUNT(*) INTO v_count_payments FROM PHIEU_THANH_TOAN;
    
    DBMS_OUTPUT.PUT_LINE('Số hóa đơn còn lại: ' || v_count_invoices);
    DBMS_OUTPUT.PUT_LINE('Số phiếu thanh toán còn lại: ' || v_count_payments);
    
    IF v_count_invoices = 0 AND v_count_payments = 0 THEN
        DBMS_OUTPUT.PUT_LINE('🎉 XÓA THÀNH CÔNG! Tất cả hóa đơn đã được xóa');
    ELSE
        DBMS_OUTPUT.PUT_LINE('⚠️ Vẫn còn dữ liệu chưa được xóa');
    END IF;
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('❌ LỖI: ' || SQLERRM);
        ROLLBACK;
        RAISE;
END;
/

-- Kiểm tra dữ liệu sau khi xóa
SELECT 'SAU KHI XÓA:' as THONG_TIN FROM DUAL;

SELECT 'Số lượng hóa đơn:' as LOAI, COUNT(*) as SO_LUONG FROM HOA_DON
UNION ALL
SELECT 'Số lượng thanh toán:' as LOAI, COUNT(*) as SO_LUONG FROM PHIEU_THANH_TOAN;

-- Hiển thị các đơn đăng ký có thể tạo hóa đơn mới
SELECT 'CÁC ĐỚN CÓ THỂ TẠO HÓA ĐƠN MỚI:' as THONG_TIN FROM DUAL;

SELECT dk.ID_DANG_KY, dk.MA_DANG_KY, dk.TRANG_THAI, 
       hv.HO_TEN as TEN_HOC_VIEN,
       lh.TEN_LOP, kh.TEN_KHOA_HOC
FROM DON_DANG_KY dk
JOIN HOC_VIEN hv ON dk.ID_HOC_VIEN = hv.ID_HOC_VIEN
JOIN LOP_HOC lh ON dk.ID_LOP = lh.ID_LOP
JOIN KHOA_HOC kh ON lh.ID_KHOA_HOC = kh.ID_KHOA_HOC
WHERE dk.TRANG_THAI = 'Đã duyệt'
ORDER BY dk.NGAY_DANG_KY DESC;

