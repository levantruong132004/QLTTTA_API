using Oracle.ManagedDataAccess.Client;

var connectionString = "User Id=QLTT_ADMIN;Password=123456;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=100.118.120.99)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=orclpdb)))";

var sql = @"
CREATE OR REPLACE PROCEDURE sp_gui_yeu_cau_dang_ky_khoa (
   p_course_code IN VARCHAR2,
   p_ket_qua      OUT NVARCHAR2
) AS
   v_user_id     NUMBER;
   v_hoc_vien_id NUMBER;
   v_khoa_id     NUMBER;
   v_lop_id      NUMBER;
BEGIN
   IF p_course_code IS NULL THEN
      p_ket_qua := N'Mã khóa học không hợp lệ.';
      RETURN;
   END IF;

   BEGIN
      SELECT id_nguoi_dung INTO v_user_id
        FROM qltt_admin.tai_khoan
       WHERE UPPER(ten_dang_nhap) = UPPER(USER);
   EXCEPTION
      WHEN NO_DATA_FOUND THEN
         p_ket_qua := N'Không tìm thấy tài khoản đăng nhập.';
         RETURN;
   END;

   BEGIN
      SELECT id_hoc_vien INTO v_hoc_vien_id
        FROM qltt_admin.hoc_vien
       WHERE id_hoc_vien = v_user_id;
   EXCEPTION
      WHEN NO_DATA_FOUND THEN
         p_ket_qua := N'Tài khoản hiện tại không phải là học viên.';
         RETURN;
   END;

   BEGIN
      SELECT id_khoa_hoc INTO v_khoa_id
        FROM qltt_admin.khoa_hoc
       WHERE ma_khoa_hoc = UPPER(p_course_code);
   EXCEPTION
      WHEN NO_DATA_FOUND THEN
         p_ket_qua := N'Không tìm thấy khóa học với mã: ' || p_course_code;
         RETURN;
   END;

   BEGIN
      SELECT id_lop_hoc INTO v_lop_id
        FROM qltt_admin.lop_hoc
       WHERE id_khoa_hoc = v_khoa_id
         AND trang_thai = N'Đang tuyển sinh'
         FETCH FIRST 1 ROWS ONLY;
   EXCEPTION
      WHEN NO_DATA_FOUND THEN
         v_lop_id := NULL;
   END;

   DECLARE
      v_count_existing NUMBER;
   BEGIN
      SELECT COUNT(*) INTO v_count_existing
        FROM qltt_admin.don_dang_ky ddk
       WHERE ddk.id_hoc_vien = v_hoc_vien_id
         AND EXISTS (
            SELECT 1 FROM qltt_admin.lop_hoc lh
            WHERE lh.id_lop_hoc = ddk.id_lop_hoc
              AND lh.id_khoa_hoc = v_khoa_id
         )
         AND ddk.trang_thai IN (N'Chờ duyệt', N'Đã duyệt');
      
      IF v_count_existing > 0 THEN
         p_ket_qua := N'Bạn đã có đơn đăng ký khóa học này rồi.';
         RETURN;
      END IF;
   END;

   INSERT INTO qltt_admin.don_dang_ky (
      ma_dang_ky, ngay_dang_ky, trang_thai, id_hoc_vien, id_lop_hoc
   )
   VALUES (
      'DGK_' || TO_CHAR(SYSDATE, 'YYYYMMDDHH24MISS') || '_' || TRUNC(DBMS_RANDOM.VALUE(1000, 9999)),
      SYSDATE, N'Chờ duyệt', v_hoc_vien_id, v_lop_id
   );

   COMMIT;

   IF v_lop_id IS NULL THEN
      p_ket_qua := N'Đã gửi yêu cầu đăng ký khóa học thành công. Nhân viên sẽ gán lớp cho bạn sau khi duyệt.';
   ELSE
      p_ket_qua := N'Đã gửi yêu cầu đăng ký lớp học thành công. Vui lòng chờ duyệt.';
   END IF;

EXCEPTION
   WHEN OTHERS THEN
      ROLLBACK;
      p_ket_qua := N'Lỗi: ' || SQLERRM;
END;
";

try
{
    using var conn = new OracleConnection(connectionString);
    await conn.OpenAsync();
    
    using var cmd = new OracleCommand(sql, conn);
    await cmd.ExecuteNonQueryAsync();
    
    Console.WriteLine("✓ Procedure updated successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Error: {ex.Message}");
}
