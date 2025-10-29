import 'dart:async';
import 'dart:convert';
import 'package:qlttta_app_mobile/services/api_service.dart';
import 'package:qlttta_app_mobile/services/auth_manager.dart';
import 'package:shared_preferences/shared_preferences.dart';

class SessionMonitor {
  static final SessionMonitor _instance = SessionMonitor._internal();
  factory SessionMonitor() => _instance;
  SessionMonitor._internal();

  final _api = ApiService();
  Timer? _timer;

  void start() {
    _timer?.cancel();
    _timer = Timer.periodic(const Duration(seconds: 5), (_) async {
      try {
        final prefs = await SharedPreferences.getInstance();
        final sid = prefs.getString('sessionId');
        final username = prefs.getString('username');
        if (sid == null ||
            sid.isEmpty ||
            username == null ||
            username.isEmpty) {
          return; // chưa đăng nhập
        }
        // Gọi check-session ở API
        final res = await _api.get(
          'auth/check-session?username=' +
              Uri.encodeComponent(username) +
              '&sessionId=' +
              Uri.encodeComponent(sid),
        );
        if (res.statusCode == 200) {
          final data = jsonDecode(res.body);
          if (data is Map && data['status'] == 'invalid') {
            // Phiên không hợp lệ -> phát sự kiện và dừng timer
            AuthManager().notifyUnauthorized(
              'Phiên đăng nhập đã bị thay thế do đăng nhập ở thiết bị khác.',
            );
          }
        }
      } catch (_) {
        // im lặng
      }
    });
  }

  void stop() {
    _timer?.cancel();
    _timer = null;
  }
}
