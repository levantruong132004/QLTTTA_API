import 'dart:convert';
import 'package:qlttta_app_mobile/services/api_service.dart';
import 'package:shared_preferences/shared_preferences.dart';

class AuthService {
  final ApiService _apiService = ApiService();

  Future<bool> login(String username, String password) async {
    final response = await _apiService.post('auth/login', jsonEncode({
      'username': username,
      'password': password,
    }));

    if (response.statusCode == 200) {
      final body = jsonDecode(response.body) as Map<String, dynamic>;
      final success = body['success'] == true;
      if (!success) return false;

      final token = (body['token'] ?? '') as String;
      final sessionId = (body['sessionId'] ?? '') as String;
      final user = body['user'];

      // Persist credentials locally
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString('auth_token', token);
      await prefs.setString('session_id', sessionId);
      await prefs.setString('username', username);
      if (user != null) {
        await prefs.setString('user_info', jsonEncode(user));
      }

      // Set headers for subsequent requests
      ApiService.setAuth(sessionId: sessionId, token: token);
      return true;
    } else {
      return false;
    }
  }

  Future<bool> register({
    required String fullName,
    required String sex,
    required DateTime dateOfBirth,
    required String phoneNumber,
    required String email,
    String? address,
    required String username,
    required String password,
    required String confirmPassword,
  }) async {
    try {
      final response = await _apiService.post('auth/register', jsonEncode({
        'fullName': fullName,
        'sex': sex,
        'dateOfBirth': dateOfBirth.toIso8601String(),
        'phoneNumber': phoneNumber,
        'email': email,
        'address': address,
        'username': username,
        'password': password,
        'confirmPassword': confirmPassword,
      }));

      if (response.statusCode == 200) {
        final data = jsonDecode(response.body);
        if (data['success'] == true) {
          return true;
        } else {
          throw Exception(data['message'] ?? 'Đăng ký thất bại');
        }
      } else {
        final data = jsonDecode(response.body);
        throw Exception(data['message'] ?? 'Đăng ký thất bại');
      }
    } catch (e) {
      throw Exception('Lỗi kết nối: ${e.toString()}');
    }
  }

  Future<void> logout() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final sessionId = prefs.getString('session_id');
      if (sessionId != null && sessionId.isNotEmpty) {
        // Ensure header is attached
        ApiService.setAuth(sessionId: sessionId, token: prefs.getString('auth_token'));
        await _apiService.post('auth/logout', jsonEncode({}));
      }
    } catch (_) {
      // ignore network errors on logout
    } finally {
      // Clear local session
      final prefs = await SharedPreferences.getInstance();
      await prefs.remove('auth_token');
      await prefs.remove('session_id');
      await prefs.remove('username');
      await prefs.remove('user_info');
      ApiService.clearAuth();
    }
  }
}
