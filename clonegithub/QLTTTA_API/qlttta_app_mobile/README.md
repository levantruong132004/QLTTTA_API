# qlttta_app_mobile

Ứng dụng di động (Flutter) cho hệ thống Quản Lý Trung Tâm Tiếng Anh (QLTTTA).

## 1. Mục tiêu
Hỗ trợ học viên:
- Đăng nhập / lưu phiên (Session Id từ API)
- Xem danh sách khóa học mở
- Gửi yêu cầu đăng ký khóa (kể cả khi chưa có lớp tuyển sinh – sau bản vá `SP_GUI_YEU_CAU_DANG_KY_KHOA`)
- Xem các đăng ký của mình và trạng thái duyệt
- Thanh toán (sau khi tích hợp) và xem hóa đơn / xác thực PDF (phase sau)

## 2. Kiến trúc tổng quan
- Frontend Mobile: Flutter (Dart)
- Backend API: ASP.NET Core (`QLTTTA_API` / `QLTTTA_WEB` repo chung)
- Database: Oracle (scripts trong thư mục `DB/` của repo gốc)
- Cơ chế phiên: Header `X-Session-Id` do API trả về (lưu trong `SharedPreferences`)

## 3. Cấu trúc thư mục (rút gọn)
```
qlttta_app_mobile/
	lib/
		main.dart                // Entry point
		services/
			api_service.dart       // Wrapper gọi REST API
		screens/                 // (Nếu có) các màn hình UI
		widgets/                 // (Nếu có) các component tái sử dụng
	pubspec.yaml               // Khai báo dependencies
	README.md                  // Tài liệu này
```

## 4. API Base URL
Mặc định trong `lib/services/api_service.dart`:
```dart
static const String _baseUrl = 'http://10.0.2.2:7158/api';
```
`10.0.2.2` là địa chỉ truy cập máy host khi chạy Android Emulator.

Đổi khi cần triển khai thực tế:
- Máy thật cùng mạng LAN: `http://<IP_MAY_CHU>:7158/api`
- Public qua Cloudflare Tunnel / Reverse proxy: `https://<domain_public>/api`

Khuyến nghị: Trích xuất `_baseUrl` vào file config/env nếu chuyển sang nhiều môi trường (dev, staging, prod).

## 5. Thiết lập & Chạy (Windows PowerShell)
```powershell
# Cài Flutter nếu chưa có (tham khảo https://docs.flutter.dev/get-started/install)
flutter --version

# Cài các packages khai báo trong pubspec.yaml
flutter pub get

# Chạy trên Android emulator hoặc thiết bị thật
flutter run

# Build APK release (sau khi kiểm thử xong)
flutter build apk --release
```

## 6. Quy trình đăng ký khóa học (sau bản vá)
1. Học viên chọn khóa → Gửi yêu cầu → API tạo đơn (có thể chưa gán lớp)
2. Nhân viên/Quản trị duyệt và gán lớp nếu chưa có
3. Học viên xem trạng thái trong màn hình "Đăng ký của tôi"

## 7. Lưu ý tích hợp
- Luồng login cần lưu `sessionId` → gửi ở mỗi request qua header `X-Session-Id`
- Thêm header nhận dạng thiết bị: `X-Device-Type: mobile`
- Dữ liệu ngày (Oracle) cần chuẩn hoá sang UTC hoặc timezone locale khi hiển thị

## 8. Các bước mở rộng kế tiếp
- Thêm màn hình thanh toán hóa đơn
- Hiển thị QR / xác thực PDF chứng chỉ
- Quét QR để tìm đơn đăng ký (đã thêm): Mở màn hình Quản lý đăng ký → icon QR trên AppBar → quét mã chứa mã đăng ký (MA_DANG_KY) hoặc ID đơn.

### Quét QR tìm đăng ký (mới)
- Backend: GET /api/Registrations/search?q=... hỗ trợ các định dạng:
	- REG:ABC123 (mã đăng ký)
	- REGID:1001 (ID đơn)
	- JSON {"registrationCode":"ABC123"} hoặc {"registrationId":1001}
	- Chuỗi thuần: ABC123 hoặc 1001
- Mobile: dùng mobile_scanner, yêu cầu quyền Camera trên Android.
- AndroidManifest đã khai báo CAMERA. Nếu build lần đầu, nhớ chạy:
	- flutter pub get
	- flutter run
- Push notification (Firebase Cloud Messaging)
- Theme đồng bộ với giao diện web retro vintage

## 9. Khắc phục sự cố thường gặp
| Vấn đề | Nguyên nhân | Cách xử lý |
|--------|-------------|------------|
| Ứng dụng báo lỗi 401 | Session Id hết hạn | Yêu cầu đăng nhập lại, refresh token (nếu có) |
| Không gửi được đăng ký | Chưa chạy bản vá procedure | Kiểm tra `fix_register_course_proc.sql` đã áp dụng chưa |
| Không truy cập được API từ emulator | Sai base URL | Đảm bảo dùng `10.0.2.2` thay vì `localhost` |
| Lỗi CORS (nếu build web) | Thiếu cấu hình backend | Bổ sung origin vào ASP.NET Core CORS policy |

## 10. Bản quyền & giấy phép
Mã nguồn nội bộ phục vụ xây dựng hệ thống QLTTTA. Không sử dụng lại ngoài phạm vi dự án khi chưa có sự cho phép.

---
Nếu cần bổ sung tính năng mới, hãy cập nhật mục **Các bước mở rộng kế tiếp** và chi tiết hoá API ở một tài liệu duy nhất thay vì tạo thêm nhiều file .md.
