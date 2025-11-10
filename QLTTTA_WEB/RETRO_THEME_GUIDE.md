# 🎨 Retro Vintage Theme Guide - Hệ thống Quản lý Tiếng Anh LDA

## Tổng quan
Giao diện web đã được nâng cấp toàn diện với theme **Retro Vintage** - phong cách cổ điển sang trọng, lấy cảm hứng từ các tài liệu văn phòng thời kỳ đầu thế kỷ 20.

**Lưu ý quan trọng về font**: 
- Toàn bộ hệ thống sử dụng **Arial/Helvetica** để hỗ trợ tiếng Việt tốt nhất
- Tránh lỗi hiển thị ký tự đặc biệt (như "tiếng" bị hiện thành "tiê'ng")

---

## 🎯 Hệ thống Buttons - Tất cả buttons đã được chuẩn hóa

### Button Variants (Màu sắc)

#### Primary (Nâu - Mặc định)
```html
<button class="btn btn-primary">Xem lớp</button>
```
- Màu nâu chính của theme
- Dùng cho hành động chính, quan trọng

#### Success (Xanh olive)
```html
<button class="btn btn-success">Lưu</button>
<button class="btn btn-success">Thêm</button>
```
- Màu xanh olive cổ điển
- Dùng cho hành động tích cực: lưu, thêm, xác nhận

#### Danger (Đỏ burgundy)
```html
<button class="btn btn-danger">Xóa</button>
```
- Màu đỏ burgundy sang trọng
- Dùng cho hành động nguy hiểm: xóa, hủy

#### Warning (Vàng gold)
```html
<button class="btn btn-warning">Cảnh báo</button>
```
- Màu vàng gold
- Dùng cho thông báo cảnh báo

#### Info (Xanh teal)
```html
<button class="btn btn-info">Thông tin</button>
```
- Màu xanh teal
- Dùng cho thông tin bổ sung

#### Secondary (Xám)
```html
<button class="btn btn-secondary">Hủy</button>
```
- Màu xám trung tính
- Dùng cho hành động phụ

### Button Outlines (Viền trong suốt)

```html
<button class="btn btn-outline-primary">Xem lớp</button>
<button class="btn btn-outline-success">Lưu</button>
<button class="btn btn-outline-danger">Xóa</button>
```

### Button Sizes

#### Small
```html
<button class="btn btn-sm btn-primary">Nhỏ</button>
```

#### Normal (mặc định)
```html
<button class="btn btn-primary">Bình thường</button>
```

#### Large
```html
<button class="btn btn-lg btn-primary">Lớn</button>
```

### Button Block (Chiếm full width)
```html
<button class="btn btn-primary w-100">Xem tất cả và đăng ký</button>
```

---

## 📝 Forms - Hệ thống biểu mẫu

### Input Fields
```html
<div class="form-group">
    <label class="form-label">Họ tên</label>
    <input type="text" class="form-control" placeholder="Nhập họ tên...">
</div>
```

### Select Dropdown
```html
<div class="form-group">
    <label class="form-label">Chọn khóa học</label>
    <select class="form-select">
        <option>Tất cả khóa học</option>
        <option>Giao tiếp cơ bản</option>
    </select>
</div>
```

### Textarea
```html
<div class="form-group">
    <label class="form-label">Mô tả</label>
    <textarea class="form-control" rows="4"></textarea>
</div>
```

### Checkbox & Radio
```html
<div class="form-check">
    <input type="checkbox" id="check1">
    <label for="check1">Tôi đồng ý điều khoản</label>
</div>

<div class="form-check">
    <input type="radio" name="gender" id="male">
    <label for="male">Nam</label>
</div>
```

---

## 🎴 Cards - Thẻ nội dung

### Card cơ bản
```html
<div class="retro-card">
    <div class="retro-card-header">
        <h3 class="retro-card-title">Tiêu đề</h3>
    </div>
    <div class="retro-card-body">
        Nội dung của card...
    </div>
    <div class="retro-card-footer">
        Footer (tùy chọn)
    </div>
</div>
```

### Card với góc trang trí
```html
<div class="retro-card corner-decorated">
    ...
</div>
```

---

## 📊 Tables - Bảng dữ liệu

```html
<table class="retro-table">
    <thead>
        <tr>
            <th>Mã</th>
            <th>Tên</th>
            <th>Thao tác</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>GT_CB</td>
            <td>Giao tiếp cơ bản</td>
            <td>
                <div class="table-actions">
                    <button class="btn btn-sm btn-primary">Sửa</button>
                    <button class="btn btn-sm btn-danger">Xóa</button>
                </div>
            </td>
        </tr>
    </tbody>
</table>
```

