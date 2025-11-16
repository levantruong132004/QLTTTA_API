# 🎓 Hệ thống Quản lý Trung tâm Tiếng Anh LDA

> LDA (Language Development Academy) là bộ giải pháp quản trị trung tâm tiếng Anh gồm API, website quản trị và ứng dụng di động. Hệ thống được xây dựng nội bộ nhưng đang mở rộng thêm tính năng tự phục vụ cho học viên.

## 🧭 Thành phần chính

| Module | Công nghệ | Chức năng chính |
| --- | --- | --- |
| `QLTTTA_API` | ASP.NET Core 8 · Oracle | REST API, xác thực session, xử lý đăng ký & hóa đơn, chữ ký số. |
| `QLTTTA_WEB` | ASP.NET Core MVC · Bootstrap 5 | Website quản trị chủ đề retro (dashboard, học viên, hóa đơn, chính sách bảo mật). |
| `qlttta_app_mobile` | Flutter 3 | Ứng dụng mobile đa vai trò: dashboard, đăng ký, QR đăng nhập web, Hồ sơ của tôi. |

## 🚀 Khởi chạy nhanh

### 1. Script tự động
```powershell
./start-lda-system.bat
```
Script mở hai tiến trình `dotnet run` (API + Web) và tự kích hoạt trình duyệt đăng nhập.

### 2. Chạy thủ công
```powershell
# Terminal 1 - API
cd QLTTTA_API/QLTTTA_API
dotnet run

# Terminal 2 - Web
cd QLTTTA_API/QLTTTA_WEB
dotnet run
```
- API: `http://localhost:5069`
- Web: `http://localhost:5165`
- Mobile: truyền `--dart-define API_BASE_URL=<url>` cho `flutter run` (mặc định `http://10.0.2.2:7158/api` trên emulator).

## 🔑 Tài khoản mẫu

| Username | Password | Vai trò |
| --- | --- | --- |
| `admin` | `123456` | Quản trị viên |
| `hocvu01` | `123456` | Nhân viên học vụ |
| `ketoan01` | `123456` | Kế toán |

> Sau khi đăng nhập, API trả về `X-Session-Id`. Mobile app lưu giá trị này trong `SharedPreferences` và gắn vào mọi request.

## 🗄️ Thiết lập cơ sở dữ liệu

1. Khởi động Oracle Database (giống môi trường triển khai thực tế).
2. Cập nhật `appsettings.Development.json` (API & Web) với connection string phù hợp.
3. Chạy `DB/database.sql` để dựng schema, sau đó áp dụng các script vá (`deploy_permissions_views_procs.sql`, `run_fix.ps1`, ...).
4. Nạp dữ liệu mẫu bằng `sample_data.sql` nhằm có sẵn tài khoản, khóa học, hóa đơn demo.

## ✨ Chức năng đã hoàn thiện

- Dashboard đa vai trò với số liệu khóa học, lớp, đăng ký, hóa đơn.
- Quản lý học viên: tìm kiếm, phân trang, xem hồ sơ chi tiết đồng bộ API/Web/Mobile.
- Đăng ký khóa/lớp đang mở, sinh hóa đơn điện tử và trạng thái thanh toán.
- QR đăng nhập web: web sinh thử thách, mobile quét và phê duyệt.
- Trang “Chính sách bảo mật” mới (cập nhật 17/11/2025) theo yêu cầu công bố dữ liệu.
- Mobile app bổ sung “Hồ sơ của tôi”, xem đăng ký và hóa đơn đang chờ.

## 📁 Sơ đồ thư mục

```
QLTTTA_API/
├── QLTTTA_API/          # ASP.NET Core API
├── QLTTTA_WEB/          # ASP.NET Core MVC web
├── qlttta_app_mobile/   # Flutter app
├── DB/                  # Script dựng & vá Oracle schema
├── ops/cloudflare/      # Công cụ public demo qua Cloudflare Tunnel
├── sample_data.sql
└── start-lda-system.bat
```

## 🛠️ Troubleshooting nhanh

1. **API không kết nối DB:** kiểm tra Oracle listener, user/password, `TNS_ADMIN`; chạy lại script cấp quyền trong `DB/`.
2. **Web 404/500:** đảm bảo `dotnet restore` thành công; xem log trong terminal để biết action lỗi.
3. **Mobile bị 401:** xóa cache app hoặc đăng nhập lại để cập nhật `sessionId`; kiểm tra lại `API_BASE_URL`.
4. **Không thể truy cập từ ngoài mạng:** chạy `ops/cloudflare/run-quick-tunnel.ps1` để mở đường hầm tạm thời.

## 🗺️ Roadmap

- [ ] Báo cáo doanh thu nâng cao (lọc theo thời gian, xuất Excel).
- [ ] Đồng bộ lịch giảng cho giáo viên trên mobile.
- [ ] Kết nối cổng thanh toán nội địa & QR Banking.
- [ ] Đa ngôn ngữ cho web & app.

## 📮 Liên hệ

- **Team:** LDA Dev Squad
- **Email:** dev@lda.edu.vn
- **Website demo:** cung cấp qua Cloudflare Tunnel khi cần (xem `ops/cloudflare`).
