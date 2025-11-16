import 'dart:convert';
import 'package:qlttta_app_mobile/models/student.dart';
import 'package:qlttta_app_mobile/services/api_service.dart';

class StudentService {
  final ApiService _apiService = ApiService();

  /// Kết quả phân trang học viên.
  Future<(List<Student> students, int totalRecords)> getStudents({int pageNumber = 1, int pageSize = 50, String? search}) async {
    final searchQuery = (search != null && search.trim().isNotEmpty)
        ? '&search=${Uri.encodeQueryComponent(search)}'
        : '';
    final response = await _apiService.get('Students?pageNumber=$pageNumber&pageSize=$pageSize$searchQuery');
    if (response.statusCode != 200) {
      throw Exception('Failed to load students: HTTP ${response.statusCode}');
    }
    final body = jsonDecode(response.body);
    List<dynamic> rawList = [];
    int total = 0;
    if (body is Map) {
      final dataField = body['data'] ?? body['Data'];
      if (dataField is List) rawList = dataField;
      final totalRecords = body['TotalRecords'] ?? body['totalRecords'];
      if (totalRecords is int) total = totalRecords;
      if (totalRecords is String) total = int.tryParse(totalRecords) ?? 0;
    } else if (body is List) {
      rawList = body;
      total = rawList.length;
    }
    final students = rawList.map((e) => Student.fromJson(e)).toList();
    return (students, total);
  }

  /// Dành cho Nhân viên học vụ/Admin: lấy toàn bộ theo endpoint staff để đồng bộ với web.
  Future<List<Student>> getStaffStudents({String? search}) async {
    final q = (search != null && search.trim().isNotEmpty) ? '?search=${Uri.encodeQueryComponent(search)}' : '';
    final response = await _apiService.get('staff/students$q');
    if (response.statusCode != 200) {
      throw Exception('Failed to load staff students: HTTP ${response.statusCode}');
    }
    final body = jsonDecode(response.body);
    final data = body is Map ? (body['data'] ?? body['Data']) : null;
    final list = (data is List) ? data : (body is List ? body : []);
    return list
        .whereType<Map>()
        .map<Student>((e) => _mapStaffStudent(Map<String, dynamic>.from(e)))
        .toList();
  }

  Future<Student?> getStaffStudentProfile(int studentId) async {
    try {
      final response = await _apiService.get('staff/students/$studentId/detail');
      if (response.statusCode != 200) return null;
      final body = jsonDecode(response.body);
      final data = body is Map ? (body['data'] ?? body['Data']) : null;
      if (data is Map) {
        final studentJson = data['student'] ?? data['Student'];
        if (studentJson is Map) {
          final mapped = _mapStaffStudent(Map<String, dynamic>.from(studentJson));
          return mapped;
        }
      }
    } catch (_) {}
    return null;
  }

  Student _mapStaffStudent(Map<String, dynamic> json) {
    int? id;
    final rawId = json['studentId'] ?? json['StudentId'];
    if (rawId is int) {
      id = rawId;
    } else if (rawId is String) {
      id = int.tryParse(rawId);
    }
    return Student(
      studentId: id ?? 0,
      maHocVien: (json['studentCode'] ?? json['StudentCode'] ?? '').toString(),
      hoTen: (json['fullName'] ?? json['FullName'] ?? '').toString(),
      soDienThoai: (json['phoneNumber'] ?? json['PhoneNumber'])?.toString(),
      ngaySinh: null,
      diaChi: (json['address'] ?? json['Address'])?.toString(),
    );
  }

  /// Lấy tổng số học viên nhanh qua endpoint count (ưu tiên), fallback lấy trang nhỏ.
  Future<int> getStudentsCount() async {
    try {
      final response = await _apiService.get('Students/count');
      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        final success = body['success'] ?? body['Success'];
        if (success == false) {
          // fallback if API reports error in envelope
        } else {
          final count = body['Count'] ?? body['count'];
          if (count is int) return count;
          if (count is String) return int.tryParse(count) ?? 0;
        }
      }
    } catch (_) {}
    // Fallback: gọi trang đầu pageSize=1 để đọc TotalRecords
    try {
      final response = await _apiService.get('Students?pageNumber=1&pageSize=1');
      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        if (body is Map) {
          final totalRecords = body['TotalRecords'] ?? body['totalRecords'];
          if (totalRecords is int) return totalRecords;
          if (totalRecords is String) return int.tryParse(totalRecords) ?? 0;
        }
      }
    } catch (_) {}
    return 0;
  }

  Future<Student?> getStudentProfile(int studentId) async {
    final student = await _tryGetStudentProfile(studentId);
    if (student != null) return student;
    return await getStaffStudentProfile(studentId);
  }

  Future<Student?> _tryGetStudentProfile(int studentId) async {
    try {
      final response = await _apiService.get('Students/$studentId');
      if (response.statusCode != 200) return null;
      final body = jsonDecode(response.body);
      final data = body['data'] ?? body['Data'];
      if (data is Map<String, dynamic>) return Student.fromJson(data);
    } catch (_) {}
    return null;
  }
}
