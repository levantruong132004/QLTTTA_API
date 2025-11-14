import 'dart:convert';
import 'package:qlttta_app_mobile/services/api_service.dart';

class ProfileService {
  final ApiService _api = ApiService();
  int? _cachedStudentId;

  Future<int?> getCurrentStudentId() async {
    if (_cachedStudentId != null) return _cachedStudentId;
    try {
      final res = await _api.get('profile');
      if (res.statusCode == 200) {
        final data = jsonDecode(res.body);
        // Try common casings
        final id = data['studentId'] ?? data['StudentId'] ?? data['id'] ?? data['Id'];
        if (id is int) {
          _cachedStudentId = id;
          return id;
        }
        if (id is String) {
          final parsed = int.tryParse(id);
          if (parsed != null) {
            _cachedStudentId = parsed;
            return parsed;
          }
        }
      }
    } catch (_) {}
    return null;
  }
}
