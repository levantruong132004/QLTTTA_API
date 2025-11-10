import 'dart:convert';
import 'package:qlttta_app_mobile/models/schedule.dart';
import 'package:qlttta_app_mobile/services/api_service.dart';

class ScheduleService {
  final ApiService _apiService = ApiService();

  // Lấy lịch học theo lớp
  Future<List<Schedule>> getByClass(int classId) async {
    try {
      final response = await _apiService.get('Schedules/by-class/$classId');
      if (response.statusCode == 200) {
        final List<dynamic> data = jsonDecode(response.body);
        return data.map((json) => Schedule.fromJson(json)).toList();
      }
      return [];
    } catch (e) {
      print('Error fetching schedules by class: $e');
      return [];
    }
  }
}
