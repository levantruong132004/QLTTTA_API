import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class ApiService {
  // For Android emulator. Replace with your actual API URL when needed
  static const String _baseUrl = 'http://10.0.2.2:7158/api';

  Future<Map<String, String>> _headers() async {
    final prefs = await SharedPreferences.getInstance();
    final sid = prefs.getString('sessionId');
    final headers = <String, String>{
      'Content-Type': 'application/json',
      'X-Device-Type': 'mobile',
    };
    if (sid != null && sid.isNotEmpty) {
      headers['X-Session-Id'] = sid;
    }
    return headers;
  }

  Future<http.Response> get(String endpoint) async {
    final res = await http.get(
      Uri.parse('$_baseUrl/$endpoint'),
      headers: await _headers(),
    );
    return res;
  }

  Future<http.Response> post(String endpoint, dynamic data) async {
    final res = await http.post(
      Uri.parse('$_baseUrl/$endpoint'),
      headers: await _headers(),
      body: data,
    );
    return res;
  }
}
