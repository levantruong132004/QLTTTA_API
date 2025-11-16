-- ==============================
-- Script: create_open_class_for_course.sql
-- Purpose: Tạo nhanh một lớp "Đang tuyển sinh" cho một khóa học có sẵn (theo MA_KHOA_HOC)
-- Sử dụng trong Oracle SQL Developer:
--   1) Đặt biến mã khóa cần tạo lớp: DEFINE COURSE_CODE = 'GT_CB';
--   2) Chạy toàn bộ script (F5). Script sẽ:
--      - Tìm ID_KHOA_HOC theo MA_KHOA_HOC.
--      - Tạo một lớp mới với trạng thái 'Đang tuyển sinh'.
--      - In ra ID_LOP_HOC vừa tạo.
-- Ghi chú:
--   - Script giả định các trigger/sequence đã cấu hình để tự sinh ID_LOP_HOC nếu cần.
--   - Nếu bảng yêu cầu ID_GIANG_VIEN NOT NULL, bạn hãy sửa phần lấy v_teacher_id bên dưới.
-- ==============================

SET SERVEROUTPUT ON
-- NOTE: Do NOT hardcode COURSE_CODE here.
-- How to use this script:
--   Option A) Define before run in the same session:
--       DEFINE COURSE_CODE = 'GT_CB'
--       @create_open_class_for_course.sql
--   Option B) Use a tiny wrapper file that sets COURSE_CODE then calls this script
--       (e.g. create_open_class_TOEIC650.sql)

DECLARE
  v_course_id    NUMBER;
  v_teacher_id   NUMBER; -- có thể để NULL nếu cột cho phép
  v_new_class_id NUMBER;
  v_code         VARCHAR2(64);
  v_name         NVARCHAR2(128);
BEGIN
  -- 1) Tìm ID_KHOA_HOC theo MA_KHOA_HOC
  SELECT ID_KHOA_HOC INTO v_course_id
  FROM QLTT_ADMIN.KHOA_HOC
  WHERE MA_KHOA_HOC = UPPER('&COURSE_CODE');

  -- 2) Chọn đại một giảng viên nếu cần (tùy schema). Có thể để NULL nếu cột cho phép.
  BEGIN
    SELECT ID_GIANG_VIEN INTO v_teacher_id
    FROM QLTT_ADMIN.GIANG_VIEN
    WHERE ROWNUM = 1;
  EXCEPTION WHEN NO_DATA_FOUND THEN
    v_teacher_id := NULL; -- nếu không có giảng viên, để null
  END;

  -- 3) Sinh mã/ tên lớp
  v_code := 'OPEN_' || '&COURSE_CODE' || '_' || TO_CHAR(SYSDATE, 'MMDDHH24MI');
  v_name := N'Lớp mở ' || '&COURSE_CODE' || ' ' || TO_CHAR(SYSDATE, 'DD/MM');

  -- 4) Tạo lớp ở trạng thái Đang tuyển sinh (ngày bắt đầu sau 7 ngày, kết thúc sau 3 tháng)
  INSERT INTO QLTT_ADMIN.LOP_HOC (
    MA_LOP_HOC, TEN_LOP_HOC, NGAY_BAT_DAU, NGAY_KET_THUC,
    SI_SO_TOI_DA, ID_KHOA_HOC, ID_GIANG_VIEN, TRANG_THAI
  ) VALUES (
    v_code, v_name, SYSDATE + 7, ADD_MONTHS(SYSDATE, 3),
    50, v_course_id, v_teacher_id, N'Đang tuyển sinh'
  )
  RETURNING ID_LOP_HOC INTO v_new_class_id;

  DBMS_OUTPUT.PUT_LINE('Đã tạo lớp mở: ID_LOP_HOC = ' || v_new_class_id || ', MA_LOP_HOC = ' || v_code);
  COMMIT;
EXCEPTION
  WHEN NO_DATA_FOUND THEN
    DBMS_OUTPUT.PUT_LINE('Không tìm thấy MA_KHOA_HOC = ' || '&COURSE_CODE');
  WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('Lỗi: ' || SQLERRM);
    ROLLBACK;
END;
/ 

-- (Tùy chọn) Tạo lịch học mẫu nếu bảng LICH_HOC cho phép để hiển thị scheduleText (có thể bỏ qua nếu không cần)
-- INSERT INTO QLTT_ADMIN.LICH_HOC (ID_LOP_HOC, THU_TRONG_TUAN, GIO_BAT_DAU, GIO_KET_THUC)
-- VALUES (v_new_class_id, 2, TO_DATE('18:00','HH24:MI'), TO_DATE('20:00','HH24:MI'));
-- COMMIT;
