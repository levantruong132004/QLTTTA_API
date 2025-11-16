# 🎓 Hệ thống Quản lý Trung tâm Tiếng Anh LDA

## 📋 Tổng quan

Hệ thống quản lý toàn diện cho trung tâm tiếng Anh LDA với đầy đủ các chức năng:

- Quản lý học viên
- Quản lý khóa học
- Quản lý lớp học
- Quản lý giáo viên
- Quản lý đăng ký học
- Quản lý thanh toán
- Báo cáo và thống kê

## 🚀 Cách khởi chạy

### Phương pháp 1: Sử dụng Script tự động

1. Chạy file `start-lda-system.bat`
2. Hệ thống sẽ tự động khởi động API và Web
3. Trình duyệt sẽ mở trang đăng nhập

### Phương pháp 2: Chạy thủ công

1. **Khởi động API:**

   ```bash
   cd "d:\Doan_KLTN\QLTTTA_API\QLTTTA_API"
   dotnet run
   ```

2. **Khởi động Web (terminal mới):**

   ```bash
   cd "d:\Doan_KLTN\QLTTTA_API\QLTTTA_WEB"
   dotnet run
   ```

3. **Truy cập:**
   - Web: http://localhost:5165
   - API: http://localhost:5069

## 🔑 Tài khoản mặc định

| Username | Password | Vai trò          |
| -------- | -------- | ---------------- |
| admin    | 123456   | Quản trị viên    |
| hocvu01  | 123456   | Nhân viên học vụ |
| ketoan01 | 123456   | Kế toán          |

## 🗄️ Cài đặt Database

1. **Kết nối Oracle Database** với thông tin trong `appsettings.json`
2. **Chạy script tạo database:**
   ```sql
   -- Chạy file sample_data.sql để tạo bảng và dữ liệu mẫu
   ```

## 📱 Các chức năng chính

### 🧑‍🎓 Quản lý Học viên

- ✅ Xem danh sách học viên (có phân trang)
- ✅ Tìm kiếm học viên
- ✅ Thêm học viên mới
- ✅ Chỉnh sửa thông tin học viên
- ✅ Xóa học viên (có kiểm tra ràng buộc)
- ✅ Xem chi tiết học viên

### 📚 Quản lý Khóa học

- 🔄 Đang phát triển...

### 🏫 Quản lý Lớp học

- 🔄 Đang phát triển...

### 👨‍🏫 Quản lý Giáo viên

- 🔄 Đang phát triển...

### 📝 Quản lý Đăng ký

- 🔄 Đang phát triển...

### 💰 Quản lý Thanh toán

- 🔄 Đang phát triển...

## 🎨 Đặc điểm Giao diện

- **Thiết kế hiện đại:** Gradient background, rounded corners
- **Responsive:** Tương thích mobile, tablet, desktop
- **Thân thiện:** Icons rõ ràng, màu sắc hài hòa
- **Hiệu ứng:** Hover effects, smooth transitions
- **Validation:** Form validation với thông báo lỗi chi tiết

## 🔧 Cấu trúc Technical

### Backend (API)

- **Framework:** ASP.NET Core 8.0
- **Database:** Oracle Database
- **Architecture:** Layered (Controller → Service → Repository)
- **Authentication:** Session-based
- **API Style:** RESTful

### Frontend (Web)

- **Framework:** ASP.NET Core MVC
- **UI Framework:** Bootstrap 5
- **Icons:** Font Awesome 6
- **CSS:** Custom CSS với Flexbox/Grid
- **JavaScript:** Vanilla JS + Bootstrap JS

## 📁 Cấu trúc Project

```
QLTTTA_API/
├── QLTTTA_API/          # Backend API
│   ├── Controllers/     # API Controllers
│   ├── Services/        # Business Logic
│   ├── Models/          # Data Models & DTOs
│   └── Program.cs       # API Configuration
├── QLTTTA_WEB/          # Frontend Web
│   ├── Controllers/     # MVC Controllers
│   ├── Views/           # Razor Views
│   ├── Models/          # ViewModels
│   └── wwwroot/         # Static Files
└── sample_data.sql      # Database Script
```

## 🐛 Troubleshooting

### Lỗi thường gặp:

1. **API không kết nối được Database:**

   - Kiểm tra connection string trong `appsettings.json`
   - Đảm bảo Oracle Database đang chạy
   - Kiểm tra user/password và service name

2. **Web không gọi được API:**

   - Kiểm tra API đang chạy trên port 5069
   - Kiểm tra CORS settings
   - Kiểm tra firewall/antivirus

3. **Lỗi build:**

   - Chạy `dotnet clean` rồi `dotnet build`
   - Kiểm tra .NET 8.0 SDK đã cài đặt

4. **Lỗi login:**
   - Đảm bảo đã chạy script tạo dữ liệu mẫu
   - Kiểm tra bảng ACCOUNTS có dữ liệu

## 🚧 Roadmap

- [ ] Hoàn thiện tất cả modules quản lý
- [ ] Thêm Dashboard với charts/statistics
- [ ] Export/Import Excel
- [ ] Email notifications
- [ ] Mobile app
- [ ] Multi-language support

## 👥 Liên hệ

- **Developer:** [Tên của bạn]
- **Email:** [Email của bạn]
- **GitHub:** [GitHub repository]
