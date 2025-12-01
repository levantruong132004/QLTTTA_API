--------------------------------------------------------
--  File: create_stored_procedures.sql
--  Purpose: Create Stored Procedures for QLTTTA_API
--  Schema: QLTT_ADMIN
--------------------------------------------------------

-- Create Sequences if not exist
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM USER_SEQUENCES WHERE SEQUENCE_NAME = 'SEQ_NHAN_VIEN_HOC_VU';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_NHAN_VIEN_HOC_VU START WITH 1 INCREMENT BY 1 NOCACHE';
    END IF;
    
    SELECT COUNT(*) INTO v_count FROM USER_SEQUENCES WHERE SEQUENCE_NAME = 'SEQ_KE_TOAN';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_KE_TOAN START WITH 1 INCREMENT BY 1 NOCACHE';
    END IF;
END;
/

CREATE OR REPLACE PROCEDURE SP_DANG_KY_HOC_VIEN(
    p_ten_dang_nhap IN VARCHAR2,
    p_mat_khau      IN VARCHAR2,
    p_email         IN VARCHAR2,
    p_ho_ten        IN NVARCHAR2,
    p_gioi_tinh     IN NVARCHAR2,
    p_ngay_sinh     IN DATE,
    p_sdt           IN VARCHAR2,
    p_dia_chi       IN NVARCHAR2,
    p_ket_qua       OUT NVARCHAR2
)
AS
    v_user_id        NUMBER;
    v_role_id        NUMBER;
    v_sql            VARCHAR2(2000);
    v_mat_khau_hash  VARCHAR2(100);
    v_ma_hoc_vien    VARCHAR2(20);
    v_uname          VARCHAR2(128);
BEGIN
    v_uname := UPPER(TRIM(p_ten_dang_nhap));

    -- Lấy vai trò
    SELECT ID_VAI_TRO INTO v_role_id 
    FROM VAI_TRO 
    WHERE TEN_VAI_TRO = 'HocVien';

    -- Generate Code
    SELECT 'HV' || LPAD(STUDENT_CODE_SEQ.NEXTVAL, 6, '0') INTO v_ma_hoc_vien FROM DUAL;

    -- Hash Password
    v_mat_khau_hash := SIMPLE_HASH(p_mat_khau || 'SALT2025');

    -- Insert Account
    INSERT INTO TAI_KHOAN (TEN_DANG_NHAP, MAT_KHAU, EMAIL, ID_VAI_TRO)
    VALUES (v_uname, v_mat_khau_hash, p_email, v_role_id)
    RETURNING ID_NGUOI_DUNG INTO v_user_id;

    -- Insert Student
    INSERT INTO HOC_VIEN (ID_HOC_VIEN, HO_TEN, MA_HOC_VIEN, GIOI_TINH, NGAY_SINH, SO_DIEN_THOAI, DIA_CHI)
    VALUES (v_user_id, p_ho_ten, v_ma_hoc_vien, p_gioi_tinh, p_ngay_sinh, p_sdt, p_dia_chi);

    -- Create Oracle User
    v_sql := 'CREATE USER "' || v_uname || '" IDENTIFIED BY "' || p_mat_khau || '" PROFILE TTTA_USER_PROFILE';
    EXECUTE IMMEDIATE v_sql;

    -- Grants
    EXECUTE IMMEDIATE 'GRANT CREATE SESSION TO "' || v_uname || '"';
    EXECUTE IMMEDIATE 'GRANT role_hocvien TO "' || v_uname || '"';

    COMMIT;
    p_ket_qua := N'Đăng ký thành công!';
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        p_ket_qua := N'Lỗi: Tên đăng nhập hoặc email đã tồn tại.';
    WHEN OTHERS THEN
        ROLLBACK;
        p_ket_qua := N'Lỗi hệ thống: ' || SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE SP_DANG_KY_NHAN_VIEN_HOC_VU(
    p_ten_dang_nhap IN VARCHAR2,
    p_mat_khau      IN VARCHAR2,
    p_email         IN VARCHAR2,
    p_ho_ten        IN NVARCHAR2,
    p_gioi_tinh     IN NVARCHAR2,
    p_sdt           IN VARCHAR2,
    p_ket_qua       OUT NVARCHAR2
)
AS
    v_user_id        NUMBER;
    v_role_id        NUMBER;
    v_sql            VARCHAR2(2000);
    v_mat_khau_hash  VARCHAR2(200);
    v_uname          VARCHAR2(128);
    v_ma_nhan_vien   VARCHAR2(20);
BEGIN
    v_uname := UPPER(TRIM(p_ten_dang_nhap));

    SELECT ID_VAI_TRO INTO v_role_id
    FROM VAI_TRO
    WHERE TEN_VAI_TRO = 'NhanVienHocVu';

    -- Generate Code NV00001
    SELECT 'NV' || LPAD(SEQ_NHAN_VIEN_HOC_VU.NEXTVAL, 5, '0') INTO v_ma_nhan_vien FROM DUAL;

    v_mat_khau_hash := SIMPLE_HASH(p_mat_khau || 'SALT2025');

    INSERT INTO TAI_KHOAN (TEN_DANG_NHAP, MAT_KHAU, EMAIL, ID_VAI_TRO)
    VALUES (v_uname, v_mat_khau_hash, p_email, v_role_id)
    RETURNING ID_NGUOI_DUNG INTO v_user_id;

    INSERT INTO NHAN_VIEN_HOC_VU (ID_NHAN_VIEN, HO_TEN, GIOI_TINH, SO_DIEN_THOAI, MA_NHAN_VIEN)
    VALUES (v_user_id, p_ho_ten, p_gioi_tinh, p_sdt, v_ma_nhan_vien);

    v_sql := 'CREATE USER "' || v_uname || '" IDENTIFIED BY "' || p_mat_khau || '" PROFILE TTTA_USER_PROFILE';
    EXECUTE IMMEDIATE v_sql;

    EXECUTE IMMEDIATE 'ALTER USER "' || v_uname || '" ACCOUNT UNLOCK';
    EXECUTE IMMEDIATE 'GRANT CREATE SESSION TO "' || v_uname || '"';
    EXECUTE IMMEDIATE 'GRANT role_nhanvienhocvu TO "' || v_uname || '"';

    COMMIT;
    p_ket_qua := N'Thêm nhân viên học vụ thành công!';
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        p_ket_qua := N'Lỗi: Tên đăng nhập hoặc email đã tồn tại.';
    WHEN OTHERS THEN
        ROLLBACK;
        p_ket_qua := N'Lỗi hệ thống: ' || SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE SP_DANG_KY_KE_TOAN(
    p_ten_dang_nhap IN VARCHAR2,
    p_mat_khau      IN VARCHAR2,
    p_email         IN VARCHAR2,
    p_ho_ten        IN NVARCHAR2,
    p_gioi_tinh     IN NVARCHAR2,
    p_sdt           IN VARCHAR2,
    p_ket_qua       OUT NVARCHAR2
)
AS
    v_user_id        NUMBER;
    v_role_id        NUMBER;
    v_sql            VARCHAR2(2000);
    v_mat_khau_hash  VARCHAR2(200);
    v_uname          VARCHAR2(128);
    v_ma_nhan_vien   VARCHAR2(20);
