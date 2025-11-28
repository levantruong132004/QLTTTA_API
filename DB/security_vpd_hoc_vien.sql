-- ============================================================================
-- VPD policy for HOC_VIEN by role
-- Usage: Run as QLTT_ADMIN (owner schema). Idempotent-ish: drops policy if exists.
-- Logic:
--  - Admin (QuanTriVienHeThong), Academic staff (NhanVienHocVu), Accountant (KeToan): see all rows
--  - Student (HocVien): see only own row
--  - Others: see nothing
--  - Identity source: CLIENT_IDENTIFIER if present; fallback to USER
-- ============================================================================
   set serveroutput on

-- Helper: try drop existing policy (ignore if not exists)
declare
   v_exists number;
begin
   select count(*)
     into v_exists
     from user_policies
    where object_name = 'HOC_VIEN'
      and policy_name = 'PV_HOC_VIEN_BY_ROLE';
   if v_exists > 0 then
      dbms_rls.drop_policy(
         object_schema => 'QLTT_ADMIN',
         object_name   => 'HOC_VIEN',
         policy_name   => 'PV_HOC_VIEN_BY_ROLE'
      );
      dbms_output.put_line('Dropped existing policy PV_HOC_VIEN_BY_ROLE');
   end if;
exception
   when others then
      dbms_output.put_line('Ignore drop policy error: ' || sqlerrm);
end;
/

create or replace function qltt_admin.vpd_pred_hoc_vien (
   p_schema in varchar2,
   p_object in varchar2
) return varchar2 as
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

	-- If not set by app, fallback to current session user
   if v_ident is null
   or length(trim(v_ident)) = 0 then
      v_ident := v_user;
   end if;

	-- Resolve account by username (case-insensitive)
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
			-- Unknown identity: deny all
         return '1=0';
   end;

	-- Admin / Nhân viên học vụ / Kế toán: allow all
   if v_role_name in ( N'QuanTriVienHeThong',
                       N'NhanVienHocVu',
                       N'KeToan' )
   or v_role_id in ( 3,
                     4,
                     5 ) then
      return '1=1';
   end if;

	-- Student: own row only
   if v_role_name in ( N'HocVien' )
   or v_role_id = 1 then
      return 'ID_HOC_VIEN = ' || v_user_id;
   end if;

	-- Others: deny
   return '1=0';
end;
/
SHOW ERRORS FUNCTION QLTT_ADMIN.VPD_PRED_HOC_VIEN

begin
   dbms_rls.add_policy(
      object_schema   => 'QLTT_ADMIN',
      object_name     => 'HOC_VIEN',
      policy_name     => 'PV_HOC_VIEN_BY_ROLE',
      function_schema => 'QLTT_ADMIN',
      policy_function => 'VPD_PRED_HOC_VIEN',
      statement_types => 'SELECT,UPDATE,DELETE',
      update_check    => true,
      enable          => true
   );
   dbms_output.put_line('Added policy PV_HOC_VIEN_BY_ROLE on HOC_VIEN');
end;
/

-- Quick sanity checks (optional)
-- SELECT * FROM USER_POLICIES WHERE OBJECT_NAME = 'HOC_VIEN';
-- SELECT QLTT_ADMIN.VPD_PRED_HOC_VIEN(USER, 'HOC_VIEN') FROM DUAL;