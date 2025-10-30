# qlttta_app_mobile

Flutter app for the LDA English Center system. This module implements:

- Đăng nhập (login) via `POST /api/auth/login` (stores token + X-Session-Id)
- Đăng ký (register) via `POST /api/auth/register`
- Đăng xuất (logout) via `POST /api/auth/logout`
- Quên mật khẩu (forgot password) UI placeholder – awaiting backend endpoint

## API base URL

The API client is configured in `lib/services/api_service.dart`:

- Android emulator: `http://10.0.2.2:7158/api`
- If you deploy/run API elsewhere, update the `_baseUrl` accordingly (e.g., LAN IP).

Auth headers are attached automatically after successful login:

- `X-Session-Id: <sessionId>`
- `Authorization: Bearer <token>`

## Quick start

1) Ensure the backend API (QLTTTA_API) is running on port 7158.

2) Run the Flutter app:

```bash
flutter pub get
flutter run
```

## Notes

- After login, session/token are stored in SharedPreferences. Logout clears them and calls the API logout.
- The Students list uses admin-level endpoints on the API side and doesn’t require per-user DB permissions.
- Forgot password: the mobile screen is ready, but the API currently doesn’t expose reset endpoints. Contact center staff or add endpoints later.
