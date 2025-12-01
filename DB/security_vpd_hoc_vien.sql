PROMPT VPD policy created: QLTT_ADMIN.PV_HOC_VIEN_BY_ROLE on HOC_VIEN

-- How to test (run as different users after the app sets CLIENT_IDENTIFIER):
-- 1) As ROLE_ID=4 or 5: SELECT * FROM QLTT_ADMIN.HOC_VIEN; -- returns all rows
-- 2) As ROLE_ID=1 (student): SELECT * FROM QLTT_ADMIN.HOC_VIEN; -- returns only one row with ID_HOC_VIEN = your ID_NGUOI_DUNG
-- 3) As other roles: query returns no rows

-- Rollback:
-- BEGIN
--   DBMS_RLS.DROP_POLICY('QLTT_ADMIN','HOC_VIEN','PV_HOC_VIEN_BY_ROLE');
-- END;
-- /

SELECT policy_name, object_owner, object_name
FROM dba_policies
WHERE object_owner = 'QLTT_ADMIN' AND object_name = 'HOC_VIEN';

BEGIN
DBMS_RLS.DROP_POLICY(
object_schema => 'QLTT_ADMIN',
object_name => 'HOC_VIEN',
policy_name => 'PV_HOC_VIEN_BY_ROLE'
);
END;

BEGIN
DBMS_RLS.ADD_POLICY(
object_schema => 'QLTT_ADMIN',
object_name => 'HOC_VIEN',
policy_name => 'PV_HOC_VIEN_BY_ROLE',
function_schema => 'QLTT_ADMIN',
policy_function => 'VPD_PRED_HOC_VIEN',
statement_types => 'SELECT,UPDATE,DELETE',
update_check => TRUE,
enable => TRUE
);
END;

@DB/security_vpd_hoc_vien.sql


-- ============================================================================
-- VPD policy for HOC_VIEN by role
-- Usage: Run as QLTT_ADMIN (owner schema). Idempotent-ish: drops policy if exists.
-- Logic:
--  - Admin (QuanTriVienHeThong) or Academic staff (NhanVienHocVu): see all rows
--  - Student (HocVien): see only own row
--  - Others: see nothing
--  - Identity source: CLIENT_IDENTIFIER if present; fallback to USER
-- ============================================================================
set serveroutput on

-- Helper: try drop existing policy (ignore if not exists)
DECLARE
  v_exists NUMBER;
BEGIN
  SELECT COUNT(*) INTO v_exists
    FROM USER_POLICIES
   WHERE OBJECT_NAME = 'HOC_VIEN'
     AND POLICY_NAME = 'PV_HOC_VIEN_BY_ROLE';
  IF v_exists > 0 THEN
    DBMS_RLS.DROP_POLICY(
      object_schema => 'QLTT_ADMIN',
      object_name   => 'HOC_VIEN',
      policy_name   => 'PV_HOC_VIEN_BY_ROLE');
    DBMS_OUTPUT.PUT_LINE('Dropped existing policy PV_HOC_VIEN_BY_ROLE');
  END IF;
EXCEPTION
  WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('Ignore drop policy error: '||SQLERRM);
END;
/

CREATE OR REPLACE FUNCTION QLTT_ADMIN.VPD_PRED_HOC_VIEN(
  p_schema IN VARCHAR2,
  p_object IN VARCHAR2
) RETURN VARCHAR2
AS
  v_ident       VARCHAR2(128);
  v_user        VARCHAR2(128);
  v_role_id     NUMBER;
  v_role_name   NVARCHAR2(100);
  v_user_id     NUMBER;
BEGIN
  v_ident := SYS_CONTEXT('USERENV','CLIENT_IDENTIFIER');
  v_user  := SYS_CONTEXT('USERENV','SESSION_USER');

  -- If not set by app, fallback to current session user
  IF v_ident IS NULL OR LENGTH(TRIM(v_ident)) = 0 THEN
    v_ident := v_user;
  END IF;

  -- Resolve account by username (case-insensitive)
  BEGIN
    SELECT tk.ID_NGUOI_DUNG, tk.ID_VAI_TRO, vt.TEN_VAI_TRO
      INTO v_user_id, v_role_id, v_role_name
      FROM QLTT_ADMIN.TAI_KHOAN tk
      JOIN QLTT_ADMIN.VAI_TRO vt ON vt.ID_VAI_TRO = tk.ID_VAI_TRO
     WHERE UPPER(tk.TEN_DANG_NHAP) = UPPER(v_ident)
       AND tk.TRANG_THAI_KICH_HOAT = 1;
  EXCEPTION
    WHEN NO_DATA_FOUND THEN
      -- Unknown identity: deny all
      RETURN '1=0';
  END;

  -- Admin or academic staff: allow all
  IF v_role_name IN (N'QuanTriVienHeThong', N'NhanVienHocVu') OR v_role_id IN (4,5) THEN
    RETURN '1=1';
  END IF;

  -- Student: own row only
  IF v_role_name IN (N'HocVien') OR v_role_id = 1 THEN
    RETURN 'ID_HOC_VIEN = ' || v_user_id;
  END IF;

  -- Others: deny
  RETURN '1=0';
END;
/
SHOW ERRORS FUNCTION QLTT_ADMIN.VPD_PRED_HOC_VIEN

BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => 'QLTT_ADMIN',
    object_name     => 'HOC_VIEN',
    policy_name     => 'PV_HOC_VIEN_BY_ROLE',
    function_schema => 'QLTT_ADMIN',
    policy_function => 'VPD_PRED_HOC_VIEN',
    statement_types => 'SELECT,INSERT,UPDATE,DELETE',
    update_check    => TRUE,
    enable          => TRUE
  );
  DBMS_OUTPUT.PUT_LINE('Added policy PV_HOC_VIEN_BY_ROLE on HOC_VIEN');
END;
/

-- Quick sanity checks (optional)
-- SELECT * FROM USER_POLICIES WHERE OBJECT_NAME = 'HOC_VIEN';
-- SELECT QLTT_ADMIN.VPD_PRED_HOC_VIEN(USER, 'HOC_VIEN') FROM DUAL;
