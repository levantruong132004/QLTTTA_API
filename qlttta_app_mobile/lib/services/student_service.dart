import 'dart:convert';
import 'package:qlttta_app_mobile/models/student.dart';
import 'package:qlttta_app_mobile/services/api_service.dart';

class StudentService {
  final ApiService _apiService = ApiService();

  Future<List<Student>> getStudents() async {
    final response = await _apiService.get('Students');

    if (response.statusCode == 200) {
      Map<String, dynamic> body = jsonDecode(response.body);
      List<dynamic> studentData = body['data'];
      List<Student> students = studentData.map((dynamic item) => Student.fromJson(item)).toList();
      return students;
    } else {
      throw Exception('Failed to load students');
    }
  }

  Future<Student?> getStudentProfile(int studentId) async {
    final response = await _apiService.get('Students/$studentId');

    if (response.statusCode == 200) {
      Map<String, dynamic> body = jsonDecode(response.body);
      return Student.fromJson(body['data']);
    } else {
      // Handle error
      return null;
    }
  }
}
