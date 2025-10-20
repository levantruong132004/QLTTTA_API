-- Bước 1: Tạo các role cần thiết
create role role_hocvien;
create role role_nhanvienhocvu;

-- Bước 2: Cấp quyền cho vai trò Học viên (role_hocvien)
grant select on v_thongtin_canhan_hv to role_hocvien;
grant select on qltt_admin.v_danhsach_khoahoc to role_hocvien;
grant select on qltt_admin.lop_hoc to role_hocvien;
grant insert on qltt_admin.don_dang_ky to role_hocvien;
grant update ( so_dien_thoai,
               dia_chi ) on v_thongtin_canhan_hv to role_hocvien;
grant execute on sp_gui_yeu_cau_dang_ky_khoa to role_hocvien;
grant insert on don_dang_ky to role_hocvien; -- Cho phép học viên tự tạo đơn đăng ký

-- Bước 3: Cấp quyền cho vai trò Nhân viên học vụ (role_nhanvienhocvu)
grant select,insert,update,delete on qltt_admin.khoa_hoc to role_nhanvienhocvu;
grant select,insert,update,delete on qltt_admin.lop_hoc to role_nhanvienhocvu;
grant select,insert,update,delete on qltt_admin.lich_hoc to role_nhanvienhocvu;
grant select,update on qltt_admin.don_dang_ky to role_nhanvienhocvu;