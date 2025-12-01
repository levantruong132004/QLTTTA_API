
-----------------------------------------------------------------------------------------------------
-- ============================================================================
-- Student-only auditing for HOC_VIEN
-- Components:
--   1) Helper function IS_STUDENT_CTX(): returns 1 if current session is Student
--      (determined via CLIENT_IDENTIFIER -> TAI_KHOAN, fallback USER)
--   2) DML row trigger on HOC_VIEN: writes change history only when Student acts
--   3) FGA policy: audits SELECT/UPDATE/DELETE only for Student sessions
--
-- Run as: QLTT_ADMIN (owner of HOC_VIEN)
-- Prereqs:
--   - EXECUTE on DBMS_FGA (to add policy) – if missing, grant: GRANT EXECUTE ON DBMS_FGA TO QLTT_ADMIN;
--   - Table HOC_VIEN exists with columns: ID_HOC_VIEN, HO_TEN, EMAIL, SO_DIEN_THOAI, DIA_CHI, GIOI_TINH, NGAY_SINH
--   - Table TAI_KHOAN with TEN_DANG_NHAP, ID_NGUOI_DUNG, ID_VAI_TRO, TRANG_THAI_KICH_HOAT
--   - Table VAI_TRO with ID_VAI_TRO, TEN_VAI_TRO (1 or name 'HocVien' for students)
-- Notes:
--   - This script is idempotent-ish: drops trigger/func/policy if exist.
--   - FGA does not require OLS; works independently.
--   - Other roles are unaffected because function guards both trigger and FGA condition.
-- ============================================================================

set serveroutput on

-- 1) Helper function: detect Student based on session identity
BEGIN
  EXECUTE IMMEDIATE 'DROP FUNCTION IS_STUDENT_CTX';
EXCEPTION WHEN OTHERS THEN NULL; END;
/

CREATE OR REPLACE FUNCTION IS_STUDENT_CTX RETURN NUMBER
AUTHID CURRENT_USER
AS
  v_ident       VARCHAR2(128);
  v_user        VARCHAR2(128);
  v_role_id     NUMBER;
  v_role_name   NVARCHAR2(100);
  v_user_id     NUMBER;
BEGIN
  v_ident := SYS_CONTEXT('USERENV','CLIENT_IDENTIFIER');
  v_user  := SYS_CONTEXT('USERENV','SESSION_USER');
  IF v_ident IS NULL OR LENGTH(TRIM(v_ident)) = 0 THEN
    v_ident := v_user;
  END IF;

  BEGIN
    SELECT tk.ID_NGUOI_DUNG, tk.ID_VAI_TRO, vt.TEN_VAI_TRO
      INTO v_user_id, v_role_id, v_role_name
      FROM QLTT_ADMIN.TAI_KHOAN tk
      JOIN QLTT_ADMIN.VAI_TRO vt ON vt.ID_VAI_TRO = tk.ID_VAI_TRO
     WHERE UPPER(tk.TEN_DANG_NHAP) = UPPER(v_ident)
       AND tk.TRANG_THAI_KICH_HOAT = 1;
  EXCEPTION
    WHEN NO_DATA_FOUND THEN
      RETURN 0; -- unknown -> not student
  END;

  IF v_role_id = 1 OR UPPER(v_role_name) IN ('HOCVIEN','HỌC VIÊN','HOC VIEN') THEN
    RETURN 1; -- student
  END IF;
  RETURN 0; -- not student (staff/admin/accountant...)
END;
/
SHOW ERRORS FUNCTION IS_STUDENT_CTX

-- 2) Audit table + sequence for DML history
DECLARE
  v_cnt NUMBER;
BEGIN
  SELECT COUNT(*) INTO v_cnt FROM user_tables WHERE table_name = 'HOC_VIEN_AUDIT';
  IF v_cnt = 0 THEN
    EXECUTE IMMEDIATE q'[
      CREATE TABLE HOC_VIEN_AUDIT (
        AUDIT_ID         NUMBER PRIMARY KEY,
        ACTION           VARCHAR2(10),
        ROW_ID_HV        NUMBER,
        STUDENT_ID       NUMBER,
        USERNAME         VARCHAR2(128),
        ROLE_ID          NUMBER,
        ROLE_NAME        NVARCHAR2(100),
        CLIENT_IDENTIFIER VARCHAR2(128),
        ACTION_TS        TIMESTAMP DEFAULT SYSTIMESTAMP,
        IP_ADDRESS       VARCHAR2(64),
        MODULE           VARCHAR2(64),
        PROGRAM          VARCHAR2(64),
        CHANGED_COLUMNS  VARCHAR2(4000),
        OLD_DATA         CLOB,
        NEW_DATA         CLOB
      )]';
  END IF;
  SELECT COUNT(*) INTO v_cnt FROM user_sequences WHERE sequence_name = 'HOC_VIEN_AUDIT_SEQ';
  IF v_cnt = 0 THEN
    EXECUTE IMMEDIATE 'CREATE SEQUENCE HOC_VIEN_AUDIT_SEQ START WITH 1 INCREMENT BY 1 NOCACHE';
  END IF;
