-- Bước 1: Tạo các role cần thiết
create role role_hocvien;
create role role_nhanvienhocvu;
create role role_ketoan;

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
grant select on qltt_admin.v_danhsach_khoahoc to role_nhanvienhocvu;

-- Thay đổi tài khoản HV thành Kế toán
UPDATE TAI_KHOAN
SET ID_VAI_TRO = (
    SELECT ID_VAI_TRO FROM VAI_TRO WHERE TEN_VAI_TRO = 'KeToan'
)
WHERE TEN_DANG_NHAP = 'minhtien';
COMMIT;

-- Vai trò Kế toán
grant select on qltt_admin.don_dang_ky to role_ketoan;           -- kiểm tra trạng thái đơn
grant select, insert, update on qltt_admin.hoa_don to role_ketoan; -- tạo/xem/sửa trạng thái hóa đơn
-- Quyền bổ sung tùy chọn cho hiển thị thông tin
grant role_ketoan to MINHTIEN;
alter user minhtien default role all;
grant select on qltt_admin.khoa_hoc to role_ketoan;
grant select on qltt_admin.lop_hoc to role_ketoan;
grant select on qltt_admin.hoc_vien to role_ketoan;
grant select on qltt_admin.tai_khoan to role_ketoan;
grant select on qltt_admin.don_dang_ky to role_ketoan;