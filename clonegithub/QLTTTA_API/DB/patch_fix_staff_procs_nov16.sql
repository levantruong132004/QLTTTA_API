-- Patch: Fix staff registration procedures (Nhan vien hoc vu & Ke toan)
-- - Normalize username (TRIM + UPPER) and use quoted identifiers consistently
-- - Ensure CREATE SESSION + role grants and unlock account
-- - Optional input p_ma_nhan_vien to populate MA_NHAN_VIEN
-- Run as owner schema (QLTT_ADMIN)

   SET DEFINE OFF;

create or replace procedure sp_dang_ky_nhan_vien_hoc_vu (
   p_ten_dang_nhap in varchar2,
   p_mat_khau      in varchar2,
   p_email         in varchar2,
   p_ho_ten        in nvarchar2,
   p_gioi_tinh     in nvarchar2,
   p_sdt           in varchar2,
   p_ma_nhan_vien  in varchar2 default null,
   p_ket_qua       out nvarchar2
) as
   v_user_id       number;
   v_role_id       number;
   v_sql           varchar2(2000);
   v_mat_khau_hash varchar2(200);
   v_uname         varchar2(128);
begin
    -- Normalize and fix username case/whitespace
   v_uname := upper(trim(p_ten_dang_nhap));

    -- Resolve role id
   select id_vai_tro
     into v_role_id
     from vai_tro
    where ten_vai_tro = 'NhanVienHocVu';

    -- Hash password (same logic as student proc)
   v_mat_khau_hash := simple_hash(p_mat_khau || 'SALT2025');

    -- Insert account row
   insert into tai_khoan (
      ten_dang_nhap,
      mat_khau,
      email,
      id_vai_tro
   ) values ( v_uname,
              v_mat_khau_hash,
              p_email,
              v_role_id ) returning id_nguoi_dung into v_user_id;

    -- Insert staff profile
   insert into nhan_vien_hoc_vu (
      id_nhan_vien,
      ho_ten,
      gioi_tinh,
      so_dien_thoai,
      ma_nhan_vien
   ) values ( v_user_id,
              p_ho_ten,
              p_gioi_tinh,
              p_sdt,
              p_ma_nhan_vien );

    -- Create physical Oracle user (quoted identifier)
   v_sql := 'CREATE USER "'
            || v_uname
            || '" IDENTIFIED BY "'
            || p_mat_khau
            || '" PROFILE TTTA_USER_PROFILE';
   execute immediate v_sql;

    -- Ensure session/grants
   execute immediate 'ALTER USER "'
                     || v_uname
                     || '" ACCOUNT UNLOCK';
   execute immediate 'GRANT CREATE SESSION TO "'
                     || v_uname
                     || '"';
   execute immediate 'GRANT role_nhanvienhocvu TO "'
                     || v_uname
                     || '"';
   commit;
   p_ket_qua := N'Thêm nhân viên học vụ thành công!';
exception
   when dup_val_on_index then
      rollback;
      p_ket_qua := N'Lỗi: Tên đăng nhập hoặc email đã tồn tại.';
   when others then
      rollback;
      p_ket_qua := N'Lỗi hệ thống: ' || sqlerrm;
end;
/

create or replace procedure sp_dang_ky_ke_toan (
   p_ten_dang_nhap in varchar2,
   p_mat_khau      in varchar2,
   p_email         in varchar2,
   p_ho_ten        in nvarchar2,
   p_gioi_tinh     in nvarchar2,
   p_sdt           in varchar2,
   p_ma_nhan_vien  in varchar2 default null,
   p_ket_qua       out nvarchar2
) as
   v_user_id       number;
   v_role_id       number;
   v_sql           varchar2(2000);
   v_mat_khau_hash varchar2(200);
   v_uname         varchar2(128);
begin
    -- Normalize and fix username
   v_uname := upper(trim(p_ten_dang_nhap));

    -- Resolve role id
   select id_vai_tro
     into v_role_id
     from vai_tro
    where ten_vai_tro = 'KeToan';

    -- Hash password
   v_mat_khau_hash := simple_hash(p_mat_khau || 'SALT2025');

    -- Insert account row
   insert into tai_khoan (
      ten_dang_nhap,
      mat_khau,
      email,
      id_vai_tro
   ) values ( v_uname,
              v_mat_khau_hash,
              p_email,
              v_role_id ) returning id_nguoi_dung into v_user_id;

    -- Insert accountant profile
   insert into ke_toan (
      id_ke_toan,
      ho_ten,
      gioi_tinh,
      so_dien_thoai,
      ma_nhan_vien
   ) values ( v_user_id,
              p_ho_ten,
              p_gioi_tinh,
              p_sdt,
              p_ma_nhan_vien );

    -- Create physical Oracle user (quoted)
   v_sql := 'CREATE USER "'
            || v_uname
            || '" IDENTIFIED BY "'
            || p_mat_khau
            || '" PROFILE TTTA_USER_PROFILE';
   execute immediate v_sql;

    -- Ensure session/grants
   execute immediate 'ALTER USER "'
                     || v_uname
                     || '" ACCOUNT UNLOCK';
   execute immediate 'GRANT CREATE SESSION TO "'
                     || v_uname
                     || '"';
   execute immediate 'GRANT role_ketoan TO "'
                     || v_uname
                     || '"';
   commit;
   p_ket_qua := N'Thêm kế toán thành công!';
exception
   when dup_val_on_index then
      rollback;
      p_ket_qua := N'Lỗi: Tên đăng nhập hoặc email đã tồn tại.';
   when others then
      rollback;
      p_ket_qua := N'Lỗi hệ thống: ' || sqlerrm;
end;
/

-- End of patch