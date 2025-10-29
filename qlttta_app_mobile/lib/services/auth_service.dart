import 'dart:convert';
import 'package:qlttta_app_mobile/services/api_service.dart';
import 'package:shared_preferences/shared_preferences.dart';

class AuthService {
  final ApiService _apiService = ApiService();

  Future<bool> login(String username, String password) async {
    final response = await _apiService.post(
      'auth/login',
      jsonEncode({
        'username': username,
        'password': password,
        'deviceType': 'mobile',
      }),
    );

    if (response.statusCode == 200) {
      final data = jsonDecode(response.body);
      if (data['success'] == true) {
        final sid = data['sessionId'] as String?;
        if (sid != null && sid.isNotEmpty) {
          final prefs = await SharedPreferences.getInstance();
          await prefs.setString('sessionId', sid);
          await prefs.setString('username', username);
        }
        return true;
      }
      return false;
    } else {
      return false;
    }
  }

  Future<void> logout({bool notifyServer = true}) async {
    final prefs = await SharedPreferences.getInstance();
    final username = prefs.getString('username') ?? '';
    final sid = prefs.getString('sessionId') ?? '';
    try {
      if (notifyServer && username.isNotEmpty && sid.isNotEmpty) {
        await _apiService.post(
          'auth/logout?username=' + Uri.encodeComponent(username),
          jsonEncode({}),
        );
      }
    } catch (_) {
      // bỏ qua lỗi
    }
    await prefs.remove('sessionId');
    await prefs.remove('username');
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
      final response = await _apiService.post(
        'auth/register',
        jsonEncode({
          'fullName': fullName,
          'sex': sex,
          'dateOfBirth': dateOfBirth.toIso8601String(),
          'phoneNumber': phoneNumber,
          'email': email,
          'address': address,
          'username': username,
          'password': password,
          'confirmPassword': confirmPassword,
        }),
      );

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
}
