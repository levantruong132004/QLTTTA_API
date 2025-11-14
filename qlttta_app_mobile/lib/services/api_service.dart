import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class ApiService {
  // Read API base URL from --dart-define when provided; fallback to emulator host
  static const String _baseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:7158/api',
  );

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
      body: jsonEncode(data), // Encode to JSON string
    );
    return res;
  }
}