---

## 🔔 Alerts - Thông báo

### Success
```html
<div class="retro-alert retro-alert-success">
    <div class="retro-alert-content">
        Lưu thành công!
    </div>
</div>
```

### Danger
```html
<div class="retro-alert retro-alert-danger">
    <div class="retro-alert-content">
        Có lỗi xảy ra!
    </div>
</div>
```

### Warning
```html
<div class="retro-alert retro-alert-warning">
    <div class="retro-alert-content">
        Cảnh báo: Vui lòng kiểm tra lại!
    </div>
</div>
```

### Info
```html
<div class="retro-alert retro-alert-info">
    <div class="retro-alert-content">
        Thông tin: Không có lớp cho khóa học đã chọn.
    </div>
</div>
```

---

## 🏷️ Badges - Nhãn

```html
<span class="retro-badge">Mặc định</span>
<span class="retro-badge retro-badge-success">Đã duyệt</span>
<span class="retro-badge retro-badge-danger">Từ chối</span>
<span class="retro-badge retro-badge-warning">Chờ duyệt</span>
```

---

## 🎨 Utility Classes

### Spacing
```html
<!-- Margins -->
<div class="mt-3">Margin top 1.5rem</div>
<div class="mb-4">Margin bottom 2rem</div>

<!-- Padding -->
<div class="p-2">Padding 1rem</div>
```

### Text Alignment
```html
<div class="text-center">Căn giữa</div>
<div class="text-left">Căn trái</div>
<div class="text-right">Căn phải</div>
```

### Display & Flex
```html
<div class="d-flex justify-content-between align-items-center gap-2">
    <span>Item 1</span>
    <span>Item 2</span>
</div>
```

### Width
```html
<div class="w-100">100% width</div>
<div class="w-50">50% width</div>
```

---

## 🎭 Decorative Elements

### Vintage Ornament (Họa tiết trang trí)
```html
<div class="vintage-ornament"></div>
```

### Vintage Divider (Đường phân cách)
```html
<div class="vintage-divider"></div>
```

### Vintage Stamp (Con dấu)
```html
<span class="vintage-stamp">Đã thanh toán</span>
```

---

## 📱 Responsive Design

Theme tự động responsive cho:
- Desktop (> 768px)
- Tablet (768px - 576px)
- Mobile (< 576px)

Buttons và tables tự động điều chỉnh kích thước phù hợp với màn hình.

---

## 🎨 Color Palette

### Primary Colors
- **Vintage Brown**: `#7d5a3b` - Nâu chính
- **Vintage Dark Brown**: `#4a3526` - Nâu đậm
- **Vintage Gold**: `#b8860b` - Vàng gold
- **Vintage Rust**: `#a85832` - Đỏ gỉ

### Accent Colors
- **Vintage Olive**: `#6b8e23` - Xanh olive (Success)
- **Vintage Burgundy**: `#7c2d37` - Đỏ burgundy (Danger)

### Neutral Colors
- **Vintage Cream**: `#f4ead5` - Kem
- **Vintage White**: `#faf8f3` - Trắng ngà
- **Vintage Gray**: `#8b7d6b` - Xám

---

## 💡 Best Practices

1. **Sử dụng đúng màu cho đúng mục đích**:
   - Primary (brown) cho hành động chính
   - Success (olive) cho lưu, thêm
   - Danger (burgundy) cho xóa
   - Secondary (gray) cho hành động phụ

2. **Kết hợp icon với button**:
   ```html
   <button class="btn btn-primary">
       <i class="fas fa-search"></i> Tìm
   </button>
   ```

3. **Responsive**:
   - Dùng `w-100` cho buttons trên mobile
   - Tables tự động scroll ngang

4. **Accessibility**:
   - Luôn có label cho form controls
   - Dùng màu có độ tương phản tốt

5. **Tiếng Việt**:
   - Font Arial/Helvetica được chọn để hỗ trợ tốt nhất cho tiếng Việt
   - Tránh dùng Times New Roman vì có vấn đề với dấu tiếng Việt

---

## 🔧 Customization

Tất cả biến màu được định nghĩa trong `:root` của `retro-vintage.css`:

```css
:root {
    --vintage-brown: #7d5a3b;
    --vintage-gold: #b8860b;
    /* ... */
}
```

Có thể override bằng cách thêm custom CSS sau khi load `retro-vintage.css`.

---

## 📞 Hỗ trợ

Mọi thắc mắc về theme vui lòng liên hệ team phát triển.

**Version**: 1.0  
**Last Updated**: November 11, 2025
