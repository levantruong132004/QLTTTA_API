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
begin
   execute immediate 'DROP FUNCTION IS_STUDENT_CTX';
exception
   when others then
      null;
end;
/

create or replace function is_student_ctx return number
   authid current_user
as
   v_ident     varchar2(128);
   v_user      varchar2(128);
   v_role_id   number;
   v_role_name nvarchar2(100);
   v_user_id   number;
begin
   v_ident := sys_context(
      'USERENV',
      'CLIENT_IDENTIFIER'
   );
   v_user := sys_context(
      'USERENV',
      'SESSION_USER'
   );
   if v_ident is null
   or length(trim(v_ident)) = 0 then
      v_ident := v_user;
   end if;

   begin
      select tk.id_nguoi_dung,
             tk.id_vai_tro,
             vt.ten_vai_tro
        into
         v_user_id,
         v_role_id,
         v_role_name
        from qltt_admin.tai_khoan tk
        join qltt_admin.vai_tro vt
      on vt.id_vai_tro = tk.id_vai_tro
       where upper(tk.ten_dang_nhap) = upper(v_ident)
         and tk.trang_thai_kich_hoat = 1;
   exception
      when no_data_found then
         return 0; -- unknown -> not student
   end;

   if v_role_id = 1
   or upper(v_role_name) in ( 'HOCVIEN',
                              'HỌC VIÊN',
                              'HOC VIEN' ) then
      return 1; -- student
   end if;
   return 0; -- not student (staff/admin/accountant...)
end;
/
SHOW ERRORS FUNCTION IS_STUDENT_CTX

-- 2) Audit table + sequence for DML history
declare
   v_cnt number;
begin
   select count(*)
     into v_cnt
     from user_tables
    where table_name = 'HOC_VIEN_AUDIT';
   if v_cnt = 0 then
      execute immediate q'[
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
   end if;
   select count(*)
     into v_cnt
     from user_sequences
    where sequence_name = 'HOC_VIEN_AUDIT_SEQ';
   if v_cnt = 0 then
      execute immediate 'CREATE SEQUENCE HOC_VIEN_AUDIT_SEQ START WITH 1 INCREMENT BY 1 NOCACHE';
   end if;
end;
/

-- 3) Row-level trigger: only logs when current session is Student
begin
   execute immediate 'DROP TRIGGER TR_AUD_HOC_VIEN_DML';
exception
   when others then
      null;
end;
/

create or replace trigger tr_aud_hoc_vien_dml before
   insert or update or delete on hoc_vien
   for each row
declare
   v_is_student number := 0;
   v_username   varchar2(128) := sys_context(
      'USERENV',
      'SESSION_USER'
   );
   v_client     varchar2(128) := sys_context(
      'USERENV',
      'CLIENT_IDENTIFIER'
   );
   v_module     varchar2(64) := sys_context(
      'USERENV',
      'MODULE'
   );
   v_prog       varchar2(64) := sys_context(
      'USERENV',
      'PROGRAM'
   );
   v_ip         varchar2(64) := sys_context(
      'USERENV',
      'IP_ADDRESS'
   );
   v_role_id    number;
   v_role_name  nvarchar2(100);
   v_student_id number;
   v_changed    varchar2(4000);
   v_old        clob;
   v_new        clob;
   v_action     varchar2(10);
   v_row_id     number;
   procedure detect_role is
   begin
      v_is_student := is_student_ctx();
      if v_is_student = 1 then
         begin
            select tk.id_vai_tro,
                   vt.ten_vai_tro,
                   tk.id_nguoi_dung
              into
               v_role_id,
               v_role_name,
               v_student_id
              from qltt_admin.tai_khoan tk
              join qltt_admin.vai_tro vt
            on vt.id_vai_tro = tk.id_vai_tro
             where upper(tk.ten_dang_nhap) = upper(coalesce(
                  v_client,
                  v_username
               ))
               and tk.trang_thai_kich_hoat = 1;
         exception
            when no_data_found then
               v_is_student := 0;
         end;
      end if;
   end;