BEGIN
    v_uname := UPPER(TRIM(p_ten_dang_nhap));

    SELECT ID_VAI_TRO INTO v_role_id
    FROM VAI_TRO
    WHERE TEN_VAI_TRO = 'KeToan';

    -- Generate Code KT001
    SELECT 'KT' || LPAD(SEQ_KE_TOAN.NEXTVAL, 3, '0') INTO v_ma_nhan_vien FROM DUAL;

    v_mat_khau_hash := SIMPLE_HASH(p_mat_khau || 'SALT2025');

    INSERT INTO TAI_KHOAN (TEN_DANG_NHAP, MAT_KHAU, EMAIL, ID_VAI_TRO)
    VALUES (v_uname, v_mat_khau_hash, p_email, v_role_id)
    RETURNING ID_NGUOI_DUNG INTO v_user_id;

    INSERT INTO KE_TOAN (ID_KE_TOAN, HO_TEN, GIOI_TINH, SO_DIEN_THOAI, MA_NHAN_VIEN)
    VALUES (v_user_id, p_ho_ten, p_gioi_tinh, p_sdt, v_ma_nhan_vien);

    v_sql := 'CREATE USER "' || v_uname || '" IDENTIFIED BY "' || p_mat_khau || '" PROFILE TTTA_USER_PROFILE';
    EXECUTE IMMEDIATE v_sql;

    EXECUTE IMMEDIATE 'ALTER USER "' || v_uname || '" ACCOUNT UNLOCK';
    EXECUTE IMMEDIATE 'GRANT CREATE SESSION TO "' || v_uname || '"';
    EXECUTE IMMEDIATE 'GRANT role_ketoan TO "' || v_uname || '"';

    COMMIT;
    p_ket_qua := N'Thêm kế toán thành công!';
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        p_ket_qua := N'Lỗi: Tên đăng nhập hoặc email đã tồn tại.';
    WHEN OTHERS THEN
        ROLLBACK;
        p_ket_qua := N'Lỗi hệ thống: ' || SQLERRM;
END;
/


--------------------------------------------------------
-- 1. StudentService Procedures
--------------------------------------------------------
----------
CREATE OR REPLACE PROCEDURE SP_GET_STUDENTS (
    p_page_number IN NUMBER,
    p_page_size   IN NUMBER,
    p_search      IN VARCHAR2,
    p_cursor      OUT SYS_REFCURSOR,
    p_total       OUT NUMBER
) AS
    v_offset NUMBER;
