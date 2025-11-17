# 🔐 CẬP NHẬT HỆ THỐNG KÝ SỐ VÀ IN HÓA ĐƠN

> **Ngày cập nhật**: 14/11/2025
> **Phiên bản**: 2.1

---

## 📋 CÁC THAY ĐỔI CHÍNH

### 1. ❌ Bỏ gửi email Private Key khi tạo cặp khóa

**Trước đây:**
- Admin tạo cặp khóa RSA
- Hệ thống tự động gửi private key qua email cho kế toán
- **Rủi ro bảo mật cao** nếu email bị hack

**Bây giờ:**
- Admin tạo cặp khóa RSA
- Private key được **download trực tiếp về máy admin**
- Admin **trao tận tay** private key cho kế toán
- **Bảo mật cao hơn**, không qua email

**File đã sửa:**
- `QLTTTA_API/Services/DigitalSignatureService.cs` - Method `GenerateCenterKeyPairAndSaveAsync()`

---

### 2. 👁️ Học viên chỉ xem được hóa đơn SAU KHI kế toán in

**Trước đây:**
- Kế toán tạo hóa đơn → Học viên xem ngay
- Kế toán ký số → Học viên xem chữ ký ngay
- **Vấn đề**: Học viên thấy hóa đơn trước khi kế toán hoàn tất quy trình

**Bây giờ:**
- Kế toán tạo hóa đơn → **Học viên CHƯA thấy**
- Kế toán ký số → **Học viên CHƯA thấy**
- Kế toán nhấn **"In hóa đơn"** → Set flag `DA_IN = 1` → **Học viên BẮT ĐẦU thấy hóa đơn**

**Luồng hoạt động:**
```
1. Kế toán: Tạo hóa đơn (DA_IN = 0)
2. Kế toán: Ký số hóa đơn
3. Kế toán: Nhấn "In và gửi email" → DA_IN = 1
4. Học viên: Bây giờ mới thấy hóa đơn trong "Đơn đăng ký của tôi"
```

**Database changes:**
```sql
-- Chạy script này để thêm cột DA_IN
-- File: DB/add_printed_flag_to_invoice.sql

ALTER TABLE QLTT_ADMIN.HOA_DON ADD DA_IN NUMBER(1) DEFAULT 0 NOT NULL;
COMMENT ON COLUMN QLTT_ADMIN.HOA_DON.DA_IN IS 'Đã in hóa đơn: 0 = Chưa in, 1 = Đã in';
```

**Files đã sửa:**
- `QLTTTA_API/Models/Entities.cs` - Thêm property `IsPrinted`
- `QLTTTA_API/Services/InvoiceService.cs` - Map cột `DA_IN`, set = 1 khi in
- `QLTTTA_API/Services/ProfileService.cs` - Chỉ lấy hóa đơn có `DA_IN = 1`

---

### 3. 🎨 PDF hóa đơn đẹp chuyên nghiệp (QuestPDF)

**Trước đây:**
- Sử dụng DinkToPdf (convert HTML → PDF)
- Khó kiểm soát layout, font, màu sắc
- File PDF đôi khi hiển thị lỗi font

**Bây giờ:**
- Sử dụng **QuestPDF** - thư viện tạo PDF chuyên nghiệp cho .NET
- Layout đẹp, rõ ràng, dễ đọc
- Hỗ trợ table, màu sắc, font Unicode (tiếng Việt)

**Giao diện PDF mới:**
```
┌──────────────────────────────────────────┐
│        TRUNG TÂM TIN HỌC               │
│         HÓA ĐƠN HỌC PHÍ                 │
│    Mã hóa đơn: HD_20251114_1234        │
├──────────────────────────────────────────┤
│ THÔNG TIN HỌC VIÊN                      │
│ Họ tên: Nguyễn Văn A                    │
│ Khóa học: Lập trình Python              │
│ Lớp: Lớp Python cơ bản 01               │
├──────────────────────────────────────────┤
│ CHI TIẾT HÓA ĐƠN                        │
│ ┌─────────────────────┬──────────────┐ │
│ │ Nội dung            │ Số tiền      │ │
│ ├─────────────────────┼──────────────┤ │
│ │ Học phí khóa Python │ 2,000,000 VNĐ│ │
│ ├─────────────────────┼──────────────┤ │
│ │ Tổng cộng           │ 2,000,000 VNĐ│ │
│ └─────────────────────┴──────────────┘ │
│                                          │
│ Ngày tạo: 14/11/2025 10:30             │
│ Hạn thanh toán: 21/11/2025              │
│                                          │
│ ĐÃ KÝ SỐ ✓                              │
│ Ngày ký: 14/11/2025 10:35              │
├──────────────────────────────────────────┤
│ Cảm ơn bạn đã tin tưởng sử dụng         │
│     dịch vụ của chúng tôi!              │
└──────────────────────────────────────────┘
```

