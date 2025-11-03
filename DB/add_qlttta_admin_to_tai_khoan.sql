DECLARE
  v_username       VARCHAR2(50) := 'QLTTTA_ADMIN';
  v_password_plain VARCHAR2(128) := '123456';
  v_email          VARCHAR2(100) := 'update_later_' || SUBSTR(SYS_GUID(), 1, 8) || '@lda.local';
  v_cnt            NUMBER;
BEGIN
  -- Kiểm tra xem tài khoản đã tồn tại chưa
  SELECT COUNT(*) INTO v_cnt 
  FROM TAI_KHOAN 
  WHERE UPPER(TEN_DANG_NHAP) = UPPER(v_username);

  IF v_cnt > 0 THEN
    -- Nếu có rồi thì cập nhật mật khẩu và vai trò
    UPDATE TAI_KHOAN
       SET MAT_KHAU = v_password_plain,
           TRANG_THAI_KICH_HOAT = 1,
           ID_VAI_TRO = 5
     WHERE UPPER(TEN_DANG_NHAP) = UPPER(v_username);
    DBMS_OUTPUT.PUT_LINE('🔁 Đã cập nhật tài khoản: ' || v_username);
  ELSE
    -- Nếu chưa có thì thêm mới
    INSERT INTO TAI_KHOAN (TEN_DANG_NHAP, MAT_KHAU, EMAIL, TRANG_THAI_KICH_HOAT, ID_VAI_TRO)
    VALUES (v_username, v_password_plain, v_email, 1, 5);
    DBMS_OUTPUT.PUT_LINE('✅ Đã thêm tài khoản mới: ' || v_username || ' (vai trò: QUANTRIVIENHETHONG)');
  END IF;
END;
/
COMMIT;


SELECT TEN_DANG_NHAP, MAT_KHAU, EMAIL, ID_VAI_TRO, TRANG_THAI_KICH_HOAT
FROM TAI_KHOAN
WHERE TEN_DANG_NHAP = 'QLTTTA_ADMIN';


CREATE USER QLTTTA_ADMIN IDENTIFIED BY 123456;
GRANT CONNECT, RESOURCE TO QLTTTA_ADMIN;
ALTER USER QLTTTA_ADMIN QUOTA UNLIMITED ON USERS;
commit
DELETE FROM TAI_KHOAN 
WHERE TEN_DANG_NHAP = 'QLTTTA_ADMIN';
COMMIT;


