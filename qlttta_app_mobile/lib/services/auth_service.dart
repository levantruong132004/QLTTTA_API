import 'dart:convert';
import 'package:qlttta_app_mobile/services/api_service.dart';
import 'package:shared_preferences/shared_preferences.dart';

class AuthService {
  final ApiService _apiService = ApiService();

  Future<Map<String, dynamic>> login(String username, String password) async {
    try {
      final response = await _apiService.post(
        'auth/login',
        {
          'username': username,
          'password': password,
          'deviceType': 'mobile',
        },
      );

      final data = jsonDecode(response.body);
      print("DEBUG LOGIN RESPONSE: $data"); // Debug print

      if (response.statusCode == 200 && data['success'] == true) {
        if (data['user'] != null) {
          final roleId = data['user']['roleId'];
          print("DEBUG PARSED ROLE ID: $roleId"); // Debug print

          final prefs = await SharedPreferences.getInstance();
          await prefs.setString('sessionId', data['sessionId']);
          await prefs.setString('username', data['user']['username']);
          await prefs.setString('fullName', data['user']['fullName'] ?? '');
          
          if (roleId != null) {
            await prefs.setInt('roleId', roleId is int ? roleId : int.tryParse(roleId.toString()) ?? 0);
          }
        }
        return {'success': true, 'message': data['message']};
      } else {
        return {
          'success': false,
          'message': data['message'] ?? 'Đăng nhập thất bại',
        };
      }
    } catch (e) {
      return {
        'success': false,
        'message': 'Lỗi kết nối: ${e.toString()}',
      };
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
          {},
        );
      }
    } catch (_) {
      // ignore network errors on logout
    }
    await prefs.remove('sessionId');
    await prefs.remove('username');
    await prefs.remove('fullName');
    await prefs.remove('roleId');
  }

  // Forgot password flow
  Future<Map<String, dynamic>> initiateForgotPassword({
    required String username,
    required String email,
  }) async {
    try {
      final response = await _apiService.post(
        'auth/forgot/initiate',
        {
          'username': username,
          'email': email,
        },
      );

      if (response.statusCode == 200) {
        final data = jsonDecode(response.body);
        if (data['success'] == true) {
          return {
            'success': true,
            'correlationId': data['correlationId'],
            'message': data['message'] ?? 'Đã gửi OTP đến email',
          };
        } else {
          return {
            'success': false,
            'message': data['message'] ?? 'Không thể gửi OTP',
            'shouldRegister': data['shouldRegister'] ?? false,
          };
        }
      } else {
        final data = jsonDecode(response.body);
        return {
          'success': false,
          'message': data['message'] ?? 'Lỗi kết nối',
        };
      }
    } catch (e) {
      return {
        'success': false,
        'message': 'Lỗi kết nối: ${e.toString()}',
      };
    }
  }

  Future<Map<String, dynamic>> verifyForgotPassword({
    required String correlationId,
    required String otp,
    required String newPassword,
    required String confirmPassword,
  }) async {
    try {
      final response = await _apiService.post(
        'auth/forgot/verify',
        {
          'correlationId': correlationId,
          'otp': otp,
          'newPassword': newPassword,
          'confirmNewPassword': confirmPassword,
        },
      );

      if (response.statusCode == 200) {
        final data = jsonDecode(response.body);
        if (data['success'] == true) {
          return {
            'success': true,
            'message': data['message'] ?? 'Đặt lại mật khẩu thành công',
          };
        } else {
          return {
            'success': false,
            'message': data['message'] ?? 'Mã OTP không hợp lệ',
          };
        }
      } else {
        final data = jsonDecode(response.body);
        return {
          'success': false,
          'message': data['message'] ?? 'Lỗi kết nối',
        };
      }
    } catch (e) {
      return {
        'success': false,
        'message': 'Lỗi kết nối: ${e.toString()}',
      };
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
      // Default path (no-OTP) kept for compatibility but backend recommends OTP
      final response = await _apiService.post('auth/register', {
        'fullName': fullName,
        'sex': sex,
        'dateOfBirth': dateOfBirth.toIso8601String(),
        'phoneNumber': phoneNumber,
        'email': email,
        'address': address,
        'username': username,
        'password': password,
        'confirmPassword': confirmPassword,
      });

      if (response.statusCode == 200) {
        final data = jsonDecode(response.body);
        if (data['success'] == true) return true;
        throw Exception(data['message'] ?? 'Đăng ký thất bại');
      } else {
        final data = jsonDecode(response.body);
        throw Exception(data['message'] ?? 'Đăng ký thất bại');
      }
    } catch (e) {
      throw Exception('Lỗi kết nối: ${e.toString()}');
    }
  }

  // Registration with OTP
  Future<Map<String, dynamic>> initiateRegisterOtp({
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
      final res = await _apiService.post('auth/register/initiate-otp', {
        'fullName': fullName,
        'sex': sex,
        'dateOfBirth': dateOfBirth.toIso8601String(),
        'phoneNumber': phoneNumber,
        'email': email,
        'address': address,
        'username': username,
        'password': password,
        'confirmPassword': confirmPassword,
      });
      final data = jsonDecode(res.body);
      if (res.statusCode == 200 && data['success'] == true) {
        return {
          'success': true,
          'correlationId': data['correlationId'],
          'message': data['message'] ?? 'Đã gửi OTP đến email',
        };
      }
      return {
        'success': false,
        'message': data['message'] ?? 'Không thể gửi OTP',
      };
    } catch (e) {
      return {
        'success': false,
        'message': 'Lỗi kết nối: ${e.toString()}',
      };
    }
  }

  Future<Map<String, dynamic>> verifyRegisterOtp({
    required String correlationId,
    required String otp,
  }) async {
    try {
      final res = await _apiService.post('auth/register/verify-otp', {
        'correlationId': correlationId,
        'otp': otp,
      });
      final data = jsonDecode(res.body);
      if (res.statusCode == 200 && data['success'] == true) {
        return {
          'success': true,
          'message': data['message'] ?? 'Đăng ký thành công',
        };
      }
      return {
        'success': false,
        'message': data['message'] ?? 'Xác thực OTP thất bại',
      };
    } catch (e) {
      return {
        'success': false,
        'message': 'Lỗi kết nối: ${e.toString()}',
      };
    }
  }

  // ========== QR Login ==========
  Future<Map<String, dynamic>> createQrForMobileLogin() async {
    final res = await _apiService.post('auth/qr/challenge', {
      'requesterDevice': 'mobile',
      'requesterInfo': 'mobile-login',
    });
    final data = jsonDecode(res.body);
    return data is Map<String, dynamic> ? data : {'success': false};
  }

  Future<Map<String, dynamic>> qrStatus(String id) async {
    final res = await _apiService.get('auth/qr/status?id=' + Uri.encodeComponent(id));
    final data = jsonDecode(res.body);
    return data is Map<String, dynamic> ? data : {'success': false};
  }

  Future<bool> consumeQrForMobile(String id, String grantToken) async {
    final res = await _apiService.post('auth/qr/consume', {
      'id': id,
      'grantToken': grantToken,
      'deviceType': 'mobile',
    });
    if (res.statusCode == 200) {
      final data = jsonDecode(res.body);
      if (data['success'] == true && data['sessionId'] != null) {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString('sessionId', data['sessionId']);
        if (data['user'] != null) {
          final u = data['user'];
          await prefs.setString('username', (u['username'] ?? '').toString());
          if (u['fullName'] != null) await prefs.setString('fullName', u['fullName']);
          final roleId = u['roleId'];
          if (roleId != null) await prefs.setInt('roleId', roleId is int ? roleId : int.tryParse(roleId.toString()) ?? 0);
        }
        return true;
      }
    }
    return false;
  }

  Future<Map<String, dynamic>> qrScan(String id) async {
    final res = await _apiService.post('auth/qr/scan', { 'id': id });
    final data = jsonDecode(res.body);
    return data is Map<String, dynamic> ? data : {'success': false};
  }

  Future<Map<String, dynamic>> qrApprove(String id, bool approve) async {
    final res = await _apiService.post('auth/qr/approve', { 'id': id, 'approve': approve });
    final data = jsonDecode(res.body);
    return data is Map<String, dynamic> ? data : {'success': false};
  }
}
