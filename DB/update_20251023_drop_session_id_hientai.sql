-- Drop legacy SESSION_ID_HIENTAI column from TAI_KHOAN (Oracle)
-- Safe pattern with existence check and exception handling

declare
   v_count number;
begin
   select count(*)
     into v_count
     from user_tab_columns
    where table_name = 'TAI_KHOAN'
      and column_name = 'SESSION_ID_HIENTAI';

   if v_count > 0 then
      execute immediate 'ALTER TABLE TAI_KHOAN DROP COLUMN SESSION_ID_HIENTAI';
   end if;
exception
   when others then
    -- Loggable block; ignore ORA-01430/ORA-00904 etc. to be idempotent
      null;
end;
/

-- Ensure per-device columns exist (no-op if already present)
declare
   v_pc number;
   v_mb number;
begin
   select count(*)
     into v_pc
     from user_tab_columns
    where table_name = 'TAI_KHOAN'
      and column_name = 'SESSION_ID_PC';
   if v_pc = 0 then
      execute immediate 'ALTER TABLE TAI_KHOAN ADD (SESSION_ID_PC VARCHAR2(64))';
      execute immediate 'CREATE INDEX IDX_TK_SESSION_PC ON TAI_KHOAN(SESSION_ID_PC)';
   end if;
   select count(*)
     into v_mb
     from user_tab_columns
    where table_name = 'TAI_KHOAN'
      and column_name = 'SESSION_ID_MOBILE';
   if v_mb = 0 then
      execute immediate 'ALTER TABLE TAI_KHOAN ADD (SESSION_ID_MOBILE VARCHAR2(64))';
      execute immediate 'CREATE INDEX IDX_TK_SESSION_MOBILE ON TAI_KHOAN(SESSION_ID_MOBILE)';
   end if;
exception
   when others then
      null;
end;
/