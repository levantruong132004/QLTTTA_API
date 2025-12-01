-- Grant UPDATE permission on HOC_VIEN to ROLE_HOCVIEN
-- Required for ProfileService to update student profile using student's own connection.

GRANT UPDATE ON QLTT_ADMIN.HOC_VIEN TO ROLE_HOCVIEN;
COMMIT;
