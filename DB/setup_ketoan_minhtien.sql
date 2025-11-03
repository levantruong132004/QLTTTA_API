-- Script bổ sung dữ liệu cho Kế toán MINHTIEN
-- Chạy script này sau khi đã chạy database.sql

-- Kiểm tra xem tài khoản minhtien đã tồn tại chưa
DECLARE
    v_user_id NUMBER;
    v_role_id NUMBER;
    v_count NUMBER;
BEGIN
    -- Lấy ID vai trò Kế toán
    SELECT ID_VAI_TRO INTO v_role_id 
    FROM VAI_TRO 
    WHERE TEN_VAI_TRO = 'KeToan';
    
    -- Kiểm tra xem tài khoản minhtien đã tồn tại chưa
    SELECT COUNT(*) INTO v_count 
    FROM TAI_KHOAN 
    WHERE TEN_DANG_NHAP = 'minhtien';
    
    IF v_count = 0 THEN
        -- Tạo tài khoản mới nếu chưa có
        INSERT INTO TAI_KHOAN (TEN_DANG_NHAP, MAT_KHAU, EMAIL, ID_VAI_TRO, TRANG_THAI_KICH_HOAT)
        VALUES ('minhtien', '$2a$11$rKf8/N7vqQf4uJqO8tR8R.8XoF3sZm9Kp1Yx7qA2B5w9.E3Vf6H8m', 'minhtien@qltt.com', v_role_id, 1)
        RETURNING ID_NGUOI_DUNG INTO v_user_id;
        
        DBMS_OUTPUT.PUT_LINE('Đã tạo tài khoản minhtien với ID: ' || v_user_id);
    ELSE
        -- Cập nhật vai trò nếu tài khoản đã tồn tại
        UPDATE TAI_KHOAN 
        SET ID_VAI_TRO = v_role_id 
        WHERE TEN_DANG_NHAP = 'minhtien'
        RETURNING ID_NGUOI_DUNG INTO v_user_id;
        
        DBMS_OUTPUT.PUT_LINE('Đã cập nhật vai trò cho tài khoản minhtien với ID: ' || v_user_id);
    END IF;
    
    -- Kiểm tra xem dữ liệu trong bảng KE_TOAN đã có chưa
    SELECT COUNT(*) INTO v_count 
    FROM KE_TOAN 
    WHERE ID_KE_TOAN = v_user_id;
    
    IF v_count = 0 THEN
        -- Thêm dữ liệu vào bảng KE_TOAN
        INSERT INTO KE_TOAN (ID_KE_TOAN, HO_TEN, MA_NHAN_VIEN, GIOI_TINH, SO_DIEN_THOAI, KHOA_CONG_PEM)
        VALUES (v_user_id, N'Nguyễn Minh Tiến', 'KT001', N'Nam', '0901234567', NULL);
        
        DBMS_OUTPUT.PUT_LINE('Đã thêm dữ liệu vào bảng KE_TOAN cho minhtien');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Dữ liệu KE_TOAN cho minhtien đã tồn tại');
    END IF;
    
    COMMIT;
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Lỗi: ' || SQLERRM);
        ROLLBACK;
END;
/

-- Thêm dữ liệu mẫu cho các kế toán khác (tùy chọn)
INSERT INTO TAI_KHOAN (TEN_DANG_NHAP, MAT_KHAU, EMAIL, ID_VAI_TRO, TRANG_THAI_KICH_HOAT)
SELECT 'ketoan01', '$2a$11$rKf8/N7vqQf4uJqO8tR8R.8XoF3sZm9Kp1Yx7qA2B5w9.E3Vf6H8m', 'ketoan01@qltt.com', 
       (SELECT ID_VAI_TRO FROM VAI_TRO WHERE TEN_VAI_TRO = 'KeToan'), 1
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM TAI_KHOAN WHERE TEN_DANG_NHAP = 'ketoan01');

INSERT INTO KE_TOAN (ID_KE_TOAN, HO_TEN, MA_NHAN_VIEN, GIOI_TINH, SO_DIEN_THOAI, KHOA_CONG_PEM)
SELECT tk.ID_NGUOI_DUNG, N'Nguyễn Thị Mai', 'KT002', N'Nữ', '0902345678', NULL
FROM TAI_KHOAN tk
WHERE tk.TEN_DANG_NHAP = 'ketoan01'
AND NOT EXISTS (SELECT 1 FROM KE_TOAN WHERE ID_KE_TOAN = tk.ID_NGUOI_DUNG);

