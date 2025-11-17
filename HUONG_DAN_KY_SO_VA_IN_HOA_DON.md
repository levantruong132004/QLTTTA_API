# 📝 HƯỚNG DẪN KÝ SỐ VÀ IN HÓA ĐƠN

## 🎯 TỔNG QUAN

Sau khi sửa, hệ thống có **1 nút duy nhất** để ký số và in hóa đơn với giao diện đẹp:
- ✅ **Nút "Ký số & In"** - Tự động ký số (nếu chưa ký) và tạo PDF với giao diện đẹp

## 📋 QUY TRÌNH SỬ DỤNG

### Bước 1: Tạo hóa đơn
1. Đăng nhập với tài khoản **Kế toán** (ketoan01 / 123456)
2. Vào trang **Trang chủ Kế toán**
3. Chọn khóa học và lớp học
4. Tìm đơn đăng ký **Đã duyệt**
5. Nhấn nút **"Tạo hóa đơn"**
6. Nhập thông tin:
   - Ngày đến hạn
   - Số tiền
7. Nhấn **"Xuất hóa đơn"**

### Bước 2: Ký số và In hóa đơn (MỚI)
1. Quay lại trang **Trang chủ Kế toán**
2. Tìm đơn vừa tạo hóa đơn
3. Nhấn nút **"Ký số & In"** (màu xanh)
4. Popup hiện ra:
   - Hiển thị tên học viên
   - Hiển thị mã hóa đơn
5. Chọn file **Private Key** (.pem)
6. Nhấn **"Ký số và In"**

### Kết quả:
✅ Hóa đơn được ký số
✅ PDF với giao diện đẹp (màu nâu #5c3a1e) được tạo
✅ PDF được gửi qua email cho học viên
✅ Thông báo thành công hiển thị

## 🎨 GIAO DIỆN PDF MỚI

### Đặc điểm:
- 🎨 **Màu chủ đạo:** Nâu (#5c3a1e) - chuyên nghiệp
- 📋 **Header:** Logo + tên trung tâm
- 📊 **Nội dung:**
  - Thông tin hóa đơn (mã, ngày, hạn)
  - Thông tin học viên (tên, khóa, lớp)
  - **Thông tin thanh toán** (ngân hàng, số TK, QR code)
- 🔐 **Chữ ký số:** Nhúng trực tiếp vào PDF
- 📱 **Footer:** Website, email, hotline

### So sánh với PDF cũ:

| Tính năng | PDF Cũ | PDF Mới |
|-----------|--------|---------|
| Màu sắc | Xanh | Nâu |
| Thông tin thanh toán | ❌ | ✅ |
| QR Code | ❌ | ✅ |
| Chữ ký số | Chỉ metadata | Toàn bộ PDF |
| Giao diện | Đơn giản | Chuyên nghiệp |

## 🔍 KIỂM TRA

### 1. Kiểm tra PDF đã ký số
- Mở email học viên nhận được
- Tải file PDF đính kèm
- Kiểm tra:
  - ✅ Màu nâu (#5c3a1e)
  - ✅ Có thông tin thanh toán
  - ✅ Có vị trí QR code
  - ✅ Footer đầy đủ

### 2. Xác thực chữ ký số
- Vào trang **Xác thực hóa đơn** (nếu có)
- Upload file PDF
- Kết quả phải là: **"✅ CHỮ KÝ VÀ PDF HOÀN TOÀN HỢP LỆ"**

## ⚠️ LƯU Ý

### Nếu hóa đơn đã ký trước đó:
- Nhấn nút **"Ký số & In"** vẫn hoạt động
- Hệ thống sẽ:
  - ✅ Phát hiện đã ký
  - ✅ Tạo lại PDF với giao diện đẹp
  - ✅ Gửi email cho học viên
  - ✅ Thông báo: "Hóa đơn đã được ký số trước đó"

### Nếu chưa ký:
- Hệ thống sẽ:
  - ✅ Ký số với private key
  - ✅ Tạo PDF với giao diện đẹp
  - ✅ Gửi email cho học viên
  - ✅ Thông báo: "Đã ký số hóa đơn thành công!"

## 🚀 TRIỂN KHAI

### 1. Build lại project
```bash
# API
cd QLTTTA_API
dotnet build

# WEB
cd QLTTTA_WEB
dotnet build
```

### 2. Restart cả 2 project
```bash
# Terminal 1 - API
cd QLTTTA_API
dotnet run

# Terminal 2 - WEB
cd QLTTTA_WEB
dotnet run
```

### 3. Test
1. Đăng nhập kế toán
2. Tạo hóa đơn
3. Nhấn "Ký số & In"
4. Upload private key
5. Kiểm tra email

## 📧 EMAIL MẪU

Học viên sẽ nhận được email với:
- **Subject:** Hóa đơn đã ký số - [Tên khóa học]
- **Body:** Thông tin hóa đơn
- **Attachment:** HoaDon_[MaHoaDon]_[NgayGio]_Signed.pdf

## ✅ CHECKLIST

- [ ] Build thành công
- [ ] Restart API và WEB
- [ ] Đăng nhập kế toán
- [ ] Tạo hóa đơn mới
- [ ] Nhấn "Ký số & In"
- [ ] Upload private key
- [ ] Kiểm tra thông báo thành công
- [ ] Kiểm tra email học viên
- [ ] Mở PDF và xem giao diện mới
- [ ] Xác thực chữ ký số

## 🎉 KẾT QUẢ

Sau khi hoàn tất, bạn sẽ có:
1. ✅ Hệ thống ký số hoàn chỉnh
2. ✅ PDF với giao diện chuyên nghiệp
3. ✅ Chữ ký số an toàn (ký toàn bộ PDF)
4. ✅ Giao diện thống nhất (email = xem hóa đơn)
5. ✅ Quy trình đơn giản (1 nút duy nhất)

---

**Lưu ý:** Nếu gặp lỗi, kiểm tra:
- Private key file đúng định dạng (.pem)
- SMTP settings trong appsettings.json
- QuestPDF license (Community)
- Log trong console
