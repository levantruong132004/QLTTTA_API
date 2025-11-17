-- ============================================================================
-- Fix VPD Policy và thêm tài khoản QLTT_ADMIN để VPD hoạt động đúng
-- ============================================================================
SET SERVEROUTPUT ON;

-- 1. Thêm tài khoản QLTT_ADMIN vào TAI_KHOAN nếu chưa có
DECLARE
  v_username       NVARCHAR2(50) := 'QLTT_ADMIN';  -- Đổi tên cho đúng với SESSION_USER
  v_password_plain NVARCHAR2(128) := '123456';
  v_email          VARCHAR2(100) := 'admin@lda.local';
  v_cnt            NUMBER;
  v_admin_role_id  NUMBER;
BEGIN
  -- Lấy ID vai trò Admin (QuanTriVienHeThong)
  BEGIN
    SELECT ID_VAI_TRO INTO v_admin_role_id
    FROM VAI_TRO
    WHERE TEN_VAI_TRO = N'QuanTriVienHeThong' OR ID_VAI_TRO = 5
    FETCH FIRST 1 ROWS ONLY;
  EXCEPTION
    WHEN NO_DATA_FOUND THEN
      v_admin_role_id := 5; -- Fallback
  END;

  -- Kiểm tra tài khoản đã tồn tại chưa
  SELECT COUNT(*) INTO v_cnt 
  FROM TAI_KHOAN 
  WHERE UPPER(TEN_DANG_NHAP) = UPPER(v_username);

  IF v_cnt > 0 THEN
    -- Nếu có rồi thì cập nhật
    UPDATE TAI_KHOAN
       SET MAT_KHAU = v_password_plain,
           TRANG_THAI_KICH_HOAT = 1,
           ID_VAI_TRO = v_admin_role_id
     WHERE UPPER(TEN_DANG_NHAP) = UPPER(v_username);
    DBMS_OUTPUT.PUT_LINE('✅ Đã cập nhật tài khoản: ' || v_username);
  ELSE
    -- Thêm mới với ID_NGUOI_DUNG cụ thể (999999) để không conflict với học viên
    INSERT INTO TAI_KHOAN (ID_NGUOI_DUNG, TEN_DANG_NHAP, MAT_KHAU, EMAIL, TRANG_THAI_KICH_HOAT, ID_VAI_TRO)
    VALUES (999999, v_username, v_password_plain, v_email, 1, v_admin_role_id);
    DBMS_OUTPUT.PUT_LINE('✅ Đã thêm tài khoản mới: ' || v_username || ' (ID: 999999, vai trò: Admin)');
  END IF;
  
  COMMIT;
END;
/

-- 2. Kiểm tra tài khoản vừa tạo
PROMPT 
PROMPT === Kiểm tra tài khoản QLTT_ADMIN ===
SELECT ID_NGUOI_DUNG, TEN_DANG_NHAP, EMAIL, ID_VAI_TRO, TRANG_THAI_KICH_HOAT
FROM TAI_KHOAN
WHERE UPPER(TEN_DANG_NHAP) = 'QLTT_ADMIN';

-- 3. Kiểm tra session hiện tại
PROMPT 
PROMPT === Kiểm tra SESSION_USER và CLIENT_IDENTIFIER ===
SELECT SYS_CONTEXT('USERENV', 'SESSION_USER') AS SESSION_USER,
       SYS_CONTEXT('USERENV', 'CLIENT_IDENTIFIER') AS CLIENT_IDENTIFIER
FROM DUAL;

-- 4. Test VPD function với username QLTT_ADMIN
PROMPT 
PROMPT === Test VPD function (nên trả về 1=1 cho Admin) ===
SELECT QLTT_ADMIN.VPD_PRED_HOC_VIEN(USER, 'HOC_VIEN') AS VPD_PREDICATE
FROM DUAL;

-- 5. Kiểm tra các policy hiện tại (chỉ dùng các cột tồn tại)
PROMPT 
PROMPT === Danh sách VPD Policies ===
SELECT object_name, policy_name, enable
FROM USER_POLICIES 
ORDER BY object_name;

-- 6. Test query thực tế trên bảng HOC_VIEN (nên thấy tất cả rows)
PROMPT 
PROMPT === Test query HOC_VIEN (nên thấy tất cả học viên) ===
SELECT COUNT(*) AS TOTAL_HOC_VIEN
FROM HOC_VIEN;

-- 7. Hiển thị 3 học viên đầu tiên để kiểm tra
PROMPT 
PROMPT === 3 học viên đầu tiên ===
SELECT ID_HOC_VIEN, MA_HOC_VIEN, HO_TEN
FROM HOC_VIEN
WHERE ROWNUM <= 3;

-- 8. Hiển thị thông báo
BEGIN
  DBMS_OUTPUT.PUT_LINE('');
  DBMS_OUTPUT.PUT_LINE('============================================');
  DBMS_OUTPUT.PUT_LINE('✅ Hoàn tất! Bây giờ:');
  DBMS_OUTPUT.PUT_LINE('1. Tài khoản QLTT_ADMIN đã có trong TAI_KHOAN');
  DBMS_OUTPUT.PUT_LINE('2. VPD sẽ nhận dạng được SESSION_USER = QLTT_ADMIN');
  DBMS_OUTPUT.PUT_LINE('3. Admin sẽ thấy tất cả dữ liệu (1=1)');
  DBMS_OUTPUT.PUT_LINE('4. Kế toán login qua API sẽ set CLIENT_IDENTIFIER');
  DBMS_OUTPUT.PUT_LINE('============================================');
END;
/
