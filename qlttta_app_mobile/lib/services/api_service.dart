import 'package:http/http.dart' as http;

class ApiService {
  static const String _baseUrl = 'http://10.0.2.2:7158/api'; // For Android emulator. Replace with your actual API URL.

  Future<http.Response> get(String endpoint) {
    return http.get(
      Uri.parse('$_baseUrl/$endpoint'),
      headers: {'Content-Type': 'application/json'},
    );
  }

  Future<http.Response> post(String endpoint, dynamic data) {
    return http.post(
      Uri.parse('$_baseUrl/$endpoint'),
      headers: {'Content-Type': 'application/json'},
      body: data,
    );
  }
}
