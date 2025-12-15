-- Grant permissions for Stored Procedures
--
-- ROLE_HOCVIEN
grant execute on qltt_admin.sp_get_my_registrations to role_hocvien;
grant execute on qltt_admin.sp_get_student_invoices to role_hocvien;
grant execute on qltt_admin.sp_get_courses to role_hocvien;
grant execute on qltt_admin.sp_get_course_by_id to role_hocvien;
grant execute on qltt_admin.sp_get_classes to role_hocvien;
grant execute on qltt_admin.sp_get_class_by_id to role_hocvien;
grant execute on qltt_admin.sp_get_schedule_by_class to role_hocvien;
grant execute on qltt_admin.sp_get_invoice_by_reg to role_hocvien;
grant execute on qltt_admin.sp_get_invoice_by_id to role_hocvien;
grant execute on qltt_admin.sp_get_invoice_student_info to role_hocvien;
grant execute on qltt_admin.sp_update_invoice_status to role_hocvien; -- For requesting payment confirmation

-- ROLE_NHANVIENHOCUHOCVU
grant execute on qltt_admin.sp_get_students to role_nhanvienhocuhocvu;
grant execute on qltt_admin.sp_get_student_by_id to role_nhanvienhocuhocvu;
grant execute on qltt_admin.sp_search_students to role_nhanvienhocvu;
grant execute on qltt_admin.sp_get_registrations to role_nhanvienhocvu;
grant execute on qltt_admin.sp_get_registration_by_id to role_nhanvienhocvu;
grant execute on qltt_admin.sp_approve_registration to role_nhanvienhocvu;
grant execute on qltt_admin.sp_reject_registration to role_nhanvienhocvu;
grant execute on qltt_admin.sp_find_staff_by_session to role_nhanvienhocvu;
grant execute on qltt_admin.sp_check_class_size to role_nhanvienhocvu;
grant execute on qltt_admin.sp_update_registration_class to role_nhanvienhocvu;
grant execute on qltt_admin.sp_update_class_status_full to role_nhanvienhocvu;
grant execute on qltt_admin.sp_staff_search_students to role_nhanvienhocvu;
grant execute on qltt_admin.sp_lock_account to role_nhanvienhocvu;
grant execute on qltt_admin.sp_unlock_account to role_nhanvienhocvu;
grant execute on qltt_admin.sp_get_student_registrations to role_nhanvienhocvu;
grant execute on qltt_admin.sp_get_student_invoices to role_nhanvienhocvu;
grant execute on qltt_admin.sp_create_invoice to role_nhanvienhocvu;
grant execute on qltt_admin.sp_get_invoice_by_reg to role_nhanvienhocvu;
grant execute on qltt_admin.sp_get_invoice_by_id to role_nhanvienhocvu;
grant execute on qltt_admin.sp_get_invoice_student_info to role_nhanvienhocvu;
grant execute on qltt_admin.sp_get_courses to role_nhanvienhocvu;
grant execute on qltt_admin.sp_get_course_by_id to role_nhanvienhocvu;
grant execute on qltt_admin.sp_get_classes to role_nhanvienhocvu;
grant execute on qltt_admin.sp_get_class_by_id to role_nhanvienhocvu;
grant execute on qltt_admin.sp_get_schedule_by_class to role_nhanvienhocvu;
grant execute on qltt_admin.sp_get_schedule_by_id to role_nhanvienhocvu;
grant execute on qltt_admin.sp_create_class to role_nhanvienhocvu;
grant execute on qltt_admin.sp_update_class to role_nhanvienhocvu;
grant execute on qltt_admin.sp_check_class_code_exists to role_nhanvienhocvu;
grant execute on qltt_admin.sp_create_course to role_nhanvienhocvu;
grant execute on qltt_admin.sp_update_course to role_nhanvienhocvu;
grant execute on qltt_admin.sp_check_course_code_exists to role_nhanvienhocvu;
grant execute on qltt_admin.sp_create_schedule to role_nhanvienhocvu;
grant execute on qltt_admin.sp_update_schedule to role_nhanvienhocvu;
grant execute on qltt_admin.sp_delete_schedule to role_nhanvienhocvu;
grant execute on qltt_admin.sp_delete_class to role_nhanvienhocvu;

-- ROLE_KETOAN
grant execute on qltt_admin.sp_get_pending_payments to role_ketoan;
grant execute on qltt_admin.sp_create_payment_receipt to role_ketoan;
grant execute on qltt_admin.sp_create_payment to role_ketoan;
grant execute on qltt_admin.sp_get_payments_by_invoice to role_ketoan;
grant execute on qltt_admin.sp_get_accountant_regs to role_ketoan;
grant execute on qltt_admin.sp_update_invoice_status to role_ketoan;
grant execute on qltt_admin.sp_get_invoice_by_id to role_ketoan;
grant execute on qltt_admin.sp_get_invoice_student_info to role_ketoan;
grant execute on qltt_admin.sp_get_invoice_by_reg to role_ketoan;
grant execute on qltt_admin.sp_get_accountant_reg_by_id to role_ketoan;

