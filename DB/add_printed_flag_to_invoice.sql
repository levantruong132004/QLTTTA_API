-- Kiểm tra cấu trúc bảng HOA_DON trước khi thêm cột DA_IN
-- Script này sẽ hiển thị tất cả các cột hiện có

-- Bước 1: Xem cấu trúc bảng HOA_DON
SELECT COLUMN_NAME, DATA_TYPE, NULLABLE, DATA_DEFAULT
FROM ALL_TAB_COLUMNS 
WHERE TABLE_NAME = 'HOA_DON' AND OWNER = 'QLTT_ADMIN'
ORDER BY COLUMN_ID;

-- Bước 2: Kiểm tra xem cột DA_IN đã tồn tại chưa
SELECT COUNT(*) as DA_IN_EXISTS
FROM ALL_TAB_COLUMNS 
WHERE TABLE_NAME = 'HOA_DON' 
  AND OWNER = 'QLTT_ADMIN' 
  AND COLUMN_NAME = 'DA_IN';

-- Bước 3: Thêm cột DA_IN nếu chưa tồn tại
DECLARE
  column_exists NUMBER := 0;
BEGIN
  -- Kiểm tra cột DA_IN đã có chưa
  SELECT COUNT(*)
  INTO column_exists
  FROM ALL_TAB_COLUMNS 
  WHERE TABLE_NAME = 'HOA_DON' 
    AND OWNER = 'QLTT_ADMIN' 
    AND COLUMN_NAME = 'DA_IN';
  
  -- Nếu chưa có thì thêm vào
  IF column_exists = 0 THEN
    EXECUTE IMMEDIATE 'ALTER TABLE QLTT_ADMIN.HOA_DON ADD DA_IN NUMBER(1) DEFAULT 0 NOT NULL';
    DBMS_OUTPUT.PUT_LINE('✅ Đã thêm cột DA_IN vào bảng HOA_DON');
    
    EXECUTE IMMEDIATE 'COMMENT ON COLUMN QLTT_ADMIN.HOA_DON.DA_IN IS ''Đã in hóa đơn: 0 = Chưa in, 1 = Đã in''';
    DBMS_OUTPUT.PUT_LINE('✅ Đã thêm comment cho cột DA_IN');
    
    -- Cập nhật các hóa đơn hiện có đã có chữ ký thành đã in
    UPDATE QLTT_ADMIN.HOA_DON 
    SET DA_IN = 1 
    WHERE CHU_KY_BASE64 IS NOT NULL;
    
    DBMS_OUTPUT.PUT_LINE('✅ Đã cập nhật ' || SQL%ROWCOUNT || ' hóa đơn có chữ ký thành trạng thái đã in');
    
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('✅ Hoàn thành thêm cột DA_IN!');
  ELSE
    DBMS_OUTPUT.PUT_LINE('ℹ️ Cột DA_IN đã tồn tại trong bảng HOA_DON');
  END IF;
EXCEPTION
  WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('❌ Lỗi: ' || SQLERRM);
    ROLLBACK;
END;
/

-- Bước 4: Xem lại cấu trúc sau khi thêm cột
SELECT COLUMN_NAME, DATA_TYPE, NULLABLE, DATA_DEFAULT
FROM ALL_TAB_COLUMNS 
WHERE TABLE_NAME = 'HOA_DON' AND OWNER = 'QLTT_ADMIN'
  AND COLUMN_NAME IN ('DA_IN', 'CHU_KY_BASE64', 'NGAY_TAO', 'HAN_THANH_TOAN', 'NGAY_HET_HAN')
ORDER BY COLUMN_ID;