END;
/

-- 3) Row-level trigger: only logs when current session is Student
BEGIN
  EXECUTE IMMEDIATE 'DROP TRIGGER TR_AUD_HOC_VIEN_DML';
EXCEPTION WHEN OTHERS THEN NULL; END;
/

CREATE OR REPLACE TRIGGER TR_AUD_HOC_VIEN_DML
BEFORE INSERT OR UPDATE OR DELETE ON HOC_VIEN
FOR EACH ROW
DECLARE
  v_is_student NUMBER := 0;
  v_username   VARCHAR2(128) := SYS_CONTEXT('USERENV','SESSION_USER');
  v_client     VARCHAR2(128) := SYS_CONTEXT('USERENV','CLIENT_IDENTIFIER');
  v_module     VARCHAR2(64)  := SYS_CONTEXT('USERENV','MODULE');
  v_prog       VARCHAR2(64)  := SYS_CONTEXT('USERENV','PROGRAM');
  v_ip         VARCHAR2(64)  := SYS_CONTEXT('USERENV','IP_ADDRESS');
  v_role_id    NUMBER;
  v_role_name  NVARCHAR2(100);
  v_student_id NUMBER;
  v_changed    VARCHAR2(4000);
  v_old        CLOB;
  v_new        CLOB;
  v_action     VARCHAR2(10);
  v_row_id     NUMBER;
  PROCEDURE detect_role IS
  BEGIN
    v_is_student := IS_STUDENT_CTX();
    IF v_is_student = 1 THEN
      BEGIN
        SELECT tk.ID_VAI_TRO, vt.TEN_VAI_TRO, tk.ID_NGUOI_DUNG
          INTO v_role_id, v_role_name, v_student_id
          FROM QLTT_ADMIN.TAI_KHOAN tk
          JOIN QLTT_ADMIN.VAI_TRO vt ON vt.ID_VAI_TRO = tk.ID_VAI_TRO
         WHERE UPPER(tk.TEN_DANG_NHAP) = UPPER(COALESCE(v_client, v_username))
           AND tk.TRANG_THAI_KICH_HOAT = 1;
      EXCEPTION WHEN NO_DATA_FOUND THEN
        v_is_student := 0;
      END;
    END IF;
  END;
