-- Add per-device session columns to TAI_KHOAN
-- Run this script as the schema owner (e.g., QLTT_ADMIN)

alter table tai_khoan add (
   session_id_pc     varchar2(64),
   session_id_mobile varchar2(64)
);

-- Optional: clear old single-session column if exists
-- ALTER TABLE TAI_KHOAN DROP COLUMN SESSION_ID_HIENTAI;

-- Add indexes to speed up lookup by user and session
create index idx_tai_khoan_session_pc on
   tai_khoan (
      id_nguoi_dung,
      session_id_pc
   );
create index idx_tai_khoan_session_mobile on
   tai_khoan (
      id_nguoi_dung,
      session_id_mobile
   );

-- Note: If table already has these columns, wrap with conditional checks in your migration tool.