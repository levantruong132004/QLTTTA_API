-- Grant fixes for per-user connections (Nov 16, 2025)
-- Apply under schema owner (QLTT_ADMIN) or a DBA account.

-- Học viên (role_hocvien): bổ sung xem lịch học khi xem danh sách lớp mở
grant select on qltt_admin.lich_hoc to role_hocvien;

-- Nhân viên học vụ (role_nhanvienhocvu): cần xem học viên, tài khoản, hóa đơn
grant select on qltt_admin.hoc_vien to role_nhanvienhocvu;
grant select on qltt_admin.tai_khoan to role_nhanvienhocvu;
grant select on qltt_admin.hoa_don to role_nhanvienhocvu;

-- Kế toán (role_ketoan): dùng bảng payments và phieu_thanh_toan
grant select,insert,update on qltt_admin.payments to role_ketoan;
grant select,insert,update on qltt_admin.phieu_thanh_toan to role_ketoan;

-- (Khuyến nghị) Tạo role quản trị vận hành trang nhân sự nếu chưa có
-- Lưu ý: nếu đã có role tương đương, hãy điều chỉnh tên role tại đây.
-- CREATE ROLE role_quantri;
grant select,insert,update,delete on qltt_admin.tai_khoan to role_quantri;
grant select,insert,update,delete on qltt_admin.nhan_vien_hoc_vu to role_quantri;
grant select,insert,update,delete on qltt_admin.ke_toan to role_quantri;
grant select on qltt_admin.vai_tro to role_quantri;

-- (Tùy chọn) Nếu không dùng role_quantri, có thể cấp trực tiếp cho user admin
-- GRANT role_quantri TO <ORACLE_ADMIN_USERNAME>;
-- ALTER USER <ORACLE_ADMIN_USERNAME> DEFAULT ROLE ALL;

-- Kiểm tra nhanh quyền cốt lõi cho các truy vấn phổ biến
-- SELECT COUNT(*) FROM QLTT_ADMIN.KHOA_HOC;
-- SELECT COUNT(*) FROM QLTT_ADMIN.LOP_HOC;
-- SELECT COUNT(*) FROM QLTT_ADMIN.DON_DANG_KY;
-- SELECT COUNT(*) FROM QLTT_ADMIN.HOA_DON;
-- SELECT COUNT(*) FROM QLTT_ADMIN.HOC_VIEN;
-- SELECT COUNT(*) FROM QLTT_ADMIN.TAI_KHOAN;