**Cài đặt QuestPDF:**
```bash
cd QLTTTA_API
dotnet add package QuestPDF
```

**Files đã sửa:**
- `QLTTTA_API/Services/InvoiceService.cs` - Method `GenerateInvoicePdf()`
- Thay thế DinkToPdf converter bằng QuestPDF Document API

---

## 🚀 HƯỚNG DẪN SỬ DỤNG MỚI

### A. Admin tạo cặp khóa cho Trung tâm

1. Đăng nhập với tài khoản Admin
2. Vào **"Quản lý chữ ký số"**
3. Nhập thông tin trung tâm:
   - Tên trung tâm
   - Địa chỉ
   - Số điện thoại
4. Nhấn **"Tạo cặp khóa"**
5. **QUAN TRỌNG**: 
   - Hệ thống sẽ tự động download file `private_key.pem` về máy
   - **KHÔNG gửi qua email nữa**
   - Admin trao tận tay file này cho Kế toán
   - Kế toán lưu file vào nơi an toàn (USB, mật khẩu bảo vệ)

### B. Kế toán tạo và ký hóa đơn

1. Đăng nhập với tài khoản Kế toán
2. Vào **"Đơn đăng ký đã duyệt"**
3. Chọn đơn cần tạo hóa đơn
4. Nhấn **"Xuất hóa đơn"**
5. Nhập:
   - Hạn thanh toán
   - Số tiền
6. Nhấn **"Tạo hóa đơn"**
7. Upload file `private_key.pem` (đã nhận từ Admin)
8. Nhấn **"Ký số"**
9. **QUAN TRỌNG**: Nhấn **"In và gửi email"** để:
   - Tạo PDF hóa đơn đẹp
   - Gửi qua email cho học viên
   - **Set flag DA_IN = 1** → Học viên bắt đầu thấy hóa đơn

### C. Học viên xem và thanh toán hóa đơn

1. Đăng nhập với tài khoản Học viên
2. Vào **"Đơn đăng ký của tôi"**
3. **Chỉ thấy hóa đơn sau khi kế toán in** (DA_IN = 1)
4. Nhấn **"Xem hóa đơn"** để xem chi tiết:
   - Mã hóa đơn
   - Số tiền
   - Hạn thanh toán
   - Trạng thái chữ ký số: **"Đã ký số (Hợp lệ)"** ✓
5. Nhấn **"Thanh toán"** để thực hiện thanh toán

---

## 📊 SO SÁNH TRƯỚC VÀ SAU

| Tính năng | Trước (v2.0) | Sau (v2.1) |
|-----------|--------------|------------|
| **Gửi Private Key qua email** | ✅ Có (RỦI RO) | ❌ Không (AN TOÀN) |
| **Học viên thấy hóa đơn** | Ngay khi tạo | Sau khi kế toán in |
| **PDF hóa đơn** | HTML → PDF (DinkToPdf) | QuestPDF (Chuyên nghiệp) |
| **Font tiếng Việt** | Đôi khi lỗi | Hoàn hảo |
| **Bảo mật Private Key** | Trung bình | Cao |
| **Kiểm soát quy trình** | Kém | Tốt |

---

## 🔧 CÀI ĐẶT

### 1. Chạy script SQL thêm cột DA_IN

```bash
# Kết nối vào Oracle SQL Developer hoặc SQL*Plus
sqlplus QLTT_ADMIN/123456@orclpdb

# Chạy file:
@DB/add_printed_flag_to_invoice.sql
```

### 2. Restart API và Web

```bash
# Restart API
cd QLTTTA_API
dotnet build
dotnet run

# Restart Web (Terminal mới)
cd QLTTTA_WEB
dotnet build
dotnet run
```

### 3. Xóa cache trình duyệt

```
Nhấn Ctrl + Shift + Delete
→ Xóa cache và cookies
→ F5 để refresh
```

---

## ✅ KIỂM TRA

### Test flow hoàn chỉnh:

1. **Admin tạo khóa** → Download được `private_key.pem`
2. **Kế toán tạo hóa đơn** → Học viên CHƯA thấy
3. **Kế toán ký số** → Học viên CHƯA thấy
4. **Kế toán in hóa đơn** → Học viên BẮT ĐẦU thấy
5. **Học viên mở email** → Thấy PDF đẹp, rõ ràng
6. **Học viên vào web** → Thấy "Đã ký số (Hợp lệ)" ✓

---

## 🎉 HOÀN TẤT

Hệ thống đã được cập nhật với:
- ✅ Bảo mật private key cao hơn (không qua email)
- ✅ Kiểm soát quy trình tốt hơn (in mới thấy)
- ✅ PDF hóa đơn đẹp, chuyên nghiệp

**Chúc bạn sử dụng hiệu quả!** 🚀
