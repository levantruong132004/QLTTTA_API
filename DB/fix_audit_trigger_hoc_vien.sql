-- Fix / harden audit trigger TR_AUD_HOC_VIEN_DML to avoid ORA-02003
-- Run as QLTT_ADMIN
-- Steps:
--   1) (Optional) Disable old trigger: ALTER TRIGGER TR_AUD_HOC_VIEN_DML DISABLE;
--   2) Create this safer version (wrap SYS_CONTEXT in exception handlers)
--   3) Test registration INSERT into HOC_VIEN
--   4) Re-enable if previously disabled: ALTER TRIGGER TR_AUD_HOC_VIEN_DML ENABLE;

CREATE OR REPLACE TRIGGER TR_AUD_HOC_VIEN_DML
BEFORE INSERT OR UPDATE OR DELETE ON HOC_VIEN
FOR EACH ROW
DECLARE
  v_is_student NUMBER := 0;
  v_username   VARCHAR2(128);
  v_client     VARCHAR2(128);
  v_module     VARCHAR2(64);
  v_prog       VARCHAR2(64);
  v_ip         VARCHAR2(64);
  v_role_id    NUMBER;
  v_role_name  NVARCHAR2(100);
  v_student_id NUMBER;
  v_changed    VARCHAR2(4000);
  v_old        CLOB;
  v_new        CLOB;
  v_action     VARCHAR2(10);
  v_row_id     NUMBER;

  PROCEDURE safe_sys_context(p_attr VARCHAR2, p_out OUT VARCHAR2) IS
  BEGIN
    BEGIN
      p_out := SYS_CONTEXT('USERENV', p_attr);
    EXCEPTION WHEN OTHERS THEN
      p_out := NULL; -- swallow invalid USERENV attribute (prevents ORA-02003)
    END;
  END;

  PROCEDURE detect_role IS
    v_ident VARCHAR2(128);
  BEGIN
    safe_sys_context('CLIENT_IDENTIFIER', v_client);
    safe_sys_context('SESSION_USER',      v_username);
    -- Fallback chain
    v_ident := NVL(TRIM(v_client), TRIM(v_username));
    IF v_ident IS NULL THEN
      v_is_student := 0;
      RETURN;
    END IF;
    BEGIN
      SELECT tk.ID_VAI_TRO, vt.TEN_VAI_TRO, tk.ID_NGUOI_DUNG
        INTO v_role_id, v_role_name, v_student_id
        FROM QLTT_ADMIN.TAI_KHOAN tk
        JOIN QLTT_ADMIN.VAI_TRO vt ON vt.ID_VAI_TRO = tk.ID_VAI_TRO
       WHERE UPPER(tk.TEN_DANG_NHAP) = UPPER(v_ident)
         AND tk.TRANG_THAI_KICH_HOAT = 1;
      IF v_role_id = 1 THEN
        v_is_student := 1;
      ELSE
        v_is_student := 0;
      END IF;
    EXCEPTION WHEN NO_DATA_FOUND THEN
      v_is_student := 0;
    WHEN OTHERS THEN
      v_is_student := 0; -- any unexpected error => treat as non-student to avoid blocking DML
    END;
  END;