-- Grant permissions for Accountant (ROLE_KETOAN) to create and manage invoices
grant execute on sp_get_registration_by_id to role_ketoan;
grant execute on sp_create_invoice to role_ketoan;
grant execute on sp_update_invoice_status to role_ketoan;
grant execute on sp_mark_invoice_printed to role_ketoan;

-- Grant permissions for viewing courses (needed for Profile and other lists)
-- Grant to all roles who might need to see course lists
grant execute on sp_get_all_courses to role_ketoan;
--GRANT EXECUTE ON SP_GET_ALL_COURSES TO ROLE_GIANGVIEN;
grant execute on sp_get_all_courses to role_hocvien;
grant execute on sp_get_all_courses to role_nhanvienhocvu;

-- Ensure Accountant can view invoice details (already granted in previous step, but good to double check)
-- GRANT EXECUTE ON SP_GET_INVOICE_BY_REG TO ROLE_KETOAN;
-- GRANT EXECUTE ON SP_GET_INVOICE_STUDENT_INFO TO ROLE_KETOAN;
-- GRANT EXECUTE ON SP_GET_ACCOUNTANT_REG_BY_ID TO ROLE_KETOAN;

commit;

-- Comprehensive Grant Permissions for Accountant (ROLE_KETOAN)

-- 1. Course & Class Viewing
grant execute on sp_get_all_courses to role_ketoan;
grant execute on sp_get_classes to role_ketoan;

-- 2. Registration Management
grant execute on sp_get_accountant_regs to role_ketoan;
grant execute on sp_get_accountant_reg_by_id to role_ketoan;
grant execute on sp_get_registration_by_id to role_ketoan;

-- 3. Invoice Management
grant execute on sp_create_invoice to role_ketoan;
grant execute on sp_get_invoice_by_reg to role_ketoan;
grant execute on sp_get_invoice_by_id to role_ketoan;
grant execute on sp_update_invoice_status to role_ketoan;
grant execute on sp_mark_invoice_printed to role_ketoan;
grant execute on sp_get_invoice_student_info to role_ketoan;

-- 4. Payment Management (if applicable)
-- GRANT EXECUTE ON SP_CONFIRM_PAYMENT TO ROLE_KETOAN; -- If exists
-- Grant Course Management Permissions

-- Grant to ROLE_NHANVIEN (Academic Staff)
grant execute on sp_create_course to role_nhanvienhocvu;
grant execute on sp_update_course to role_nhanvienhocvu;
grant execute on sp_delete_course to role_nhanvienhocvu;


-- Tạo các role 
create role role_hocvien;
create role role_nhanvienhocvu;
create role role_ketoan
-- Cấp quyền cho vai trò Học viên (role_hocvien)
grant select on v_thongtin_canhan_hv to role_hocvien;
grant select on qltt_admin.lich_hoc to role_hocvien;
grant select on qltt_admin.hoc_vien to role_nhanvienhocvu;
grant select on qltt_admin.tai_khoan to role_nhanvienhocvu;
grant select on qltt_admin.hoa_don to role_nhanvienhocvu;
grant select on qltt_admin.v_danhsach_khoahoc to role_hocvien;
grant select on qltt_admin.lop_hoc to role_hocvien;
grant insert on qltt_admin.don_dang_ky to role_hocvien;
grant update ( so_dien_thoai,
               dia_chi ) on v_thongtin_canhan_hv to role_hocvien;
grant execute on sp_gui_yeu_cau_dang_ky_khoa to role_hocvien;
grant insert on don_dang_ky to role_hocvien; -- Cho phép học viên tự tạo đơn đăng ký
grant select on qltt_admin.lich_hoc to role_hocvien;
grant update ( trang_thai ) on qltt_admin.hoa_don to role_hocvien;
commit;


-- Cấp quyền cho vai trò Nhân viên học vụ (role_nhanvienhocvu)
grant select,insert,update,delete on qltt_admin.khoa_hoc to role_nhanvienhocvu;
grant select,insert,update,delete on qltt_admin.lop_hoc to role_nhanvienhocvu;
grant select,insert,update,delete on qltt_admin.lich_hoc to role_nhanvienhocvu;
grant select,update on qltt_admin.don_dang_ky to role_nhanvienhocvu;
grant select on qltt_admin.hoc_vien to role_nhanvienhocvu;
grant select on qltt_admin.tai_khoan to role_nhanvienhocvu;
grant select on qltt_admin.hoa_don to role_nhanvienhocvu;


-- Cấp quyền cho vai trò Kế toán (role_ketoan)
grant select,insert,update on qltt_admin.phieu_thanh_toan to role_ketoan;
commit;