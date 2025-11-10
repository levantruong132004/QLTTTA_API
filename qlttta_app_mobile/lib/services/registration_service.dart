import 'dart:convert';
import 'package:qlttta_app_mobile/models/registration.dart';
import 'package:qlttta_app_mobile/models/open_class.dart';
import 'package:qlttta_app_mobile/services/api_service.dart';

class RegistrationService {
  final ApiService _apiService = ApiService();

  // Học viên đăng ký khóa học (with detailed message)
  Future<Map<String, dynamic>> registerCourseWithMessage(String courseCode) async {
    try {
      print('Registering course: $courseCode');
      final response = await _apiService.post('profile/register-course', {
        'courseCode': courseCode,
      });
      
      print('Register course response status: ${response.statusCode}');
      print('Register course response body: ${response.body}');
      
      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        final success = body['success'] ?? body['Success'] ?? false;
        final message = body['message'] ?? body['Message'] ?? '';
        print('Register course success: $success, message: $message');
        return {'success': success, 'message': message};
      } else {
        final body = jsonDecode(response.body);
        final message = body['message'] ?? body['Message'] ?? 'Lỗi không xác định';
        print('Register course failed with status: ${response.statusCode}, message: $message');
        return {'success': false, 'message': message};
      }
    } catch (e) {
      print('Error registering course: $e');
      return {'success': false, 'message': 'Lỗi kết nối: $e'};
    }
  }

  // Học viên đăng ký khóa học
  Future<bool> registerCourse(String courseCode) async {
    final result = await registerCourseWithMessage(courseCode);
    return result['success'] ?? false;
  }

  // Học viên xem đơn của mình
  Future<List<Registration>> getMyRegistrations() async {
    try {
      final response = await _apiService.get('Registrations/my');
      if (response.statusCode == 200) {
        final List<dynamic> data = jsonDecode(response.body);
        return data.map((json) => Registration.fromJson(json)).toList();
      }
      return [];
    } catch (e) {
      print('Error fetching my registrations: $e');
      return [];
    }
  }

  // Admin/NhanVienHocVu xem tất cả đơn
  Future<List<Registration>> getAllRegistrations({String? status, int? classId}) async {
    try {
      String url = 'Registrations';
      List<String> params = [];
      if (status != null) params.add('status=$status');
      if (classId != null) params.add('classId=$classId');
      if (params.isNotEmpty) url += '?${params.join('&')}';

      final response = await _apiService.get(url);
      if (response.statusCode == 200) {
        final List<dynamic> data = jsonDecode(response.body);
        return data.map((json) => Registration.fromJson(json)).toList();
      }
      return [];
    } catch (e) {
      print('Error fetching all registrations: $e');
      return [];
    }
  }

  // Kế toán xem đơn (lọc theo khóa/lớp)
  Future<List<Registration>> getAccountantRegistrations({int? courseId, int? classId}) async {
    try {
      String url = 'Registrations/accountant';
      List<String> params = [];
      if (courseId != null) params.add('courseId=$courseId');
      if (classId != null) params.add('classId=$classId');
      if (params.isNotEmpty) url += '?${params.join('&')}';

      final response = await _apiService.get(url);
      if (response.statusCode == 200) {
        final List<dynamic> data = jsonDecode(response.body);
        return data.map((json) => Registration.fromJson(json)).toList();
      }
      return [];
    } catch (e) {
      print('Error fetching accountant registrations: $e');
      return [];
    }
  }

  // Duyệt đơn
  Future<Map<String, dynamic>> approveRegistration(int registrationId, {int? classId}) async {
    try {
      String url = 'Registrations/$registrationId/approve';
      if (classId != null) url += '?classId=$classId';

      final response = await _apiService.post(url, {});
      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        return {'success': true, 'message': body['Message'] ?? 'Duyệt đơn thành công'};
      }
      final body = jsonDecode(response.body);
      return {'success': false, 'message': body['Message'] ?? 'Duyệt đơn thất bại'};
    } catch (e) {
      print('Error approving registration: $e');
      return {'success': false, 'message': 'Lỗi kết nối: $e'};
    }
  }

  // Từ chối đơn
  Future<Map<String, dynamic>> rejectRegistration(int registrationId) async {
    try {
      final response = await _apiService.post('Registrations/$registrationId/reject', {});
      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        return {'success': true, 'message': body['Message'] ?? 'Từ chối đơn thành công'};
      }
      final body = jsonDecode(response.body);
      return {'success': false, 'message': body['Message'] ?? 'Từ chối đơn thất bại'};
    } catch (e) {
      print('Error rejecting registration: $e');
      return {'success': false, 'message': 'Lỗi kết nối: $e'};
    }
  }

  // --- CLASS-BASED REGISTRATION ADDITIONS ---
  // Lấy danh sách lớp mở cho một khóa học (theo courseCode)
  Future<List<OpenClassItem>> getOpenClasses(String courseCode) async {
    try {
      final endpoint = 'profile/open-classes/$courseCode';
      print('Fetching open classes for courseCode=$courseCode');
      final response = await _apiService.get(endpoint);
      print('Open classes response status: ${response.statusCode}');
      print('Open classes response body: ${response.body}');

      if (response.statusCode == 200) {
        final dynamic decoded = jsonDecode(response.body);
        // Defensive: backend might return either array or { data: [...] }
        List<dynamic> rawList;
        if (decoded is List) {
          rawList = decoded;
        } else if (decoded is Map<String, dynamic>) {
          rawList = decoded['data'] ?? decoded['Data'] ?? [];
        } else {
          rawList = [];
        }
        return rawList.map((e) => OpenClassItem.fromJson(e)).toList();
      }
      return [];
    } catch (e) {
      print('Error fetching open classes: $e');
      return [];
    }
  }

  // Đăng ký trực tiếp vào một lớp học mở
  Future<Map<String, dynamic>> registerClass(int classId) async {
    try {
      print('Registering classId=$classId');
      final response = await _apiService.post('profile/register-class', {
        'classId': classId,
      });
      print('Register class response status: ${response.statusCode}');
      print('Register class response body: ${response.body}');

      final body = jsonDecode(response.body);
      final success = body['success'] ?? body['Success'] ?? (response.statusCode == 200);
      final message = body['message'] ?? body['Message'] ?? (success ? 'Đăng ký lớp thành công' : 'Đăng ký lớp thất bại');
      return {'success': success, 'message': message};
    } catch (e) {
      print('Error registering class: $e');
      return {'success': false, 'message': 'Lỗi kết nối: $e'};
    }
  }
}