BEGIN
  detect_role;
  IF v_is_student <> 1 THEN
    RETURN; -- no logging for non-students
  END IF;

  -- Build changed columns (for UPDATE only) -- exclude EMAIL to be schema-agnostic
  IF UPDATING THEN
    v_changed := NULL;
    IF NVL(:OLD.HO_TEN, '¤')       <> NVL(:NEW.HO_TEN, '¤')       THEN v_changed := v_changed||'HO_TEN,'; END IF;
    IF NVL(:OLD.SO_DIEN_THOAI,'¤') <> NVL(:NEW.SO_DIEN_THOAI,'¤') THEN v_changed := v_changed||'SO_DIEN_THOAI,'; END IF;
    IF NVL(:OLD.DIA_CHI, '¤')      <> NVL(:NEW.DIA_CHI, '¤')      THEN v_changed := v_changed||'DIA_CHI,'; END IF;
    IF NVL(:OLD.GIOI_TINH,'¤')     <> NVL(:NEW.GIOI_TINH,'¤')     THEN v_changed := v_changed||'GIOI_TINH,'; END IF;
    IF NVL(:OLD.NGAY_SINH, DATE '0001-01-01') <> NVL(:NEW.NGAY_SINH, DATE '0001-01-01') THEN v_changed := v_changed||'NGAY_SINH,'; END IF;
    IF v_changed IS NOT NULL THEN v_changed := RTRIM(v_changed, ','); END IF;
  END IF;

  -- Produce OLD/NEW as JSON (works in 21c)
  IF INSERTING THEN
    SELECT JSON_OBJECT('HO_TEN' VALUE :NEW.HO_TEN,
                       'SO_DIEN_THOAI' VALUE :NEW.SO_DIEN_THOAI,
                       'DIA_CHI' VALUE :NEW.DIA_CHI, 'GIOI_TINH' VALUE :NEW.GIOI_TINH,
                       'NGAY_SINH' VALUE TO_CHAR(:NEW.NGAY_SINH,'YYYY-MM-DD'))
      INTO v_new FROM dual;
  ELSIF UPDATING THEN
    SELECT JSON_OBJECT('HO_TEN' VALUE :OLD.HO_TEN,
                       'SO_DIEN_THOAI' VALUE :OLD.SO_DIEN_THOAI,
                       'DIA_CHI' VALUE :OLD.DIA_CHI, 'GIOI_TINH' VALUE :OLD.GIOI_TINH,
                       'NGAY_SINH' VALUE TO_CHAR(:OLD.NGAY_SINH,'YYYY-MM-DD'))
      INTO v_old FROM dual;
    SELECT JSON_OBJECT('HO_TEN' VALUE :NEW.HO_TEN,
                       'SO_DIEN_THOAI' VALUE :NEW.SO_DIEN_THOAI,
                       'DIA_CHI' VALUE :NEW.DIA_CHI, 'GIOI_TINH' VALUE :NEW.GIOI_TINH,
                       'NGAY_SINH' VALUE TO_CHAR(:NEW.NGAY_SINH,'YYYY-MM-DD'))
      INTO v_new FROM dual;
  ELSIF DELETING THEN
    SELECT JSON_OBJECT('HO_TEN' VALUE :OLD.HO_TEN,
                       'SO_DIEN_THOAI' VALUE :OLD.SO_DIEN_THOAI,
                       'DIA_CHI' VALUE :OLD.DIA_CHI, 'GIOI_TINH' VALUE :OLD.GIOI_TINH,
                       'NGAY_SINH' VALUE TO_CHAR(:OLD.NGAY_SINH,'YYYY-MM-DD'))
      INTO v_old FROM dual;
  END IF;

  -- Determine action and row id for the audit row (can't use INSERTING/UPDATING inside SQL)
  IF INSERTING THEN
    v_action := 'INSERT';
    v_row_id := :NEW.ID_HOC_VIEN;
  ELSIF UPDATING THEN
    v_action := 'UPDATE';
    v_row_id := :OLD.ID_HOC_VIEN;
  ELSIF DELETING THEN
    v_action := 'DELETE';
    v_row_id := :OLD.ID_HOC_VIEN;
  END IF;

  INSERT INTO HOC_VIEN_AUDIT(
    AUDIT_ID, ACTION, ROW_ID_HV, STUDENT_ID, USERNAME, ROLE_ID, ROLE_NAME,
    CLIENT_IDENTIFIER, ACTION_TS, IP_ADDRESS, MODULE, PROGRAM,
    CHANGED_COLUMNS, OLD_DATA, NEW_DATA)
  VALUES(
    HOC_VIEN_AUDIT_SEQ.NEXTVAL,
    v_action,
    v_row_id,
    v_student_id, v_username, v_role_id, v_role_name,
    v_client, SYSTIMESTAMP, v_ip, v_module, v_prog,
    v_changed, v_old, v_new);
END;
/
SHOW ERRORS TRIGGER TR_AUD_HOC_VIEN_DML

-- 4) FGA policy: audit only when IS_STUDENT_CTX() = 1
BEGIN
  DBMS_FGA.DROP_POLICY(object_schema => 'QLTT_ADMIN', object_name => 'HOC_VIEN', policy_name => 'FGA_HV_STUDENT');
EXCEPTION WHEN OTHERS THEN NULL; END;
/

BEGIN
  DBMS_FGA.ADD_POLICY(
    object_schema   => 'QLTT_ADMIN',
    object_name     => 'HOC_VIEN',
    policy_name     => 'FGA_HV_STUDENT',
    audit_condition => 'QLTT_ADMIN.IS_STUDENT_CTX() = 1',
    statement_types => 'SELECT,UPDATE,DELETE',
    audit_trail     => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column    => NULL
  );
  DBMS_OUTPUT.PUT_LINE('Added FGA policy FGA_HV_STUDENT on HOC_VIEN (student-only).');
END;
/

-- 5) Quick verification cheatsheet
--   a) DML history (trigger):
--      SELECT * FROM QLTT_ADMIN.HOC_VIEN_AUDIT ORDER BY ACTION_TS DESC;
--   b) FGA logs:
--      SELECT DB_USER, OBJ_NAME, POLICY_NAME, SCN, SQL_TEXT, SQL_BIND, NTIMESTAMP#
--      FROM   SYS.FGA_LOG$ WHERE OBJ$SCHEMA = 'QLTT_ADMIN' AND OBJ_NAME = 'HOC_VIEN'
--      ORDER BY NTIMESTAMP# DESC;
--      -- or
--      SELECT * FROM DBA_FGA_AUDIT_TRAIL WHERE OBJECT_SCHEMA='QLTT_ADMIN' AND OBJECT_NAME='HOC_VIEN' ORDER BY TIMESTAMP DESC;
