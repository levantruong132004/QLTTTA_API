CREATE OR REPLACE PROCEDURE sp_gui_yeu_cau_dang_ky_khoa (
   p_course_code IN VARCHAR2,
   p_ket_qua      OUT NVARCHAR2
) AS
   v_user_id     NUMBER;
   v_hoc_vien_id NUMBER;
   v_khoa_id     NUMBER;
   v_lop_id      NUMBER;
BEGIN
   -- Kiểm tra đầu vào
   IF p_course_code IS NULL THEN
      p_ket_qua := N'Mã khóa học không hợp lệ.';
      RETURN;
   END IF;

   -- Lấy ID người dùng hiện tại (đang đăng nhập)
   SELECT id_nguoi_dung
     INTO v_user_id
     FROM qltt_admin.tai_khoan
    WHERE UPPER(ten_dang_nhap) = UPPER(USER);

   -- Kiểm tra người này có phải học viên hợp lệ không
   SELECT id_hoc_vien
     INTO v_hoc_vien_id
     FROM qltt_admin.hoc_vien
    WHERE id_hoc_vien = v_user_id;

   -- Lấy ID khóa học tương ứng với mã nhập vào
   SELECT id_khoa_hoc
     INTO v_khoa_id
     FROM qltt_admin.khoa_hoc
    WHERE ma_khoa_hoc = UPPER(p_course_code);

   -- Tìm lớp học thuộc khóa này đang tuyển sinh
   SELECT id_lop_hoc
     INTO v_lop_id
     FROM qltt_admin.lop_hoc
    WHERE id_khoa_hoc = v_khoa_id
      AND trang_thai = N'Đang tuyển sinh'
      FETCH FIRST 1 ROWS ONLY;

   -- Tạo đơn đăng ký mới
   INSERT INTO qltt_admin.don_dang_ky (
      ma_dang_ky,
      ngay_dang_ky,
      trang_thai,
      id_hoc_vien,
      id_lop_hoc
   )
   VALUES (
      'DGK_' || TO_CHAR(SYSDATE, 'YYYYMMDDHH24MISS') || '_' || TRUNC(DBMS_RANDOM.VALUE(1000, 9999)),
      SYSDATE,
      N'Chờ duyệt',
      v_hoc_vien_id,
      v_lop_id
   );

   p_ket_qua := N'Đã gửi yêu cầu đăng ký lớp học thành công. Vui lòng chờ duyệt.';
EXCEPTION
   WHEN NO_DATA_FOUND THEN
      p_ket_qua := N'Không tìm thấy học viên, khóa học hoặc lớp học phù hợp.';
   WHEN OTHERS THEN
      p_ket_qua := N'Lỗi: ' || SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE sp_duyet_don_dang_ky (
    p_ma_dang_ky IN VARCHAR2,      -- Mã đơn đăng ký cần duyệt
    p_hanh_dong  IN VARCHAR2,      -- 'DUYET' hoặc 'TUCHOI'
    p_ket_qua    OUT NVARCHAR2     -- Kết quả trả về
) AS
    v_id_don         NUMBER;
    v_id_nhan_vien   NUMBER;
    v_count_nv       NUMBER;
    v_trang_thai_moi NVARCHAR2(30);
BEGIN
    -- Kiểm tra đầu vào
    IF p_ma_dang_ky IS NULL OR p_hanh_dong IS NULL THEN
        p_ket_qua := N'Mã đơn đăng ký hoặc hành động không hợp lệ.';
        RETURN;
    END IF;

    -- Lấy ID người dùng hiện tại (đang đăng nhập)
    SELECT id_nguoi_dung
      INTO v_id_nhan_vien
      FROM qltt_admin.tai_khoan
     WHERE UPPER(ten_dang_nhap) = UPPER(USER);

    -- Kiểm tra người này có phải nhân viên học vụ không
    SELECT COUNT(*)
      INTO v_count_nv
      FROM qltt_admin.nhan_vien_hoc_vu
     WHERE id_nhan_vien = v_id_nhan_vien;

    IF v_count_nv = 0 THEN
        p_ket_qua := N'Tài khoản hiện tại không phải nhân viên học vụ.';
        RETURN;
    END IF;

    -- Kiểm tra đơn đăng ký có tồn tại không
    SELECT id_dang_ky
      INTO v_id_don
      FROM qltt_admin.don_dang_ky
     WHERE ma_dang_ky = p_ma_dang_ky;

    -- Xác định trạng thái mới
    IF UPPER(p_hanh_dong) = 'DUYET' THEN
        v_trang_thai_moi := N'Đã duyệt';
    ELSIF UPPER(p_hanh_dong) = 'TUCHOI' THEN
        v_trang_thai_moi := N'Đã từ chối';
    ELSE
        p_ket_qua := N'Hành động không hợp lệ (chỉ chấp nhận DUYET hoặc TUCHOI).';
        RETURN;
    END IF;

    -- Cập nhật đơn đăng ký
    UPDATE qltt_admin.don_dang_ky
       SET trang_thai        = v_trang_thai_moi,
           ngay_duyet        = SYSDATE,
           id_nhan_vien_duyet = v_id_nhan_vien
     WHERE id_dang_ky = v_id_don;

    COMMIT;

    IF v_trang_thai_moi = N'Đã duyệt' THEN
        p_ket_qua := N'Đơn đăng ký đã được phê duyệt thành công.';
    ELSE
        p_ket_qua := N'Đơn đăng ký đã bị từ chối.';
    END IF;

EXCEPTION
    WHEN NO_DATA_FOUND THEN
        p_ket_qua := N'Không tìm thấy đơn đăng ký hoặc tài khoản hợp lệ.';
    WHEN OTHERS THEN
        p_ket_qua := N'Lỗi: ' || SQLERRM;
END;
/
