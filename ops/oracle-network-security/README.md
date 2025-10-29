# Oracle Network Security (Wire Encryption)

Để tránh lộ lọt thông tin khi bắt gói tin giữa API và Oracle DB, bật mã hóa kênh kết nối.
Bạn có 2 lựa chọn:

1) Oracle Native Network Encryption (NNE) – cấu hình nhanh, không cần chứng chỉ
2) TCPS (TLS/SSL) – an toàn cao hơn, cần wallet/chứng chỉ

## 1) Native Network Encryption (NNE)

Server `sqlnet.ora`:

```
SQLNET.ENCRYPTION_SERVER = REQUIRED
SQLNET.ENCRYPTION_TYPES_SERVER = (AES256)
SQLNET.CRYPTO_CHECKSUM_SERVER = REQUIRED
SQLNET.CRYPTO_CHECKSUM_TYPES_SERVER = (SHA256)
```

Client `sqlnet.ora` (máy chạy API):

```
SQLNET.ENCRYPTION_CLIENT = REQUIRED
SQLNET.ENCRYPTION_TYPES_CLIENT = (AES256)
SQLNET.CRYPTO_CHECKSUM_CLIENT = REQUIRED
SQLNET.CRYPTO_CHECKSUM_TYPES_CLIENT = (SHA256)
```

Xác minh: kiểm tra `V$SESSION_CONNECT_INFO` để thấy Encryption và Crypto-checksums.

## 2) TCPS (TLS/SSL)

- Cấu hình listener dùng TCPS (port 2484), tạo và cấu hình Oracle Wallet cho server và client.
- Bật `SSL_SERVER_DN_MATCH = ON` ở client để xác thực CN của server.

Tài liệu tham khảo (Oracle):
- Oracle Database Security Guide – Configuring Network Data Encryption and Integrity
- Oracle Net Services Administrator's Guide – Configuring Secure Sockets Layer Authentication

Lưu ý: Dù dùng NNE hay TCPS, vẫn phải dùng HTTPS cho web/mobile -> API để mã hóa đoạn đường còn lại.