begin
   detect_role;
   if v_is_student <> 1 then
      return; -- no logging for non-students
   end if;

  -- Build changed columns (for UPDATE only) -- exclude EMAIL to be schema-agnostic
   if updating then
      v_changed := null;
      if nvl(
         :old.ho_ten,
         '¤'
      ) <> nvl(
         :new.ho_ten,
         '¤'
      ) then
         v_changed := v_changed || 'HO_TEN,';
      end if;
      if nvl(
         :old.so_dien_thoai,
         '¤'
      ) <> nvl(
         :new.so_dien_thoai,
         '¤'
      ) then
         v_changed := v_changed || 'SO_DIEN_THOAI,';
      end if;
      if nvl(
         :old.dia_chi,
         '¤'
      ) <> nvl(
         :new.dia_chi,
         '¤'
      ) then
         v_changed := v_changed || 'DIA_CHI,';
      end if;
      if nvl(
         :old.gioi_tinh,
         '¤'
      ) <> nvl(
         :new.gioi_tinh,
         '¤'
      ) then
         v_changed := v_changed || 'GIOI_TINH,';
      end if;
      if nvl(
         :old.ngay_sinh,
         date '0001-01-01'
      ) <> nvl(
         :new.ngay_sinh,
         date '0001-01-01'
      ) then
         v_changed := v_changed || 'NGAY_SINH,';
      end if;
      if v_changed is not null then
         v_changed := rtrim(
            v_changed,
            ','
         );
      end if;
   end if;

  -- Produce OLD/NEW as JSON (works in 21c)
   if inserting then
      select
         json_object(
            'HO_TEN' value :new.ho_ten,
                     'SO_DIEN_THOAI' value :new.so_dien_thoai,
                     'DIA_CHI' value :new.dia_chi,
                     'GIOI_TINH' value :new.gioi_tinh,
                     'NGAY_SINH' value to_char(
               :new.ngay_sinh,
               'YYYY-MM-DD'
            )
         )
        into v_new
        from dual;
   elsif updating then
      select
         json_object(
            'HO_TEN' value :old.ho_ten,
                     'SO_DIEN_THOAI' value :old.so_dien_thoai,
                     'DIA_CHI' value :old.dia_chi,
                     'GIOI_TINH' value :old.gioi_tinh,
                     'NGAY_SINH' value to_char(
               :old.ngay_sinh,
               'YYYY-MM-DD'
            )
         )
        into v_old
        from dual;
      select
         json_object(
            'HO_TEN' value :new.ho_ten,
                     'SO_DIEN_THOAI' value :new.so_dien_thoai,
                     'DIA_CHI' value :new.dia_chi,
                     'GIOI_TINH' value :new.gioi_tinh,
                     'NGAY_SINH' value to_char(
               :new.ngay_sinh,
               'YYYY-MM-DD'
            )
         )
        into v_new
        from dual;
   elsif deleting then
      select
         json_object(
            'HO_TEN' value :old.ho_ten,
                     'SO_DIEN_THOAI' value :old.so_dien_thoai,
                     'DIA_CHI' value :old.dia_chi,
                     'GIOI_TINH' value :old.gioi_tinh,
                     'NGAY_SINH' value to_char(
               :old.ngay_sinh,
               'YYYY-MM-DD'
            )
         )
        into v_old
        from dual;
   end if;

  -- Determine action and row id for the audit row (can't use INSERTING/UPDATING inside SQL)
   if inserting then
      v_action := 'INSERT';
      v_row_id := :new.id_hoc_vien;
   elsif updating then
      v_action := 'UPDATE';
      v_row_id := :old.id_hoc_vien;
   elsif deleting then
      v_action := 'DELETE';
      v_row_id := :old.id_hoc_vien;
   end if;

   insert into hoc_vien_audit (
      audit_id,
      action,
      row_id_hv,
      student_id,
      username,
      role_id,
      role_name,
      client_identifier,
      action_ts,
      ip_address,
      module,
      program,
      changed_columns,
      old_data,
      new_data
   ) values ( hoc_vien_audit_seq.nextval,
              v_action,
              v_row_id,
              v_student_id,
              v_username,
              v_role_id,
              v_role_name,
              v_client,
              systimestamp,
              v_ip,
              v_module,
              v_prog,
              v_changed,
              v_old,
              v_new );
end;
/
SHOW ERRORS TRIGGER TR_AUD_HOC_VIEN_DML

-- 4) FGA policy: audit only when IS_STUDENT_CTX() = 1
begin
   dbms_fga.drop_policy(
      object_schema => 'QLTT_ADMIN',
      object_name   => 'HOC_VIEN',
      policy_name   => 'FGA_HV_STUDENT'
   );
exception
   when others then
      null;
end;
/

begin
   dbms_fga.add_policy(
      object_schema   => 'QLTT_ADMIN',
      object_name     => 'HOC_VIEN',
      policy_name     => 'FGA_HV_STUDENT',
      audit_condition => 'QLTT_ADMIN.IS_STUDENT_CTX() = 1',
      statement_types => 'SELECT,UPDATE,DELETE',
      audit_trail     => dbms_fga.db + dbms_fga.extended,
      audit_column    => null
   );
   dbms_output.put_line('Added FGA policy FGA_HV_STUDENT on HOC_VIEN (student-only).');
end;
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

-- 4) FGA policy: audit only when IS_STUDENT_CTX() = 1
begin
   dbms_fga.drop_policy(
      object_schema => 'QLTT_ADMIN',
      object_name   => 'HOC_VIEN',
      policy_name   => 'FGA_HV_STUDENT'
   );
exception
   when others then
      null;
end;
/

begin
   dbms_fga.add_policy(
      object_schema   => 'QLTT_ADMIN',
      object_name     => 'HOC_VIEN',
      policy_name     => 'FGA_HV_STUDENT',
      audit_condition => 'QLTT_ADMIN.IS_STUDENT_CTX() = 1',
      statement_types => 'SELECT,UPDATE,DELETE',
      audit_trail     => dbms_fga.db + dbms_fga.extended,
      audit_column    => null
   );
   dbms_output.put_line('Added FGA policy FGA_HV_STUDENT on HOC_VIEN (student-only).');
end;
/