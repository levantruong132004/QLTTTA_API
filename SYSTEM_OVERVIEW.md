# 📚 HỆ THỐNG QUẢN LÝ TRUNG TÂM TIN HỌC - TỔNG QUAN

> **Phiên bản**: 2.0 - Đã gộp tính năng từ nhánh `QLTTTA_API-feature-qr-full-public-profile`
> **Ngày cập nhật**: 13/11/2025

---

## 📋 MỤC LỤC

1. [Tổng quan hệ thống](#tổng-quan-hệ-thống)
2. [Kiến trúc & Công nghệ](#kiến-trúc--công-nghệ)
3. [Phân quyền người dùng](#phân-quyền-người-dùng)
4. [Các tính năng chính](#các-tính-năng-chính)
5. [API Endpoints](#api-endpoints)
6. [Hướng dẫn cài đặt](#hướng-dẫn-cài-đặt)
7. [Hướng dẫn sử dụng](#hướng-dẫn-sử-dụng)

---

## 🎯 TỔNG QUAN HỆ THỐNG

Hệ thống quản lý trung tâm tin học là giải pháp toàn diện để quản lý:
- **Học viên** (Students)
- **Khóa học** (Courses)
- **Lớp học** (Classes)
- **Đơn đăng ký** (Registrations)
- **Hóa đơn** (Invoices) với **chữ ký số** (Digital Signature)
- **Thanh toán** (Payments)
- **QR Code** cho profile công khai

---

## 🏗️ KIẾN TRÚC & CÔNG NGHỆ

### Backend API (QLTTTA_API)
- **Framework**: ASP.NET Core 8.0
- **Database**: Oracle 19c
- **ORM**: Dapper + Oracle.ManagedDataAccess
- **Security**: Session-based authentication, VPD (Virtual Private Database)
- **Digital Signature**: RSA-SHA256
- **Email**: SMTP (Gmail)
- **PDF Generation**: QuestPDF

### Frontend Web (QLTTTA_WEB)
- **Framework**: ASP.NET Core MVC
- **UI**: Bootstrap 5, jQuery
- **Charts**: Chart.js
- **Icons**: Font Awesome

### Mobile App (qlttta_app_mobile)
- **Framework**: Flutter/Dart
- **State Management**: Provider pattern
- **HTTP Client**: Dio

---

## 👥 PHÂN QUYỀN NGƯỜI DÙNG

### 1. **Học viên (HOCVIEN)** 🎓
**Quyền truy cập**:
- ✅ Xem khóa học và lớp học (READ)
- ✅ Xem đơn đăng ký của mình
- ✅ Xem hóa đơn của mình
- ✅ Thanh toán hóa đơn
- ✅ QR Code public profile

**Không có quyền**:
- ❌ Tạo/Sửa/Xóa khóa học, lớp học
- ❌ Duyệt đơn đăng ký
- ❌ Tạo hóa đơn

### 2. **Nhân viên học vụ (NHAN_VIEN_HOC_VU)** 👨‍💼
**Quyền truy cập**:
- ✅ Quản lý khóa học (CRUD)
- ✅ Quản lý lớp học (CRUD)
- ✅ Xem tất cả đơn đăng ký
- ✅ **Duyệt** hoặc **Từ chối** đơn đăng ký
- ✅ Gán học viên vào lớp

**Không có quyền**:
- ❌ Tạo hóa đơn
- ❌ Ký số hóa đơn

### 3. **Kế toán (KE_TOAN)** 💰
**Quyền truy cập**:
- ✅ Xem đơn đăng ký **đã được phê duyệt**
- ✅ **Tạo hóa đơn** cho đơn đã duyệt
- ✅ **Ký số hóa đơn** bằng private key
- ✅ Gửi hóa đơn PDF qua email
- ✅ Xác thực chữ ký số

**Không có quyền**:
- ❌ Duyệt/Từ chối đơn đăng ký
- ❌ Sửa/Xóa khóa học, lớp học

---

## 🚀 CÁC TÍNH NĂNG CHÍNH

### ✨ Tính năng mới từ nhánh feature

#### 1. **QR Code cho Public Profile**
- Mỗi học viên có URL public profile riêng
- Tạo QR code tự động khi tạo học viên
- Scan QR để xem profile (không cần đăng nhập)
- URL format: `http://localhost:7158/Student/PublicProfile/{studentId}`

#### 2. **Digital Signature (Chữ ký số)**
- Kế toán ký số hóa đơn bằng RSA private key
- Lưu trữ chữ ký dạng Base64 trong database
- Xác thực chữ ký với public key
- Hiển thị trạng thái: "Đã ký số (Hợp lệ)" / "Đã ký số (Không hợp lệ)"

#### 3. **Email & PDF Invoice**
- Tạo PDF hóa đơn với đầy đủ thông tin
- Gửi tự động qua email cho học viên
- Template email đẹp với HTML

#### 4. **Session-based Authentication**
- Hỗ trợ đa thiết bị (PC + Mobile)
- Header: `X-Session-Id` và `X-Device-Type`
- VPD policy tự động áp dụng quyền theo user

---

## 📡 API ENDPOINTS

### **Khóa học (Courses)**
```
GET    /api/courses              - Xem tất cả khóa học (PUBLIC)
GET    /api/courses/{id}         - Xem chi tiết khóa học (PUBLIC)
POST   /api/courses              - Tạo khóa học (STAFF ONLY)
PUT    /api/courses/{id}         - Sửa khóa học (STAFF ONLY)
DELETE /api/courses/{id}         - Xóa khóa học (STAFF ONLY)
```

### **Lớp học (Classes)**
```
GET    /api/classes              - Xem tất cả lớp (PUBLIC)
GET    /api/classes/{id}         - Xem chi tiết lớp (PUBLIC)
GET    /api/classes/{id}/roster  - Xem danh sách học viên (PUBLIC)
POST   /api/classes              - Tạo lớp (STAFF ONLY)
PUT    /api/classes/{id}         - Sửa lớp (STAFF ONLY)
DELETE /api/classes/{id}         - Xóa lớp (STAFF ONLY)
```

### **Đơn đăng ký (Registrations)**
```
# Nhân viên học vụ
GET    /api/registrations?status=...&classId=...  - Xem tất cả đơn (STAFF)
POST   /api/registrations/{id}/approve            - Duyệt đơn (STAFF)
POST   /api/registrations/{id}/reject             - Từ chối đơn (STAFF)

# Học viên
GET    /api/registrations/my                      - Xem đơn của mình (STUDENT)

# Kế toán
GET    /api/registrations/accountant?courseId=...&classId=...  - Xem đơn đã duyệt (ACCOUNTANT)
GET    /api/registrations/accountant/{id}                      - Chi tiết đơn (ACCOUNTANT)
```

### **Hóa đơn (Invoices)**
```
# Kế toán
POST   /api/invoices                    - Tạo hóa đơn (ACCOUNTANT)
POST   /api/invoices/create-and-sign    - Tạo và ký số hóa đơn (ACCOUNTANT) ⭐
POST   /api/invoices/print-and-email    - In và gửi PDF qua email (ACCOUNTANT)

# Học viên & Kế toán
GET    /api/invoices/by-registration/{registrationId}  - Xem hóa đơn theo đơn đăng ký
GET    /api/invoices/with-signature/{invoiceId}        - Xem hóa đơn có chữ ký số
POST   /api/invoices/student-pay                       - Học viên thanh toán (STUDENT)
```

### **Học viên (Students)**
```
GET    /api/students              - Danh sách học viên
GET    /api/students/{id}         - Chi tiết học viên
POST   /api/students              - Tạo học viên
PUT    /api/students/{id}         - Cập nhật học viên
DELETE /api/students/{id}         - Vô hiệu hóa học viên
GET    /api/students/search       - Tìm kiếm học viên
```

---

## ⚙️ HƯỚNG DẪN CÀI ĐẶT

### 1. **Cài đặt Database**

```sql
-- Chạy các script theo thứ tự:
1. DB/ttta_center_table.sql          -- Tạo bảng chính
2. DB/digital_signature_schema.sql   -- Thêm cột chữ ký số
3. DB/role.sql                       -- Tạo roles và users
4. DB/store_proceduere.sql           -- Stored procedures
5. DB/deploy_permissions_views_procs.sql  -- Phân quyền VPD
```

### 2. **Cấu hình Connection String**

Sửa file `QLTTTA_API/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "OracleDbConnection": "User Id=QLTT_ADMIN;Password=123456;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=100.118.120.99)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=orclpdb)))"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUser": "your-email@gmail.com",
    "SmtpPass": "your-app-password",
    "FromEmail": "your-email@gmail.com"
  },
  "App": {
    "PublicBaseUrl": "http://localhost:7158"
  }
}
```

### 3. **Chạy ứng dụng**

#### Backend API:
```bash
cd QLTTTA_API
dotnet restore
dotnet run
# API chạy tại: http://localhost:5165
```

#### Frontend Web:
```bash
cd QLTTTA_WEB
dotnet restore
dotnet run
# Web chạy tại: http://localhost:7158
```

#### Mobile App:
```bash
cd qlttta_app_mobile
flutter pub get
flutter run
```

### 4. **Tạo Private/Public Key cho Kế toán**

```bash
# Tạo private key
openssl genrsa -out private_key.pem 2048

# Trích xuất public key
openssl rsa -in private_key.pem -pubout -out public_key.pem

# Copy các file vào thư mục gốc project
```

---

## 📖 HƯỚNG DẪN SỬ DỤNG

### **Luồng nghiệp vụ chính**

#### 1. **Nhân viên tạo khóa học và lớp học**
```
1. Đăng nhập với tài khoản nhân viên
2. Vào menu "Khóa học" → Thêm khóa học mới
3. Vào menu "Lớp học" → Thêm lớp học (chọn khóa học đã tạo)
4. Thiết lập lịch học, giảng viên, sĩ số tối đa
```

#### 2. **Học viên đăng ký học**
```
1. Đăng nhập với tài khoản học viên
2. Xem danh sách khóa học và lớp học
3. Gửi đơn đăng ký
4. Theo dõi trạng thái đơn tại "Đơn đăng ký của tôi"
```

#### 3. **Nhân viên duyệt đơn đăng ký**
```
1. Vào menu "Đơn đăng ký"
2. Lọc đơn theo trạng thái "Chờ duyệt"
3. Xem chi tiết đơn
4. Chọn "Duyệt" hoặc "Từ chối"
5. (Tùy chọn) Chuyển học viên sang lớp khác nếu cần
```

#### 4. **Kế toán tạo và ký số hóa đơn** ⭐
```
1. Đăng nhập với tài khoản kế toán
2. Vào menu "Đơn đăng ký đã duyệt"
3. Chọn đơn cần tạo hóa đơn
4. Nhấn "Tạo và ký số hóa đơn"
5. Upload private key file (.pem)
6. Hệ thống tự động:
   - Tạo hóa đơn
   - Ký số với private key
   - Lưu chữ ký vào database
7. (Tùy chọn) In và gửi PDF qua email cho học viên
```

#### 5. **Học viên xem và thanh toán hóa đơn**
```
1. Vào "Đơn đăng ký của tôi"
2. Nhấn "Xem hóa đơn" ở đơn đã duyệt
3. Kiểm tra thông tin hóa đơn và chữ ký số
   - Nếu hiển thị "Đã ký số (Hợp lệ)" → Hóa đơn đáng tin cậy
4. Chọn phương thức thanh toán
5. Hoàn tất thanh toán
```

---

## 🔐 BẢO MẬT

### Session Management
- Session ID được tạo ngẫu nhiên khi đăng nhập
- Lưu trong database (TAI_KHOAN.SESSION_ID_PC / SESSION_ID_MOBILE)
- Hết hạn sau 24h hoặc khi logout
- Header required: `X-Session-Id`, `X-Device-Type`

### VPD (Virtual Private Database)
- Tự động lọc dữ liệu theo quyền user
- Học viên chỉ thấy dữ liệu của mình
- Policy áp dụng trên: DON_DANG_KY, HOA_DON

### Digital Signature
- Algorithm: RSA-SHA256 (2048 bit)
- Private key: Chỉ kế toán giữ
- Public key: Lưu trong database để verify
- Không thể giả mạo chữ ký

---

## 📚 TÀI LIỆU THAM KHẢO

- [Digital Signature README](./DIGITAL_SIGNATURE_README.md)
- [QR Code README](./QR_CODE_README.md)
- [Database Schema](./DB/ttta_center_table.sql)

---

## 🤝 HỖ TRỢ

Nếu gặp vấn đề, vui lòng kiểm tra:
1. Connection string đến Oracle Database
2. SMTP settings cho email
3. Private/Public key files
4. Logs trong `QLTTTA_API/logs/`

---

## 🎉 HOÀN TẤT

Hệ thống đã sẵn sàng sử dụng! Chúc bạn làm việc hiệu quả! 🚀
