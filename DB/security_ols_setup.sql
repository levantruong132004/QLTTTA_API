-- ============================================================================
-- Oracle Label Security (OLS) setup for student data
-- NOTE: Requires Oracle Label Security to be installed and licensed.
-- Run as LBACSYS (or user with OLS admin privileges). Adjust object owners as needed.
-- This script is idempotent-ish; drop operations are guarded when possible.
-- Policy name: PV_OLS_STUDENT
-- Protected tables: QLTT_ADMIN.HOC_VIEN (optionally DON_DANG_KY/HOA_DON later)
-- Labels:
--   - STAFF: Visible to academic staff (NhanVienHocVu) and Admin/KeToan as needed
--   - PUBLIC: Default label for non-sensitive student rows
-- Strategy:
--   - Add policy and levels
--   - Apply policy to HOC_VIEN with default label = PUBLIC
--   - Grant QLTT_ADMIN the ability to set labels on rows
--   - Application will set access profile to STAFF for staff users
-- ============================================================================

   set serveroutput on

-- One-block installer: if OLS is not installed/enabled, print message and skip gracefully
declare
   v_installed varchar2(5);
   v_has_ols   boolean := false;
begin
  -- Detect OLS option
   begin
      select case
                when value = 'TRUE' then
                   'TRUE'
                else
                   'FALSE'
             end
        into v_installed
        from v$option
       where parameter = 'Oracle Label Security';
   exception
      when others then
         v_installed := 'FALSE';
   end;

   if v_installed = 'TRUE' then
      v_has_ols := true;
      dbms_output.put_line('INFO: Oracle Label Security appears to be installed. Proceeding to configure policy.');
   else
      dbms_output.put_line('WARNING: Oracle Label Security is NOT installed/enabled on this database.');
      dbms_output.put_line('ACTION: Enable OLS option, run catols.sql as SYS/LBACSYS, then rerun this script.');
   end if;

   if v_has_ols then
    -- Try drop existing policy (ignore errors)
      begin
         sa_sysdba.drop_policy(policy_name => 'PV_OLS_STUDENT');
      exception
         when others then
            null;
      end;

    -- Create policy with label column
      sa_sysdba.create_policy(
         policy_name => 'PV_OLS_STUDENT',
         column_name => 'OLS_LABEL'
      );
      dbms_output.put_line('Created policy PV_OLS_STUDENT');

    -- Create level values (using range 10..100)
      begin
         sa_components.create_level(
            'PV_OLS_STUDENT',
            10,
            'PUBLIC',
            'PUBLIC'
         );
      exception
         when others then
            null;
      end;
      begin
         sa_components.create_level(
            'PV_OLS_STUDENT',
            50,
            'STAFF',
            'STAFF'
         );
      exception
         when others then
            null;
      end;

    -- Apply policy to table HOC_VIEN in QLTT_ADMIN with default PUBLIC label for all rows
      begin
         sa_policy_admin.apply_table_policy(
            policy_name   => 'PV_OLS_STUDENT',
            schema_name   => 'QLTT_ADMIN',
            table_name    => 'HOC_VIEN',
            table_options => sa_policy_admin.all_rows('PUBLIC')
         );
         dbms_output.put_line('Applied policy to QLTT_ADMIN.HOC_VIEN');
      exception
         when others then
            dbms_output.put_line('ERROR applying table policy on HOC_VIEN: ' || sqlerrm);
            raise;
      end;

    -- Grant QLTT_ADMIN label capabilities for this policy
      begin
         sa_user_admin.set_user_labels(
            policy_name    => 'PV_OLS_STUDENT',
            user_name      => 'QLTT_ADMIN',
            max_read_label => 'STAFF',
            min_read_label => 'PUBLIC',
            def_read_label => 'PUBLIC',
            row_label      => 'PUBLIC'
         );
         dbms_output.put_line('Granted labels to QLTT_ADMIN');
      exception
         when others then
            dbms_output.put_line('ERROR granting labels to QLTT_ADMIN: ' || sqlerrm);
            raise;
      end;

    -- Ensure app schema can set access profile and convert label text
      begin
         execute immediate 'GRANT EXECUTE ON SA_SESSION TO QLTT_ADMIN';
      exception
         when others then
            null;
      end;
      begin
         execute immediate 'GRANT EXECUTE ON SA_LABEL_ADMIN TO QLTT_ADMIN';
      exception
         when others then
            null;
      end;
   end if;
end;
/

-- To enable STAFF access at session time (from application):
--   BEGIN SA_SESSION.SET_ACCESS_PROFILE('PV_OLS_STUDENT', 'STAFF'); END;
-- To revert to default:
--   BEGIN SA_SESSION.CLEAR_LABELS('PV_OLS_STUDENT'); END;

-- Verify policy
-- SELECT * FROM DBA_SA_POLICIES WHERE POLICY_NAME = 'PV_OLS_STUDENT';
-- SELECT * FROM USER_TAB_COLUMNS WHERE COLUMN_NAME = 'OLS_LABEL' AND TABLE_NAME='HOC_VIEN';

-- Optional: Label management examples (run as QLTT_ADMIN after policy applied)
--   -- Elevate some rows to STAFF label (visible only when session access includes STAFF)
--   UPDATE QLTT_ADMIN.HOC_VIEN
--      SET OLS_LABEL = CHAR_TO_LABEL('PV_OLS_STUDENT', 'STAFF')
--    WHERE <your_condition_for_sensitive_rows>;
--   COMMIT;

-- Session usage examples (run in the same session as the app):
--   -- Grant STAFF visibility for academic staff users
--   BEGIN SA_SESSION.SET_ACCESS_PROFILE('PV_OLS_STUDENT', 'STAFF'); END;
--   -- Revert to defaults (PUBLIC only)
--   BEGIN SA_SESSION.CLEAR_LABELS('PV_OLS_STUDENT'); END;

-- Additional verification
-- SELECT * FROM DBA_SA_TABLE_POLICIES WHERE POLICY_NAME = 'PV_OLS_STUDENT' AND TABLE_NAME='HOC_VIEN';
-- SELECT LABEL, LONG_NAME FROM DBA_SA_LEVELS WHERE POLICY_NAME = 'PV_OLS_STUDENT';