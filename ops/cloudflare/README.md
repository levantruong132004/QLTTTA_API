# Cloudflare Tunnel cho truy cập công khai

Dự án này hỗ trợ xuất bản ứng dụng WEB cục bộ lên Internet thông qua Cloudflare Tunnel. Có 2 cách:

- Quick Tunnel (dùng thử nhanh, URL ngẫu nhiên, không cần tên miền)
- Named Tunnel + DNS (URL ổn định qua subdomain của bạn, chạy như Windows Service)

## A. Cài cloudflared (Windows)

- Tải: https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/
- Kiểm tra: `cloudflared --version`
- Tuỳ chọn qua trình quản lý gói:
  - Chocolatey: `choco install cloudflared` (nếu đã cài choco)
  - Scoop: `scoop install cloudflared` (nếu đã cài scoop)

## B. Quick Tunnel (dễ nhất, URL ngẫu nhiên)

1. Chạy WEB (mặc định: http://localhost:5165)
2. PowerShell tại thư mục repo:

```powershell
# Cách 1: One-liner
cloudflared tunnel --url http://localhost:5165

# Cách 2: Dùng script (kết quả tương đương)
./ops/cloudflare/run-quick-tunnel.ps1
```

Cloudflared sẽ in ra URL dạng: `https://<random>.trycloudflare.com`

3. Cập nhật QR base URL

- Sửa `QLTTTA_WEB/appsettings.Development.json`:

```json
{
  "PublicBaseUrl": "https://<random>.trycloudflare.com"
}
```

Reload trang Admin > Khoá học, bấm "Tạo QR" và quét trên điện thoại.

Lưu ý: Quick Tunnel tạo URL mới mỗi lần chạy. Khi URL thay đổi, cập nhật lại `PublicBaseUrl`.

Mẹo: Nếu bạn tải file `cloudflared.exe` (hoặc `cloudflared-windows-amd64.exe`) và đặt ngay trong thư mục `ops/cloudflare/`, script `run-quick-tunnel.ps1` sẽ tự phát hiện và chạy binary cục bộ này dù bạn chưa thêm PATH. Nếu đã cài bằng MSI, hãy mở PowerShell mới để PATH có hiệu lực.

## C. Named Tunnel + DNS (ổn định, không cần chạy tay)

Yêu cầu: bạn có một tên miền trong Cloudflare (gói Free là đủ). Trỏ nameserver tại nơi mua domain về cặp NS của Cloudflare, chờ propagate.

1. Đăng nhập cloudflared

```powershell
cloudflared tunnel login
```

Chọn account/zone trong trình duyệt để ủy quyền.

2. Tạo tunnel đặt tên (ví dụ: qlttta-web)

```powershell
cloudflared tunnel create qlttta-web
```

Lệnh này tạo file credentials (UUID.json) trong thư mục `.cloudflared` của user.

3. Tạo file cấu hình

- Sao chép `ops/cloudflare/config.template.yml` sang thư mục `.cloudflared` của bạn và cập nhật:
  - `tunnel`: thay bằng UUID tunnel vừa tạo
  - `credentials-file`: trỏ đúng đến file UUID.json
  - `hostname`: thay bằng subdomain của bạn (vd: web.example.com)

4. Tạo bản ghi DNS cho subdomain

```powershell
cloudflared tunnel route dns qlttta-web web.example.com
```

5. Chạy thử tunnel

```powershell
cloudflared tunnel run qlttta-web
```

6. Cài làm Windows Service (tự chạy khi khởi động)

```powershell
cloudflared service install
```

Mặc định service sẽ đọc cấu hình trong thư mục `.cloudflared\config.yml` của user.

7. Cập nhật QR base URL

- Sửa `QLTTTA_WEB/appsettings.json` hoặc `appsettings.Development.json`:

```json
{
  "PublicBaseUrl": "https://web.example.com"
}
```

> Gợi ý: Tránh dùng ký tự không hợp lệ trong tên miền/subdomain (ví dụ dấu gạch dưới \_). Nếu Dashboard báo "Invalid nameservers", hãy về trang quản lý domain (registrar) để đổi NS sang cặp NS Cloudflare cung cấp, sau đó quay lại Dashboard và đợi cập nhật trạng thái.
