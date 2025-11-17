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

-- Bổ sung quyền cho vai trò Học viên (cho phép xem hóa đơn và các bảng liên quan)
-- Lưu ý: Các truy vấn API đang dùng kết nối theo USER và join trực tiếp các bảng DON_DANG_KY, HOA_DON,
-- LOP_HOC, KHOA_HOC, HOC_VIEN và subquery TAI_KHOAN; vì vậy cần cấp SELECT tối thiểu cho các bảng này.
grant select on qltt_admin.don_dang_ky to role_hocvien;
grant select on qltt_admin.hoa_don     to role_hocvien;
grant select on qltt_admin.khoa_hoc    to role_hocvien;
grant select on qltt_admin.hoc_vien    to role_hocvien;
-- Subquery: (SELECT ID_NGUOI_DUNG FROM TAI_KHOAN WHERE UPPER(TEN_DANG_NHAP)=USER)
-- Không thể cấp SELECT theo cột trong Oracle, tạm thời cấp SELECT trên bảng để hoạt động trong môi trường dev
grant select on qltt_admin.tai_khoan   to role_hocvien;
grant select on qltt_admin.TTTA   to role_hocvien;
-- Bước 3: Cấp quyền cho vai trò Nhân viên học vụ (role_nhanvienhocvu)
grant select,insert,update,delete on qltt_admin.khoa_hoc to role_nhanvienhocvu;
grant select,insert,update,delete on qltt_admin.lop_hoc to role_nhanvienhocvu;
grant select,insert,update,delete on qltt_admin.lich_hoc to role_nhanvienhocvu;
grant select,update on qltt_admin.don_dang_ky to role_nhanvienhocvu;
grant select on qltt_admin.v_danhsach_khoahoc to role_nhanvienhocvu;

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
grant role_ketoan to THAITHUAN;
alter user thaithuan default role all;
grant select on qltt_admin.khoa_hoc to role_ketoan;
grant select on qltt_admin.lop_hoc to role_ketoan;
grant select on qltt_admin.hoc_vien to role_ketoan;
grant select on qltt_admin.tai_khoan to role_ketoan;
grant select on qltt_admin.don_dang_ky to role_ketoan;
--Tạo view tối giản:
CREATE OR REPLACE VIEW qltt_admin.KE_TOAN_PUB AS SELECT ID_KE_TOAN, HO_TEN, KHOA_CONG_PEM FROM qltt_admin.KE_TOAN;
--Cấp quyền cho các role đang dùng web:
GRANT SELECT ON qltt_admin.KE_TOAN_PUB TO role_ketoan; GRANT SELECT ON qltt_admin.KE_TOAN_PUB TO role_hocvien;
--ạo synonym để các user kết nối (per-user) gọi được tên KE_TOAN_PUB:
CREATE PUBLIC SYNONYM KE_TOAN_PUB FOR qltt_admin.KE_TOAN_PUB;
--Đảm bảo học viên xem được hóa đơn:
GRANT SELECT ON qltt_admin.HOA_DON TO role_hocvien;
-- Cấp quyền quản trị đầy đủ
GRANT DBA TO QLTTTA_ADMIN;

-- (Tùy chọn) Cho phép QLTTTA_ADMIN được cấp lại quyền cho người khác
GRANT DBA TO QLTTTA_ADMIN WITH ADMIN OPTION;
commit;