BEGIN
  -- Collect optional context (do not block if unavailable)
  safe_sys_context('MODULE',     v_module);
  safe_sys_context('PROGRAM',    v_prog);
  safe_sys_context('IP_ADDRESS', v_ip);

  detect_role;
  -- Removed student-only check to audit all users (Staff, Admin, Student)
  -- IF v_is_student <> 1 THEN
  --   RETURN; 
  -- END IF;

  -- For UPDATE build changed columns list
  IF UPDATING THEN
    v_changed := NULL;
    IF NVL(:OLD.HO_TEN, '¤')       <> NVL(:NEW.HO_TEN, '¤')       THEN v_changed := v_changed||'HO_TEN,'; END IF;
    IF NVL(:OLD.SO_DIEN_THOAI,'¤') <> NVL(:NEW.SO_DIEN_THOAI,'¤') THEN v_changed := v_changed||'SO_DIEN_THOAI,'; END IF;
    IF NVL(:OLD.DIA_CHI, '¤')      <> NVL(:NEW.DIA_CHI, '¤')      THEN v_changed := v_changed||'DIA_CHI,'; END IF;
    IF NVL(:OLD.GIOI_TINH,'¤')     <> NVL(:NEW.GIOI_TINH,'¤')     THEN v_changed := v_changed||'GIOI_TINH,'; END IF;
    IF NVL(:OLD.NGAY_SINH, DATE '0001-01-01') <> NVL(:NEW.NGAY_SINH, DATE '0001-01-01') THEN v_changed := v_changed||'NGAY_SINH,'; END IF;
    IF v_changed IS NOT NULL THEN v_changed := RTRIM(v_changed, ','); END IF;
  END IF;

  -- Build JSON snapshots (guard with EXCEPTION so failure doesn't abort DML)
  BEGIN
    IF INSERTING THEN
      SELECT JSON_OBJECT('HO_TEN' VALUE :NEW.HO_TEN,
                         'SO_DIEN_THOAI' VALUE :NEW.SO_DIEN_THOAI,
                         'DIA_CHI' VALUE :NEW.DIA_CHI,
                         'GIOI_TINH' VALUE :NEW.GIOI_TINH,
                         'NGAY_SINH' VALUE TO_CHAR(:NEW.NGAY_SINH,'YYYY-MM-DD'))
        INTO v_new FROM dual;
    ELSIF UPDATING THEN
      SELECT JSON_OBJECT('HO_TEN' VALUE :OLD.HO_TEN,
                         'SO_DIEN_THOAI' VALUE :OLD.SO_DIEN_THOAI,
                         'DIA_CHI' VALUE :OLD.DIA_CHI,
                         'GIOI_TINH' VALUE :OLD.GIOI_TINH,
                         'NGAY_SINH' VALUE TO_CHAR(:OLD.NGAY_SINH,'YYYY-MM-DD'))
        INTO v_old FROM dual;
      SELECT JSON_OBJECT('HO_TEN' VALUE :NEW.HO_TEN,
                         'SO_DIEN_THOAI' VALUE :NEW.SO_DIEN_THOAI,
                         'DIA_CHI' VALUE :NEW.DIA_CHI,
                         'GIOI_TINH' VALUE :NEW.GIOI_TINH,
                         'NGAY_SINH' VALUE TO_CHAR(:NEW.NGAY_SINH,'YYYY-MM-DD'))
        INTO v_new FROM dual;
    ELSIF DELETING THEN
      SELECT JSON_OBJECT('HO_TEN' VALUE :OLD.HO_TEN,
                         'SO_DIEN_THOAI' VALUE :OLD.SO_DIEN_THOAI,
                         'DIA_CHI' VALUE :OLD.DIA_CHI,
                         'GIOI_TINH' VALUE :OLD.GIOI_TINH,
                         'NGAY_SINH' VALUE TO_CHAR(:OLD.NGAY_SINH,'YYYY-MM-DD'))
        INTO v_old FROM dual;
    END IF;
  EXCEPTION WHEN OTHERS THEN
    v_old := NULL; v_new := NULL; -- if JSON_OBJECT unsupported => skip
  END;

  -- Action/row id
  IF INSERTING THEN
    v_action := 'INSERT'; v_row_id := :NEW.ID_HOC_VIEN;
  ELSIF UPDATING THEN
    v_action := 'UPDATE'; v_row_id := :OLD.ID_HOC_VIEN;
  ELSIF DELETING THEN
    v_action := 'DELETE'; v_row_id := :OLD.ID_HOC_VIEN;
  END IF;

  -- Insert audit row (ignore errors)
  BEGIN
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
  EXCEPTION WHEN OTHERS THEN NULL; END;
END;
/