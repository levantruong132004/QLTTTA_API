## Hướng dẫn thiết lập CSDL (Oracle) và bản vá đăng ký khóa học

Tài liệu này tổng hợp 2 nội dung:
- Quickstart khởi tạo database từ thư mục `DB/`
- Bản vá stored procedure cho phép học viên gửi đơn đăng ký khóa ngay cả khi chưa có lớp mở tuyển sinh

---

## 1) Quickstart khởi tạo database

Yêu cầu tối thiểu:
- Oracle Database 19c/21c (PDB)
- Tài khoản quản trị schema: `QLTT_ADMIN` (hoặc tương đương)

Thứ tự khuyến nghị khi triển khai (tất cả file nằm trong thư mục `DB/`):
1. `database.sql` — Tạo schema nền tảng, bảng chính
2. `digital_signature_schema.sql` — Bảng/khoá phục vụ chữ ký số hóa đơn
3. `ttta_center_table.sql` — Bảng dữ liệu trung tâm (tham chiếu/điều khiển)
4. `role.sql` — Tạo roles/quyền cơ bản
5. `store_proceduere.sql` — Toàn bộ stored procedures chính
6. `deploy_permissions_views_procs.sql` — Cấp quyền và publish view/procedure
7. (Tùy chọn) `setup_ketoan_minhtien.sql` — Thiết lập phần kế toán
8. (Tùy chọn) `add_qlttta_admin_to_tai_khoan.sql` — Thêm user quản trị mẫu
9. (Tùy chọn) `sample_data.sql` (ở thư mục gốc repo) — Dữ liệu mẫu để thử nghiệm

Các script hỗ trợ khi cần:
- `debug_digital_signature.sql` — Kiểm tra/tinh chỉnh chữ ký số
- `delete_all_invoices.sql` — Xóa dữ liệu hóa đơn khi cần reset môi trường

Chạy script bằng một trong các cách sau:

### Cách A: SQL Developer (khuyến nghị)
1) Kết nối với user schema (ví dụ `QLTT_ADMIN`)
2) Open từng file theo thứ tự trên và bấm Run Script (F5)

### Cách B: SQL*Plus / SQLcl
```powershell
sqlplus QLTT_ADMIN/<password>@//<host>:<port>/<service>
@database.sql
@digital_signature_schema.sql
@ttta_center_table.sql
@role.sql
@store_proceduere.sql
@deploy_permissions_views_procs.sql
-- tùy chọn
-- @setup_ketoan_minhtien.sql
-- @add_qlttta_admin_to_tai_khoan.sql
-- @..\sample_data.sql
EXIT;
```

---

## 2) Bản vá: Cho phép đăng ký khóa học khi chưa có lớp tuyển sinh

### Vấn đề
Mobile app không thể đăng ký khóa học vì stored procedure yêu cầu phải có lớp học ở trạng thái "Đang tuyển sinh".

### Giải pháp
Sửa stored procedure `SP_GUI_YEU_CAU_DANG_KY_KHOA` để:
- Cho phép đăng ký khóa học ngay cả khi chưa có lớp (đơn có thể lưu `ID_LOP_HOC = NULL`)
- Nhân viên sẽ gán lớp khi duyệt đơn
- Thêm validation tránh đăng ký trùng

### Cách áp dụng bản vá

1) Mở và chạy file `fix_register_course_proc.sql` trong thư mục `DB/` bằng một trong các công cụ sau:

• SQL Developer
  - Kết nối user `QLTT_ADMIN`
  - Mở file và Run Script (F5)

• SQL*Plus / SQLcl
```powershell
sqlplus QLTT_ADMIN/<password>@//<host>:<port>/<service>
@fix_register_course_proc.sql
EXIT;
```

• Dán trực tiếp nội dung script vào bất kỳ SQL worksheet nào (SQL Developer, DBeaver, ...)

Kết quả mong đợi: `Procedure created.`

### Sau khi cập nhật
- Khởi động lại ứng dụng (hoặc hot reload) mobile/web
- Thử tạo đơn đăng ký khóa: vẫn thành công nếu chưa có lớp mở

### Thay đổi chính (minh họa)

Trước:
```sql
-- Bắt buộc phải có lớp đang tuyển sinh
SELECT id_lop_hoc INTO v_lop_id
FROM qltt_admin.lop_hoc
WHERE id_khoa_hoc = v_khoa_id
  AND trang_thai = N'Đang tuyển sinh'
  FETCH FIRST 1 ROWS ONLY;
-- Nếu không tìm thấy → EXCEPTION → Thất bại
```

Sau:
```sql
-- Tìm lớp đang tuyển sinh (nếu có)
BEGIN
  SELECT id_lop_hoc INTO v_lop_id
  FROM qltt_admin.lop_hoc
  WHERE id_khoa_hoc = v_khoa_id
    AND trang_thai = N'Đang tuyển sinh'
    FETCH FIRST 1 ROWS ONLY;
EXCEPTION
  WHEN NO_DATA_FOUND THEN
    v_lop_id := NULL;  -- OK, nhân viên sẽ gán sau
END;

-- Tạo đơn với id_lop_hoc = NULL nếu chưa có lớp
INSERT INTO qltt_admin.don_dang_ky (...)
VALUES (..., v_lop_id);  -- Có thể NULL
```

### Validation đi kèm
- ✅ Kiểm tra tài khoản tồn tại
- ✅ Kiểm tra là học viên hợp lệ
- ✅ Kiểm tra khóa học tồn tại
- ✅ Kiểm tra trùng đăng ký (không cho đăng ký 2 lần cùng 1 khóa)
- ✅ Trả thông báo chi tiết theo từng lỗi

### Test nhanh
- Trường hợp có lớp mở tuyển sinh: đơn có `ID_LOP_HOC`
- Chưa có lớp: đơn tạo thành công, `ID_LOP_HOC = NULL`
- Đăng ký trùng: báo lỗi đã tồn tại đơn
- Tài khoản không phải học viên: báo lỗi quyền

### Rollback (nếu cần)
Nếu muốn quay về version thủ tục cũ, chạy lại file `store_proceduere.sql` gốc theo thứ tự triển khai.