BEGIN
    v_offset := (p_page_number - 1) * p_page_size;
    
    -- Count total
    IF p_search IS NULL OR LENGTH(TRIM(p_search)) = 0 THEN
        SELECT COUNT(*) INTO p_total 
        FROM QLTT_ADMIN.HOC_VIEN s
        JOIN QLTT_ADMIN.TAI_KHOAN a ON a.ID_NGUOI_DUNG = s.ID_HOC_VIEN
        WHERE a.TRANG_THAI_KICH_HOAT = 1;
        
        OPEN p_cursor FOR
            SELECT * FROM (
                SELECT s.*, ROW_NUMBER() OVER (ORDER BY s.ID_HOC_VIEN) AS RN
                FROM QLTT_ADMIN.HOC_VIEN s
                JOIN QLTT_ADMIN.TAI_KHOAN a ON a.ID_NGUOI_DUNG = s.ID_HOC_VIEN
                WHERE a.TRANG_THAI_KICH_HOAT = 1
            )
            WHERE RN > v_offset AND RN <= v_offset + p_page_size;
    ELSE
        SELECT COUNT(*) INTO p_total 
        FROM QLTT_ADMIN.HOC_VIEN s
        JOIN QLTT_ADMIN.TAI_KHOAN a ON a.ID_NGUOI_DUNG = s.ID_HOC_VIEN
        WHERE a.TRANG_THAI_KICH_HOAT = 1
          AND (UPPER(s.HO_TEN) LIKE UPPER(p_search) OR UPPER(s.MA_HOC_VIEN) LIKE UPPER(p_search));

        OPEN p_cursor FOR
            SELECT * FROM (
                SELECT s.*, ROW_NUMBER() OVER (ORDER BY s.ID_HOC_VIEN) AS RN
                FROM QLTT_ADMIN.HOC_VIEN s
                JOIN QLTT_ADMIN.TAI_KHOAN a ON a.ID_NGUOI_DUNG = s.ID_HOC_VIEN
                WHERE a.TRANG_THAI_KICH_HOAT = 1
                  AND (UPPER(s.HO_TEN) LIKE UPPER(p_search) OR UPPER(s.MA_HOC_VIEN) LIKE UPPER(p_search))
            )
            WHERE RN > v_offset AND RN <= v_offset + p_page_size;
    END IF;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_STUDENT_BY_ID (
    p_id     IN NUMBER,
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT s.* 
        FROM QLTT_ADMIN.HOC_VIEN s
        JOIN QLTT_ADMIN.TAI_KHOAN a ON a.ID_NGUOI_DUNG = s.ID_HOC_VIEN
        WHERE s.ID_HOC_VIEN = p_id AND a.TRANG_THAI_KICH_HOAT = 1;
END;
/

CREATE OR REPLACE PROCEDURE SP_CHECK_USERNAME_EXISTS (
    p_username IN VARCHAR2,
    p_count    OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*) INTO p_count 
    FROM QLTT_ADMIN.TAI_KHOAN 
    WHERE UPPER(TEN_DANG_NHAP) = UPPER(TRIM(p_username));
END;
/

CREATE OR REPLACE PROCEDURE SP_CHECK_EMAIL_EXISTS (
    p_email IN VARCHAR2,
    p_count OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*) INTO p_count 
    FROM QLTT_ADMIN.TAI_KHOAN 
    WHERE UPPER(EMAIL) = UPPER(TRIM(p_email));
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_ROLE_ID_BY_NAME (
    p_role_name IN VARCHAR2,
    p_role_id   OUT NUMBER
) AS
BEGIN
    SELECT ID_VAI_TRO INTO p_role_id 
    FROM QLTT_ADMIN.VAI_TRO 
    WHERE UPPER(TEN_VAI_TRO) = UPPER(TRIM(p_role_name))
    FETCH FIRST 1 ROWS ONLY;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        p_role_id := 0;
END;
/

CREATE OR REPLACE PROCEDURE SP_CREATE_ACCOUNT (
    p_username IN VARCHAR2,
    p_password IN VARCHAR2,
    p_email    IN VARCHAR2,
    p_role_id  IN NUMBER,
    p_user_id  OUT NUMBER
) AS
BEGIN
    INSERT INTO QLTT_ADMIN.TAI_KHOAN (TEN_DANG_NHAP, MAT_KHAU, EMAIL, ID_VAI_TRO, TRANG_THAI_KICH_HOAT)
    VALUES (TRIM(p_username), p_password, TRIM(p_email), p_role_id, 1)
    RETURNING ID_NGUOI_DUNG INTO p_user_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_CREATE_STUDENT (
    p_id       IN NUMBER,
    p_fullname IN NVARCHAR2,
    p_sex      IN NVARCHAR2,
    p_dob      IN DATE,
    p_phone    IN VARCHAR2,
    p_addr     IN NVARCHAR2
) AS
BEGIN
    INSERT INTO QLTT_ADMIN.HOC_VIEN (ID_HOC_VIEN, HO_TEN, GIOI_TINH, NGAY_SINH, SO_DIEN_THOAI, DIA_CHI)
    VALUES (p_id, p_fullname, p_sex, p_dob, p_phone, p_addr);
END;
/

CREATE OR REPLACE PROCEDURE SP_CHECK_STUDENT_CODE_EXISTS (
    p_code       IN VARCHAR2,
    p_exclude_id IN NUMBER,
    p_count      OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*) INTO p_count
    FROM QLTT_ADMIN.HOC_VIEN
    WHERE UPPER(MA_HOC_VIEN) = UPPER(TRIM(p_code))
      AND ID_HOC_VIEN != p_exclude_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_UPDATE_STUDENT (
    p_id       IN NUMBER,
    p_fullname IN NVARCHAR2,
    p_code     IN VARCHAR2,
    p_sex      IN NVARCHAR2,
    p_dob      IN DATE,
    p_phone    IN VARCHAR2,
    p_addr     IN NVARCHAR2
) AS
BEGIN
    UPDATE QLTT_ADMIN.HOC_VIEN
    SET HO_TEN = p_fullname,
        MA_HOC_VIEN = p_code,
        GIOI_TINH = p_sex,
        NGAY_SINH = p_dob,
        SO_DIEN_THOAI = p_phone,
        DIA_CHI = p_addr
    WHERE ID_HOC_VIEN = p_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_SOFT_DELETE_STUDENT (
    p_id       IN NUMBER,
    p_rowcount OUT NUMBER
) AS
BEGIN
    UPDATE QLTT_ADMIN.TAI_KHOAN 
    SET TRANG_THAI_KICH_HOAT = 0 
    WHERE ID_NGUOI_DUNG = p_id;
    p_rowcount := SQL%ROWCOUNT;
END;
/

CREATE OR REPLACE PROCEDURE SP_SEARCH_STUDENTS (
    p_keyword IN VARCHAR2,
    p_cursor  OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT s.* 
        FROM QLTT_ADMIN.HOC_VIEN s
        JOIN QLTT_ADMIN.TAI_KHOAN a ON a.ID_NGUOI_DUNG = s.ID_HOC_VIEN
        WHERE a.TRANG_THAI_KICH_HOAT = 1
          AND (UPPER(s.HO_TEN) LIKE UPPER(p_keyword) OR UPPER(s.MA_HOC_VIEN) LIKE UPPER(p_keyword))
        ORDER BY s.HO_TEN;
END;
/

--------------------------------------------------------
-- 2. AuthService Procedures
--------------------------------------------------------

CREATE OR REPLACE PROCEDURE SP_CHECK_ORACLE_USER_EXISTS (
    p_username IN VARCHAR2,
    p_count    OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*) INTO p_count 
    FROM ALL_USERS 
    WHERE USERNAME = UPPER(TRIM(p_username));
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_USER_INFO_BY_USERNAME (
    p_username IN VARCHAR2,
    p_cursor   OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT tk.ID_NGUOI_DUNG, tk.TEN_DANG_NHAP, tk.EMAIL, tk.TRANG_THAI_KICH_HOAT,
               vt.TEN_VAI_TRO, vt.ID_VAI_TRO, hv.HO_TEN
        FROM QLTT_ADMIN.TAI_KHOAN tk
        LEFT JOIN QLTT_ADMIN.VAI_TRO vt ON vt.ID_VAI_TRO = tk.ID_VAI_TRO
        LEFT JOIN QLTT_ADMIN.HOC_VIEN hv ON hv.ID_HOC_VIEN = tk.ID_NGUOI_DUNG
        WHERE UPPER(tk.TEN_DANG_NHAP) = UPPER(TRIM(p_username));
END;
/

CREATE OR REPLACE PROCEDURE SP_LOCK_SESSION_ROW (
    p_user_id IN NUMBER,
    p_cursor  OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT SESSION_ID_PC, SESSION_ID_MOBILE 
        FROM QLTT_ADMIN.TAI_KHOAN 
        WHERE ID_NGUOI_DUNG = p_user_id 
        FOR UPDATE WAIT 1;
END;
/

CREATE OR REPLACE PROCEDURE SP_UPDATE_SESSION_ID (
    p_user_id     IN NUMBER,
    p_session_id  IN VARCHAR2,
    p_device_type IN VARCHAR2
) AS
BEGIN
    IF LOWER(p_device_type) = 'mobile' THEN
        UPDATE QLTT_ADMIN.TAI_KHOAN SET SESSION_ID_MOBILE = p_session_id WHERE ID_NGUOI_DUNG = p_user_id;
    ELSE
        UPDATE QLTT_ADMIN.TAI_KHOAN SET SESSION_ID_PC = p_session_id WHERE ID_NGUOI_DUNG = p_user_id;
    END IF;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_SESSION_ID (
    p_username    IN VARCHAR2,
    p_device_type IN VARCHAR2,
    p_session_id  OUT VARCHAR2
) AS
BEGIN
    IF LOWER(p_device_type) = 'mobile' THEN
        SELECT SESSION_ID_MOBILE INTO p_session_id FROM QLTT_ADMIN.TAI_KHOAN WHERE UPPER(TEN_DANG_NHAP) = UPPER(TRIM(p_username));
    ELSE
        SELECT SESSION_ID_PC INTO p_session_id FROM QLTT_ADMIN.TAI_KHOAN WHERE UPPER(TEN_DANG_NHAP) = UPPER(TRIM(p_username));
    END IF;
EXCEPTION
    WHEN NO_DATA_FOUND THEN p_session_id := NULL;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_USERNAME_BY_SESSION (
    p_session_id  IN VARCHAR2,
    p_device_type IN VARCHAR2,
    p_username    OUT VARCHAR2
) AS
BEGIN
    IF LOWER(p_device_type) = 'mobile' THEN
        SELECT TEN_DANG_NHAP INTO p_username FROM QLTT_ADMIN.TAI_KHOAN WHERE SESSION_ID_MOBILE = p_session_id FETCH FIRST 1 ROWS ONLY;
    ELSE
        SELECT TEN_DANG_NHAP INTO p_username FROM QLTT_ADMIN.TAI_KHOAN WHERE SESSION_ID_PC = p_session_id FETCH FIRST 1 ROWS ONLY;
    END IF;
EXCEPTION
    WHEN NO_DATA_FOUND THEN p_username := NULL;
END;
/

CREATE OR REPLACE PROCEDURE SP_CLEAR_SESSION (
    p_session_id  IN VARCHAR2,
    p_device_type IN VARCHAR2
) AS
BEGIN
    IF LOWER(p_device_type) = 'mobile' THEN
        UPDATE QLTT_ADMIN.TAI_KHOAN SET SESSION_ID_MOBILE = NULL WHERE SESSION_ID_MOBILE = p_session_id;
    ELSE
        UPDATE QLTT_ADMIN.TAI_KHOAN SET SESSION_ID_PC = NULL WHERE SESSION_ID_PC = p_session_id;
    END IF;
END;
/

--------------------------------------------------------
-- 3. RegistrationService Procedures
--------------------------------------------------------

CREATE OR REPLACE PROCEDURE SP_GET_REGISTRATIONS (
    p_status     IN NVARCHAR2,
    p_class_id   IN NUMBER,
    p_class_code IN VARCHAR2,
    p_cursor     OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT dk.ID_DANG_KY, dk.MA_DANG_KY, dk.NGAY_DANG_KY, dk.TRANG_THAI,
               dk.ID_HOC_VIEN, dk.ID_LOP_HOC, NVL(dk.ID_NHAN_VIEN_DUYET,0) AS STAFF_ID,
               lh.TEN_LOP_HOC, kh.TEN_KHOA_HOC
        FROM QLTT_ADMIN.DON_DANG_KY dk
        JOIN QLTT_ADMIN.LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
        JOIN QLTT_ADMIN.KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
        WHERE (p_status IS NULL OR UPPER(TRIM(dk.TRANG_THAI)) LIKE UPPER(TRIM(p_status)) || '%')
          AND (p_class_id IS NULL OR dk.ID_LOP_HOC = p_class_id)
          AND (p_class_code IS NULL OR UPPER(lh.MA_LOP_HOC) = UPPER(p_class_code))
        ORDER BY dk.ID_DANG_KY DESC;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_REGISTRATION_BY_ID (
    p_id     IN NUMBER,
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT dk.ID_DANG_KY, dk.MA_DANG_KY, dk.NGAY_DANG_KY, dk.TRANG_THAI,
               dk.ID_HOC_VIEN, dk.ID_LOP_HOC, NVL(dk.ID_NHAN_VIEN_DUYET,0) AS STAFF_ID,
               lh.TEN_LOP_HOC, kh.TEN_KHOA_HOC
        FROM QLTT_ADMIN.DON_DANG_KY dk
        JOIN QLTT_ADMIN.LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
        JOIN QLTT_ADMIN.KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
        WHERE dk.ID_DANG_KY = p_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_REGISTRATION_INFO (
    p_id        IN NUMBER,
    p_cursor    OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT MA_DANG_KY, ID_LOP_HOC 
        FROM QLTT_ADMIN.DON_DANG_KY 
        WHERE ID_DANG_KY = p_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_UPDATE_REGISTRATION_CLASS (
    p_reg_id   IN NUMBER,
    p_class_id IN NUMBER
) AS
BEGIN
    UPDATE QLTT_ADMIN.DON_DANG_KY 
    SET ID_LOP_HOC = p_class_id 
    WHERE ID_DANG_KY = p_reg_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_CHECK_CLASS_SIZE (
    p_class_id IN NUMBER,
    p_cursor   OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT (SELECT COUNT(*) FROM QLTT_ADMIN.DON_DANG_KY WHERE ID_LOP_HOC=p_class_id AND TRANG_THAI = N'Đã duyệt') AS CNT,
               (SELECT SI_SO_TOI_DA FROM QLTT_ADMIN.LOP_HOC WHERE ID_LOP_HOC=p_class_id) AS MAXS
        FROM DUAL;
END;
/

CREATE OR REPLACE PROCEDURE SP_FIND_STAFF_BY_SESSION (
    p_session_id IN VARCHAR2,
    p_staff_id   OUT NUMBER
) AS
BEGIN
    SELECT ID_NGUOI_DUNG INTO p_staff_id
    FROM QLTT_ADMIN.TAI_KHOAN 
    WHERE SESSION_ID_PC = p_session_id OR SESSION_ID_MOBILE = p_session_id
    FETCH FIRST 1 ROWS ONLY;
EXCEPTION
    WHEN NO_DATA_FOUND THEN p_staff_id := NULL;
END;
/

CREATE OR REPLACE PROCEDURE SP_APPROVE_REGISTRATION (
    p_id       IN NUMBER,
    p_staff_id IN NUMBER
) AS
BEGIN
    UPDATE QLTT_ADMIN.DON_DANG_KY
    SET TRANG_THAI = N'Đã duyệt', 
        NGAY_DUYET = SYSDATE, 
        ID_NHAN_VIEN_DUYET = p_staff_id
    WHERE ID_DANG_KY = p_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_UPDATE_CLASS_STATUS_FULL (
    p_class_id IN NUMBER
) AS
BEGIN
    UPDATE QLTT_ADMIN.LOP_HOC 
    SET TRANG_THAI = N'Đã đủ sĩ số' 
    WHERE ID_LOP_HOC = p_class_id AND TRANG_THAI <> N'Đã đủ sĩ số';
END;
/

CREATE OR REPLACE PROCEDURE SP_REJECT_REGISTRATION (
    p_id       IN NUMBER,
    p_staff_id IN NUMBER
) AS
BEGIN
    UPDATE QLTT_ADMIN.DON_DANG_KY
    SET TRANG_THAI = N'Đã từ chối', 
        NGAY_DUYET = SYSDATE, 
        ID_NHAN_VIEN_DUYET = p_staff_id
    WHERE ID_DANG_KY = p_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_MY_REGISTRATIONS (
    p_student_id IN NUMBER,
    p_cursor     OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT dk.ID_DANG_KY, dk.MA_DANG_KY, dk.NGAY_DANG_KY, dk.TRANG_THAI,
               dk.ID_HOC_VIEN, dk.ID_LOP_HOC, NVL(dk.ID_NHAN_VIEN_DUYET,0) AS STAFF_ID,
               lh.TEN_LOP_HOC, kh.TEN_KHOA_HOC
        FROM QLTT_ADMIN.DON_DANG_KY dk
        JOIN QLTT_ADMIN.LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
        JOIN QLTT_ADMIN.KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
        WHERE dk.ID_HOC_VIEN = p_student_id 
        ORDER BY dk.ID_DANG_KY DESC;
END;
/

CREATE OR REPLACE PROCEDURE SP_SEARCH_REGISTRATIONS (
    p_id      IN NUMBER,
    p_keyword IN VARCHAR2,
    p_cursor  OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT dk.ID_DANG_KY, dk.MA_DANG_KY, dk.NGAY_DANG_KY, dk.TRANG_THAI,
               dk.ID_HOC_VIEN, dk.ID_LOP_HOC, NVL(dk.ID_NHAN_VIEN_DUYET,0) AS STAFF_ID,
               lh.TEN_LOP_HOC, kh.TEN_KHOA_HOC
        FROM QLTT_ADMIN.DON_DANG_KY dk
        JOIN QLTT_ADMIN.LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
        JOIN QLTT_ADMIN.KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
        WHERE (p_id IS NOT NULL AND dk.ID_DANG_KY = p_id)
           OR (UPPER(dk.MA_DANG_KY) LIKE p_keyword)
           OR (UPPER(lh.MA_LOP_HOC) LIKE p_keyword)
           OR (UPPER(kh.MA_KHOA_HOC) LIKE p_keyword)
        ORDER BY dk.ID_DANG_KY DESC;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_ACCOUNTANT_REGS (
    p_course_id IN NUMBER,
    p_class_id  IN NUMBER,
    p_cursor    OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT dk.ID_DANG_KY, dk.NGAY_DANG_KY, dk.TRANG_THAI,
               hv.ID_HOC_VIEN, hv.HO_TEN, tk.EMAIL, hv.SO_DIEN_THOAI,
               lh.ID_LOP_HOC, lh.TEN_LOP_HOC, kh.TEN_KHOA_HOC,
               NVL(kh.HOC_PHI_TIEU_CHUAN,0) AS HOC_PHI,
               hd.ID_HOA_DON
        FROM QLTT_ADMIN.DON_DANG_KY dk
        LEFT JOIN QLTT_ADMIN.HOC_VIEN hv ON hv.ID_HOC_VIEN = dk.ID_HOC_VIEN
        LEFT JOIN QLTT_ADMIN.TAI_KHOAN tk ON tk.ID_NGUOI_DUNG = hv.ID_HOC_VIEN
        LEFT JOIN QLTT_ADMIN.LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
        LEFT JOIN QLTT_ADMIN.KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
        LEFT JOIN QLTT_ADMIN.HOA_DON hd ON hd.ID_DANG_KY = dk.ID_DANG_KY
        WHERE (p_course_id IS NULL OR kh.ID_KHOA_HOC = p_course_id)
          AND (p_class_id IS NULL OR lh.ID_LOP_HOC = p_class_id)
        ORDER BY dk.ID_DANG_KY DESC;
END;
/

--------------------------------------------------------
-- 4. CourseService Procedures
--------------------------------------------------------

CREATE OR REPLACE PROCEDURE SP_GET_COURSES (
    p_page_number IN NUMBER,
    p_page_size   IN NUMBER,
    p_search      IN VARCHAR2,
    p_cursor      OUT SYS_REFCURSOR,
    p_total       OUT NUMBER
) AS
    v_offset NUMBER;
BEGIN
    v_offset := (p_page_number - 1) * p_page_size;
    
    -- Count total
    IF p_search IS NULL THEN
        SELECT COUNT(*) INTO p_total FROM QLTT_ADMIN.KHOA_HOC;
    ELSE
        SELECT COUNT(*) INTO p_total FROM QLTT_ADMIN.KHOA_HOC 
        WHERE UPPER(TEN_KHOA_HOC) LIKE UPPER(p_search) OR UPPER(MA_KHOA_HOC) LIKE UPPER(p_search);
    END IF;

    -- Get data
    IF p_search IS NULL THEN
        OPEN p_cursor FOR
            SELECT * FROM (
                SELECT c.*, ROW_NUMBER() OVER (ORDER BY c.ID_KHOA_HOC) as RN
                FROM QLTT_ADMIN.KHOA_HOC c
            ) WHERE RN > v_offset AND RN <= v_offset + p_page_size;
    ELSE
        OPEN p_cursor FOR
            SELECT * FROM (
                SELECT c.*, ROW_NUMBER() OVER (ORDER BY c.ID_KHOA_HOC) as RN
                FROM QLTT_ADMIN.KHOA_HOC c
                WHERE UPPER(TEN_KHOA_HOC) LIKE UPPER(p_search) OR UPPER(MA_KHOA_HOC) LIKE UPPER(p_search)
            ) WHERE RN > v_offset AND RN <= v_offset + p_page_size;
    END IF;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_COURSE_BY_ID (
    p_id     IN NUMBER,
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT ID_KHOA_HOC, MA_KHOA_HOC, TEN_KHOA_HOC, MO_TA, HOC_PHI_TIEU_CHUAN
        FROM QLTT_ADMIN.KHOA_HOC WHERE ID_KHOA_HOC = p_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_CREATE_COURSE (
    p_code     IN VARCHAR2,
    p_name     IN NVARCHAR2,
    p_desc     IN NVARCHAR2,
    p_fee      IN NUMBER,
    p_cursor   OUT SYS_REFCURSOR
) AS
BEGIN
    INSERT INTO QLTT_ADMIN.KHOA_HOC (MA_KHOA_HOC, TEN_KHOA_HOC, MO_TA, HOC_PHI_TIEU_CHUAN)
    VALUES (p_code, p_name, p_desc, p_fee);
    
    OPEN p_cursor FOR
        SELECT * FROM QLTT_ADMIN.KHOA_HOC WHERE MA_KHOA_HOC = p_code;
END;
/

CREATE OR REPLACE PROCEDURE SP_UPDATE_COURSE (
    p_id       IN NUMBER,
    p_code     IN VARCHAR2,
    p_name     IN NVARCHAR2,
    p_desc     IN NVARCHAR2,
    p_fee      IN NUMBER
) AS
BEGIN
    UPDATE QLTT_ADMIN.KHOA_HOC 
    SET MA_KHOA_HOC = p_code,
        TEN_KHOA_HOC = p_name,
        MO_TA = p_desc,
        HOC_PHI_TIEU_CHUAN = p_fee
    WHERE ID_KHOA_HOC = p_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_DELETE_COURSE (
    p_id       IN NUMBER,
    p_rowcount OUT NUMBER
) AS
BEGIN
    DELETE FROM QLTT_ADMIN.KHOA_HOC WHERE ID_KHOA_HOC = p_id;
    p_rowcount := SQL%ROWCOUNT;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_ALL_COURSES (
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT ID_KHOA_HOC, MA_KHOA_HOC, TEN_KHOA_HOC, MO_TA, HOC_PHI_TIEU_CHUAN
        FROM QLTT_ADMIN.KHOA_HOC ORDER BY TEN_KHOA_HOC;
END;
/

CREATE OR REPLACE PROCEDURE SP_CHECK_COURSE_CODE_EXISTS (
    p_code       IN VARCHAR2,
    p_exclude_id IN NUMBER,
    p_count      OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*) INTO p_count
    FROM QLTT_ADMIN.KHOA_HOC
    WHERE UPPER(MA_KHOA_HOC) = UPPER(TRIM(p_code))
      AND (p_exclude_id IS NULL OR ID_KHOA_HOC != p_exclude_id);
END;
/

--------------------------------------------------------
-- 5. ClassService Procedures
--------------------------------------------------------

CREATE OR REPLACE PROCEDURE SP_GET_CLASSES (
    p_course_id IN NUMBER,
    p_search    IN VARCHAR2,
    p_cursor    OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT lh.ID_LOP_HOC, lh.MA_LOP_HOC, lh.TEN_LOP_HOC, lh.NGAY_BAT_DAU, lh.NGAY_KET_THUC,
               lh.SI_SO_TOI_DA, lh.ID_KHOA_HOC, lh.ID_GIANG_VIEN, lh.TRANG_THAI,
               (SELECT COUNT(*) FROM QLTT_ADMIN.DON_DANG_KY dk 
                JOIN QLTT_ADMIN.HOA_DON hd ON hd.ID_DANG_KY = dk.ID_DANG_KY AND UPPER(hd.TRANG_THAI) = UPPER(N'Đã thanh toán')
                WHERE dk.ID_LOP_HOC = lh.ID_LOP_HOC AND UPPER(dk.TRANG_THAI) = UPPER(N'Đã duyệt')) AS APPROVED_COUNT
        FROM QLTT_ADMIN.LOP_HOC lh
        WHERE (p_course_id IS NULL OR p_course_id = 0 OR lh.ID_KHOA_HOC = p_course_id)
          AND (p_search IS NULL OR UPPER(lh.TEN_LOP_HOC) LIKE UPPER(p_search) OR UPPER(lh.MA_LOP_HOC) LIKE UPPER(p_search))
        ORDER BY lh.ID_LOP_HOC;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_CLASS_BY_ID (
    p_id     IN NUMBER,
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT * FROM QLTT_ADMIN.LOP_HOC WHERE ID_LOP_HOC = p_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_CREATE_CLASS (
    p_code      IN VARCHAR2,
    p_name      IN NVARCHAR2,
    p_start     IN DATE,
    p_end       IN DATE,
    p_max       IN NUMBER,
    p_course_id IN NUMBER,
    p_teacher_id IN NUMBER,
    p_cursor    OUT SYS_REFCURSOR
) AS
BEGIN
    INSERT INTO QLTT_ADMIN.LOP_HOC (MA_LOP_HOC, TEN_LOP_HOC, NGAY_BAT_DAU, NGAY_KET_THUC, SI_SO_TOI_DA, ID_KHOA_HOC, ID_GIANG_VIEN, TRANG_THAI)
    VALUES (p_code, p_name, p_start, p_end, p_max, p_course_id, p_teacher_id, N'Đang tuyển sinh');
    
    OPEN p_cursor FOR
        SELECT * FROM QLTT_ADMIN.LOP_HOC WHERE MA_LOP_HOC = p_code;
END;
/

CREATE OR REPLACE PROCEDURE SP_UPDATE_CLASS (
    p_id        IN NUMBER,
    p_code      IN VARCHAR2,
    p_name      IN NVARCHAR2,
    p_start     IN DATE,
    p_end       IN DATE,
    p_max       IN NUMBER,
    p_course_id IN NUMBER,
    p_teacher_id IN NUMBER,
    p_status    IN NVARCHAR2
) AS
BEGIN
    IF p_status IS NOT NULL THEN
        UPDATE QLTT_ADMIN.LOP_HOC 
        SET MA_LOP_HOC=p_code, TEN_LOP_HOC=p_name, NGAY_BAT_DAU=p_start, NGAY_KET_THUC=p_end,
            SI_SO_TOI_DA=p_max, ID_KHOA_HOC=p_course_id, ID_GIANG_VIEN=p_teacher_id, TRANG_THAI=p_status
        WHERE ID_LOP_HOC=p_id;
    ELSE
        UPDATE QLTT_ADMIN.LOP_HOC 
        SET MA_LOP_HOC=p_code, TEN_LOP_HOC=p_name, NGAY_BAT_DAU=p_start, NGAY_KET_THUC=p_end,
            SI_SO_TOI_DA=p_max, ID_KHOA_HOC=p_course_id, ID_GIANG_VIEN=p_teacher_id
        WHERE ID_LOP_HOC=p_id;
    END IF;
END;
/

CREATE OR REPLACE PROCEDURE SP_DELETE_CLASS (
    p_id       IN NUMBER,
    p_rowcount OUT NUMBER
) AS
    v_reg_count NUMBER;
BEGIN
    -- Check for registrations
    SELECT COUNT(*) INTO v_reg_count FROM QLTT_ADMIN.DON_DANG_KY WHERE ID_LOP_HOC = p_id;
    IF v_reg_count > 0 THEN
        RAISE_APPLICATION_ERROR(-20001, 'Không thể xóa lớp học đã có học viên đăng ký.');
    END IF;

    -- Delete schedules
    DELETE FROM QLTT_ADMIN.LICH_HOC WHERE ID_LOP_HOC = p_id;

    -- Delete class
    DELETE FROM QLTT_ADMIN.LOP_HOC WHERE ID_LOP_HOC = p_id;
    p_rowcount := SQL%ROWCOUNT;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_CLASS_ROSTER (
    p_class_id IN NUMBER,
    p_cursor   OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT hv.ID_HOC_VIEN, hv.HO_TEN, hv.MA_HOC_VIEN, hv.GIOI_TINH, hv.NGAY_SINH, hv.SO_DIEN_THOAI, hv.DIA_CHI
        FROM QLTT_ADMIN.DON_DANG_KY dk
        JOIN QLTT_ADMIN.HOC_VIEN hv ON hv.ID_HOC_VIEN = dk.ID_HOC_VIEN
        JOIN QLTT_ADMIN.HOA_DON hd ON hd.ID_DANG_KY = dk.ID_DANG_KY AND UPPER(hd.TRANG_THAI)=UPPER(N'Đã thanh toán')
        WHERE dk.ID_LOP_HOC = p_class_id AND UPPER(dk.TRANG_THAI) = UPPER(N'Đã duyệt');
END;
/

CREATE OR REPLACE PROCEDURE SP_CHECK_CLASS_CODE_EXISTS (
    p_code       IN VARCHAR2,
    p_exclude_id IN NUMBER,
    p_count      OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*) INTO p_count
    FROM QLTT_ADMIN.LOP_HOC
    WHERE UPPER(MA_LOP_HOC) = UPPER(TRIM(p_code))
      AND (p_exclude_id IS NULL OR ID_LOP_HOC != p_exclude_id);
END;
/

--------------------------------------------------------
-- 6. ScheduleService Procedures
--------------------------------------------------------

CREATE OR REPLACE PROCEDURE SP_GET_SCHEDULE_BY_CLASS (
    p_class_id IN NUMBER,
    p_cursor   OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT ID_LICH_HOC, ID_LOP_HOC, THU_TRONG_TUAN, GIO_BAT_DAU, GIO_KET_THUC
        FROM QLTT_ADMIN.LICH_HOC 
        WHERE ID_LOP_HOC = p_class_id 
        ORDER BY THU_TRONG_TUAN, GIO_BAT_DAU;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_SCHEDULE_BY_ID (
    p_id     IN NUMBER,
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT ID_LICH_HOC, ID_LOP_HOC, THU_TRONG_TUAN, GIO_BAT_DAU, GIO_KET_THUC
        FROM QLTT_ADMIN.LICH_HOC 
        WHERE ID_LICH_HOC = p_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_CREATE_SCHEDULE (
    p_class_id IN NUMBER,
    p_dow      IN NUMBER,
    p_start    IN VARCHAR2,
    p_end      IN VARCHAR2,
    p_cursor   OUT SYS_REFCURSOR
) AS
BEGIN
    INSERT INTO QLTT_ADMIN.LICH_HOC (ID_LOP_HOC, THU_TRONG_TUAN, GIO_BAT_DAU, GIO_KET_THUC)
    VALUES (p_class_id, p_dow, p_start, p_end);
    
    OPEN p_cursor FOR
        SELECT ID_LICH_HOC, ID_LOP_HOC, THU_TRONG_TUAN, GIO_BAT_DAU, GIO_KET_THUC
        FROM QLTT_ADMIN.LICH_HOC 
        WHERE ID_LOP_HOC = p_class_id AND THU_TRONG_TUAN = p_dow AND GIO_BAT_DAU = p_start 
        ORDER BY ID_LICH_HOC DESC FETCH FIRST 1 ROWS ONLY;
END;
/

CREATE OR REPLACE PROCEDURE SP_UPDATE_SCHEDULE (
    p_id       IN NUMBER,
    p_class_id IN NUMBER,
    p_dow      IN NUMBER,
    p_start    IN VARCHAR2,
    p_end      IN VARCHAR2
) AS
BEGIN
    UPDATE QLTT_ADMIN.LICH_HOC 
    SET ID_LOP_HOC=p_class_id, THU_TRONG_TUAN=p_dow, GIO_BAT_DAU=p_start, GIO_KET_THUC=p_end
    WHERE ID_LICH_HOC=p_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_DELETE_SCHEDULE (
    p_id       IN NUMBER,
    p_rowcount OUT NUMBER
) AS
BEGIN
    DELETE FROM QLTT_ADMIN.LICH_HOC WHERE ID_LICH_HOC = p_id;
    p_rowcount := SQL%ROWCOUNT;
END;
/

--------------------------------------------------------
-- 7. InvoiceService Procedures
--------------------------------------------------------

CREATE OR REPLACE PROCEDURE SP_CREATE_INVOICE (
    p_code    IN VARCHAR2,
    p_due     IN DATE,
    p_amt     IN NUMBER,
    p_reg_id  IN NUMBER,
    p_out_id  OUT NUMBER
) AS
BEGIN
    INSERT INTO QLTT_ADMIN.HOA_DON (MA_HOA_DON, NGAY_TAO, NGAY_HET_HAN, SO_TIEN, TRANG_THAI, ID_DANG_KY)
    VALUES (p_code, SYSDATE, p_due, p_amt, N'Chưa thanh toán', p_reg_id)
    RETURNING ID_HOA_DON INTO p_out_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_INVOICE_BY_REG (
    p_reg_id IN NUMBER,
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT ID_HOA_DON, MA_HOA_DON, NGAY_TAO, NGAY_HET_HAN, SO_TIEN, TRANG_THAI, 
               ID_DANG_KY, CHU_KY_BASE64, THUAT_TOAN, ID_KE_TOAN_KY, NGAY_KY, CHU_KY_HINH_BASE64,
               NVL(DA_IN, 0) AS DA_IN
        FROM QLTT_ADMIN.HOA_DON 
        WHERE ID_DANG_KY = p_reg_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_INVOICE_BY_ID (
    p_id     IN NUMBER,
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT ID_HOA_DON, MA_HOA_DON, NGAY_TAO, NGAY_HET_HAN, SO_TIEN, TRANG_THAI, 
               ID_DANG_KY, CHU_KY_BASE64, THUAT_TOAN, ID_KE_TOAN_KY, NGAY_KY, CHU_KY_HINH_BASE64,
               NVL(DA_IN, 0) AS DA_IN
        FROM QLTT_ADMIN.HOA_DON 
        WHERE ID_HOA_DON = p_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_UPDATE_INVOICE_STATUS (
    p_id     IN NUMBER,
    p_status IN NVARCHAR2,
    p_rows   OUT NUMBER
) AS
BEGIN
    UPDATE QLTT_ADMIN.HOA_DON 
    SET TRANG_THAI = p_status 
    WHERE ID_HOA_DON = p_id;
    p_rows := SQL%ROWCOUNT;
END;
/

CREATE OR REPLACE PROCEDURE SP_MARK_INVOICE_PRINTED (
    p_id   IN NUMBER,
    p_rows OUT NUMBER
) AS
BEGIN
    UPDATE QLTT_ADMIN.HOA_DON 
    SET DA_IN = 1 
    WHERE ID_HOA_DON = p_id;
    p_rows := SQL%ROWCOUNT;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_INVOICE_STUDENT_INFO (
    p_reg_id IN NUMBER,
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT hv.HO_TEN, tk.EMAIL, kh.TEN_KHOA_HOC, lh.TEN_LOP_HOC
        FROM QLTT_ADMIN.DON_DANG_KY dk
        JOIN QLTT_ADMIN.HOC_VIEN hv ON hv.ID_HOC_VIEN = dk.ID_HOC_VIEN
        LEFT JOIN QLTT_ADMIN.TAI_KHOAN tk ON tk.ID_NGUOI_DUNG = hv.ID_HOC_VIEN
        JOIN QLTT_ADMIN.LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
        JOIN QLTT_ADMIN.KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
        WHERE dk.ID_DANG_KY = p_reg_id;
END;
/

--------------------------------------------------------
-- 8. PaymentService Procedures
--------------------------------------------------------

CREATE OR REPLACE PROCEDURE SP_GET_PENDING_PAYMENTS (
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT hd.ID_HOA_DON, hd.MA_HOA_DON, hd.SO_TIEN, hd.NGAY_TAO,
               hv.HO_TEN, dk.MA_DANG_KY, kh.TEN_KHOA_HOC
        FROM QLTT_ADMIN.HOA_DON hd
        JOIN QLTT_ADMIN.DON_DANG_KY dk ON dk.ID_DANG_KY = hd.ID_DANG_KY
        JOIN QLTT_ADMIN.HOC_VIEN hv ON hv.ID_HOC_VIEN = dk.ID_HOC_VIEN
        JOIN QLTT_ADMIN.LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
        JOIN QLTT_ADMIN.KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
        WHERE hd.TRANG_THAI = N'Chờ xác nhận thanh toán'
        ORDER BY hd.NGAY_TAO DESC;
END;
/

CREATE OR REPLACE PROCEDURE SP_CREATE_PAYMENT_RECEIPT (
    p_invoice_id IN NUMBER,
    p_amount     IN NUMBER,
    p_acc_id     IN NUMBER
) AS
BEGIN
    INSERT INTO QLTT_ADMIN.PHIEU_THANH_TOAN 
    (NGAY_THANH_TOAN, SO_TIEN_DA_TRA, PHUONG_THUC_THANH_TOAN, ID_HOA_DON, ID_KE_TOAN_XAC_NHAN)
    VALUES (SYSDATE, p_amount, N'Chuyển khoản', p_invoice_id, p_acc_id);
    
    UPDATE QLTT_ADMIN.HOA_DON 
    SET TRANG_THAI = N'Đã thanh toán' 
    WHERE ID_HOA_DON = p_invoice_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_CREATE_PAYMENT (
    p_inv_id  IN NUMBER,
    p_amt     IN NUMBER,
    p_method  IN NVARCHAR2,
    p_acc_id  IN NUMBER,
    p_out_id  OUT NUMBER
) AS
BEGIN
    INSERT INTO QLTT_ADMIN.PHIEU_THANH_TOAN (NGAY_THANH_TOAN, SO_TIEN_DA_TRA, PHUONG_THUC_THANH_TOAN, ID_HOA_DON, ID_KE_TOAN_XAC_NHAN)
    VALUES (SYSDATE, p_amt, p_method, p_inv_id, p_acc_id)
    RETURNING ID_THANH_TOAN INTO p_out_id;
    
    UPDATE QLTT_ADMIN.HOA_DON 
    SET TRANG_THAI = N'Đã thanh toán' 
    WHERE ID_HOA_DON = p_inv_id;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_PAYMENTS_BY_INVOICE (
    p_inv_id IN NUMBER,
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT ID_THANH_TOAN, NGAY_THANH_TOAN, SO_TIEN_DA_TRA, PHUONG_THUC_THANH_TOAN, ID_HOA_DON, ID_KE_TOAN_XAC_NHAN
        FROM QLTT_ADMIN.PHIEU_THANH_TOAN 
        WHERE ID_HOA_DON = p_inv_id 
        ORDER BY ID_THANH_TOAN DESC;
END;
/

--------------------------------------------------------
-- 9. StaffService Procedures
--------------------------------------------------------

CREATE OR REPLACE PROCEDURE SP_STAFF_SEARCH_STUDENTS (
    p_keyword IN VARCHAR2,
    p_cursor  OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT hv.ID_HOC_VIEN, hv.MA_HOC_VIEN, hv.HO_TEN, tk.EMAIL, hv.SO_DIEN_THOAI, NVL(tk.TRANG_THAI_KICH_HOAT,0) AS ACTIVE
        FROM QLTT_ADMIN.HOC_VIEN hv
        LEFT JOIN QLTT_ADMIN.TAI_KHOAN tk ON tk.ID_NGUOI_DUNG = hv.ID_HOC_VIEN
        WHERE (p_keyword IS NULL OR 
               UPPER(hv.HO_TEN) LIKE UPPER(p_keyword) OR 
               UPPER(hv.MA_HOC_VIEN) LIKE UPPER(p_keyword) OR 
               UPPER(tk.EMAIL) LIKE UPPER(p_keyword) OR 
               UPPER(tk.TEN_DANG_NHAP) LIKE UPPER(p_keyword) OR 
               hv.SO_DIEN_THOAI LIKE p_keyword)
        ORDER BY hv.ID_HOC_VIEN DESC;
END;
/

CREATE OR REPLACE PROCEDURE SP_LOCK_ACCOUNT (
    p_id   IN NUMBER,
    p_rows OUT NUMBER
) AS
BEGIN
    UPDATE QLTT_ADMIN.TAI_KHOAN 
    SET TRANG_THAI_KICH_HOAT = 0 
    WHERE ID_NGUOI_DUNG = p_id;
    p_rows := SQL%ROWCOUNT;
END;
/

CREATE OR REPLACE PROCEDURE SP_UNLOCK_ACCOUNT (
    p_id   IN NUMBER,
    p_rows OUT NUMBER
) AS
BEGIN
    UPDATE QLTT_ADMIN.TAI_KHOAN 
    SET TRANG_THAI_KICH_HOAT = 1 
    WHERE ID_NGUOI_DUNG = p_id;
    p_rows := SQL%ROWCOUNT;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_STUDENT_REGISTRATIONS (
    p_id     IN NUMBER,
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT dk.ID_DANG_KY, dk.MA_DANG_KY, dk.NGAY_DANG_KY, dk.TRANG_THAI,
               dk.ID_HOC_VIEN, dk.ID_LOP_HOC, NVL(dk.ID_NHAN_VIEN_DUYET,0) AS STAFF_ID,
               lh.TEN_LOP_HOC, kh.TEN_KHOA_HOC
        FROM QLTT_ADMIN.DON_DANG_KY dk
        JOIN QLTT_ADMIN.LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
        JOIN QLTT_ADMIN.KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
        WHERE dk.ID_HOC_VIEN = p_id 
        ORDER BY dk.ID_DANG_KY DESC;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_STUDENT_INVOICES (
    p_id     IN NUMBER,
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT hd.ID_HOA_DON, hd.MA_HOA_DON, hd.TRANG_THAI, dk.ID_DANG_KY, kh.TEN_KHOA_HOC
        FROM QLTT_ADMIN.HOA_DON hd
        JOIN QLTT_ADMIN.DON_DANG_KY dk ON dk.ID_DANG_KY = hd.ID_DANG_KY
        JOIN QLTT_ADMIN.LOP_HOC lh ON lh.ID_LOP_HOC = dk.ID_LOP_HOC
        JOIN QLTT_ADMIN.KHOA_HOC kh ON kh.ID_KHOA_HOC = lh.ID_KHOA_HOC
        WHERE dk.ID_HOC_VIEN = p_id 
        ORDER BY hd.ID_HOA_DON DESC;
END;
/

--------------------------------------------------------
-- 10. AdminStaffService Procedures
--------------------------------------------------------

CREATE OR REPLACE PROCEDURE SP_GET_STAFF_LIST (
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT tk.ID_NGUOI_DUNG, tk.TEN_DANG_NHAP, tk.EMAIL, tk.ID_VAI_TRO, vt.TEN_VAI_TRO,
               tk.TRANG_THAI_KICH_HOAT,
               COALESCE(nv.HO_TEN, kt.HO_TEN) AS HO_TEN,
               COALESCE(nv.GIOI_TINH, kt.GIOI_TINH) AS GIOI_TINH,
               COALESCE(nv.SO_DIEN_THOAI, kt.SO_DIEN_THOAI) AS SO_DIEN_THOAI
        FROM QLTT_ADMIN.TAI_KHOAN tk
        LEFT JOIN QLTT_ADMIN.VAI_TRO vt ON vt.ID_VAI_TRO = tk.ID_VAI_TRO
        LEFT JOIN QLTT_ADMIN.NHAN_VIEN_HOC_VU nv ON nv.ID_NHAN_VIEN = tk.ID_NGUOI_DUNG
        LEFT JOIN QLTT_ADMIN.KE_TOAN kt ON kt.ID_KE_TOAN = tk.ID_NGUOI_DUNG
        WHERE tk.ID_VAI_TRO IN (3,4,5)
        ORDER BY tk.ID_NGUOI_DUNG;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_STAFF_BY_ID (
    p_id     IN NUMBER,
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT tk.ID_NGUOI_DUNG, tk.TEN_DANG_NHAP, tk.EMAIL, tk.ID_VAI_TRO, vt.TEN_VAI_TRO,
               tk.TRANG_THAI_KICH_HOAT,
               COALESCE(nv.HO_TEN, kt.HO_TEN) AS HO_TEN,
               COALESCE(nv.GIOI_TINH, kt.GIOI_TINH) AS GIOI_TINH,
               COALESCE(nv.SO_DIEN_THOAI, kt.SO_DIEN_THOAI) AS SO_DIEN_THOAI
        FROM QLTT_ADMIN.TAI_KHOAN tk
        LEFT JOIN QLTT_ADMIN.VAI_TRO vt ON vt.ID_VAI_TRO = tk.ID_VAI_TRO
        LEFT JOIN QLTT_ADMIN.NHAN_VIEN_HOC_VU nv ON nv.ID_NHAN_VIEN = tk.ID_NGUOI_DUNG
        LEFT JOIN QLTT_ADMIN.KE_TOAN kt ON kt.ID_KE_TOAN = tk.ID_NGUOI_DUNG
        WHERE tk.ID_NGUOI_DUNG = p_id;
END;
/

-- Create Sequences if not exist and Fix Start Value
DECLARE
    v_count NUMBER;
    v_max_id NUMBER;
    v_sql VARCHAR2(200);
BEGIN
    -- Fix SEQ_NHAN_VIEN_HOC_VU
    SELECT COUNT(*) INTO v_count FROM USER_SEQUENCES WHERE SEQUENCE_NAME = 'SEQ_NHAN_VIEN_HOC_VU';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_NHAN_VIEN_HOC_VU START WITH 1 INCREMENT BY 1 NOCACHE';
    END IF;

    -- Reset SEQ_NHAN_VIEN_HOC_VU to max + 1
    SELECT NVL(MAX(TO_NUMBER(REGEXP_SUBSTR(MA_NHAN_VIEN, '\d+'))), 0) + 1 INTO v_max_id FROM NHAN_VIEN_HOC_VU;
    EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_NHAN_VIEN_HOC_VU';
    v_sql := 'CREATE SEQUENCE SEQ_NHAN_VIEN_HOC_VU START WITH ' || v_max_id || ' INCREMENT BY 1 NOCACHE';
    EXECUTE IMMEDIATE v_sql;

    -- Fix SEQ_KE_TOAN
    SELECT COUNT(*) INTO v_count FROM USER_SEQUENCES WHERE SEQUENCE_NAME = 'SEQ_KE_TOAN';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_KE_TOAN START WITH 1 INCREMENT BY 1 NOCACHE';
    END IF;

    -- Reset SEQ_KE_TOAN to max + 1
    SELECT NVL(MAX(TO_NUMBER(REGEXP_SUBSTR(MA_NHAN_VIEN, '\d+'))), 0) + 1 INTO v_max_id FROM KE_TOAN;
    EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_KE_TOAN';
    v_sql := 'CREATE SEQUENCE SEQ_KE_TOAN START WITH ' || v_max_id || ' INCREMENT BY 1 NOCACHE';
    EXECUTE IMMEDIATE v_sql;
END;
/

CREATE OR REPLACE PROCEDURE SP_UPDATE_STAFF_INFO (
    p_id       IN NUMBER,
    p_email    IN VARCHAR2,
    p_role_id  IN NUMBER,
    p_fullname IN NVARCHAR2,
    p_sex      IN NVARCHAR2,
    p_phone    IN VARCHAR2,
    p_status   OUT VARCHAR2
) AS
BEGIN
    -- Only update Email in TAI_KHOAN
    UPDATE QLTT_ADMIN.TAI_KHOAN 
    SET EMAIL = TRIM(p_email)
    WHERE ID_NGUOI_DUNG = p_id;

    -- Update Phone in NHAN_VIEN_HOC_VU or KE_TOAN depending on where the ID exists
    -- We try updating both tables; one will succeed, one will do nothing (0 rows)
    
    UPDATE QLTT_ADMIN.NHAN_VIEN_HOC_VU
    SET SO_DIEN_THOAI = p_phone
    WHERE ID_NHAN_VIEN = p_id;
    
    UPDATE QLTT_ADMIN.KE_TOAN
    SET SO_DIEN_THOAI = p_phone
    WHERE ID_KE_TOAN = p_id;
    
    COMMIT;
    p_status := 'SUCCESS';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_status := 'ERROR: ' || SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE SP_LOCK_ACCOUNT (
    p_id   IN NUMBER,
    p_rows OUT NUMBER
) AS
BEGIN
    UPDATE QLTT_ADMIN.TAI_KHOAN SET TRANG_THAI_KICH_HOAT = 0 WHERE ID_NGUOI_DUNG = p_id;
    p_rows := SQL%ROWCOUNT;
    COMMIT;
END;
/

CREATE OR REPLACE PROCEDURE SP_UNLOCK_ACCOUNT (
    p_id   IN NUMBER,
    p_rows OUT NUMBER
) AS
BEGIN
    UPDATE QLTT_ADMIN.TAI_KHOAN SET TRANG_THAI_KICH_HOAT = 1 WHERE ID_NGUOI_DUNG = p_id;
    p_rows := SQL%ROWCOUNT;
    COMMIT;
END;
/

--------------------------------------------------------
-- 11. Missing Procedures (Fixes)
--------------------------------------------------------

CREATE OR REPLACE PROCEDURE SP_GET_ALL_COURSES (
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT ID_KHOA_HOC, MA_KHOA_HOC, TEN_KHOA_HOC, MO_TA, HOC_PHI_TIEU_CHUAN
        FROM QLTT_ADMIN.KHOA_HOC
        ORDER BY ID_KHOA_HOC DESC;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_CLASS_ROSTER (
    p_class_id IN NUMBER,
    p_cursor   OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT hv.ID_HOC_VIEN, hv.MA_HOC_VIEN, hv.HO_TEN, hv.GIOI_TINH, hv.NGAY_SINH, hv.SO_DIEN_THOAI
        FROM QLTT_ADMIN.DON_DANG_KY dk
        JOIN QLTT_ADMIN.HOC_VIEN hv ON hv.ID_HOC_VIEN = dk.ID_HOC_VIEN
        JOIN QLTT_ADMIN.HOA_DON hd ON hd.ID_DANG_KY = dk.ID_DANG_KY
        WHERE dk.ID_LOP_HOC = p_class_id
          AND hd.TRANG_THAI = N'Đã thanh toán'
        ORDER BY hv.HO_TEN;
END;
/

CREATE OR REPLACE PROCEDURE SP_GET_ALL_TEACHERS (
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT ID_GIANG_VIEN, HO_TEN, MA_GIANG_VIEN
        FROM QLTT_ADMIN.GIANG_VIEN
        ORDER BY HO_TEN;
END;
/

-- Grants for Staff
BEGIN
    EXECUTE IMMEDIATE 'GRANT EXECUTE ON SP_GET_ALL_COURSES TO ROLE_NHANVIENHOCVU';
    EXECUTE IMMEDIATE 'GRANT EXECUTE ON SP_GET_CLASS_ROSTER TO ROLE_NHANVIENHOCVU';
    EXECUTE IMMEDIATE 'GRANT EXECUTE ON SP_GET_ALL_TEACHERS TO ROLE_NHANVIENHOCVU';
EXCEPTION
    WHEN OTHERS THEN NULL; -- Ignore if role doesn't exist or grant fails
END;
/