-- Cấp quyền cho role kế toán
GRANT SELECT, INSERT, UPDATE ON QLTT_ADMIN.HOA_DON TO ROLE_KETOAN;
GRANT SELECT, INSERT, UPDATE ON QLTT_ADMIN.PHIEU_THANH_TOAN TO ROLE_KETOAN;
GRANT SELECT ON QLTT_ADMIN.DON_DANG_KY TO ROLE_KETOAN;
GRANT SELECT ON QLTT_ADMIN.HOC_VIEN TO ROLE_KETOAN;
GRANT SELECT ON QLTT_ADMIN.LOP_HOC TO ROLE_KETOAN;
GRANT SELECT ON QLTT_ADMIN.KHOA_HOC TO ROLE_KETOAN;
GRANT SELECT, UPDATE ON QLTT_ADMIN.KE_TOAN TO ROLE_KETOAN;

-- Gán role cho user minhtien
GRANT ROLE_KETOAN TO MINHTIEN;
ALTER USER MINHTIEN DEFAULT ROLE ALL;

COMMIT;

-- Kiểm tra kết quả
SELECT 'TAI_KHOAN' as BANG, COUNT(*) as SO_LUONG FROM TAI_KHOAN WHERE TEN_DANG_NHAP = 'minhtien'
UNION ALL
SELECT 'KE_TOAN' as BANG, COUNT(*) as SO_LUONG FROM KE_TOAN 
WHERE ID_KE_TOAN = (SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE TEN_DANG_NHAP = 'minhtien');

-- Hiển thị thông tin kế toán
SELECT kt.ID_KE_TOAN, kt.HO_TEN, kt.MA_NHAN_VIEN, kt.GIOI_TINH, kt.SO_DIEN_THOAI,
       CASE WHEN kt.KHOA_CONG_PEM IS NULL THEN 'Chưa có' ELSE 'Đã có' END as TRANG_THAI_KHOA
FROM KE_TOAN kt
JOIN TAI_KHOAN tk ON kt.ID_KE_TOAN = tk.ID_NGUOI_DUNG
WHERE tk.TEN_DANG_NHAP = 'minhtien';

DBMS_OUTPUT.PUT_LINE('=== HOÀN THÀNH THIẾT LẬP KẾ TOÁN ===');
DBMS_OUTPUT.PUT_LINE('Tài khoản: minhtien / Mật khẩu: 123456');
DBMS_OUTPUT.PUT_LINE('Vai trò: Kế toán');
DBMS_OUTPUT.PUT_LINE('Bước tiếp theo: Tạo cặp khóa RSA trên giao diện web');
/


-- Thêm tài khoản kế toán khác để kiểm thử (tùy chọn)
select * from tai_khoan


DECLARE
  v_username  VARCHAR2(50) := 'thuanthai';       
  v_id        NUMBER;
BEGIN
  SELECT ID_NGUOI_DUNG INTO v_id
  FROM TAI_KHOAN
  WHERE UPPER(TEN_DANG_NHAP) = UPPER(v_username);

  MERGE INTO KE_TOAN kt
  USING (SELECT v_id AS id FROM dual) s
     ON (kt.ID_KE_TOAN = s.id)
  WHEN NOT MATCHED THEN
    INSERT (ID_KE_TOAN, HO_TEN, MA_NHAN_VIEN, GIOI_TINH, SO_DIEN_THOAI, KHOA_CONG_PEM)
    VALUES (s.id, 'Phạm Thái Thuận', 'KT003', 'Nam', '0382998490', NULL)
  WHEN MATCHED THEN
    UPDATE SET
      HO_TEN        = 'Phạm Thái Thuận',
      MA_NHAN_VIEN  = 'KT003',
      GIOI_TINH     = 'Nam',
      SO_DIEN_THOAI = '0382998490';
      -- KHOA_CONG_PEM giữ nguyên

  COMMIT;
END;

select * from ke_toan

UPDATE TAI_KHOAN
SET ID_VAI_TRO = 3
WHERE TEN_DANG_NHAP = 'thuanthai';
GRANT ROLE_KETOAN TO THUANTHAI;
ALTER USER THUANTHAI DEFAULT ROLE ALL;
commit
