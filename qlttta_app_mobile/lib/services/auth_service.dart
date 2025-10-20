import 'dart:convert';
import 'package:qlttta_app_mobile/services/api_service.dart';

class AuthService {
  final ApiService _apiService = ApiService();

  Future<bool> login(String username, String password) async {
    final response = await _apiService.post('auth/login', jsonEncode({
      'username': username,
      'password': password,
    }));

    if (response.statusCode == 200) {
      // TODO: Handle successful login, e.g., store token
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
}
