import 'package:http/http.dart' as http;
import 'dart:async';
import 'dart:io' show HttpHeaders;

class ApiService {
  // For Android emulator use 10.0.2.2. If running on device, point to LAN IP. For iOS simulator, use localhost.
  static const String _baseUrl = 'http://10.0.2.2:7158/api';

  static final Map<String, String> _defaultHeaders = {
    HttpHeaders.contentTypeHeader: 'application/json',
  };

  static String? _sessionId;
  static String? _bearerToken;

  static void setAuth({String? sessionId, String? token}) {
    _sessionId = sessionId;
    _bearerToken = token;
  }

  static void clearAuth() {
    _sessionId = null;
    _bearerToken = null;
  }

  Map<String, String> _buildHeaders([Map<String, String>? extra]) {
    final headers = {..._defaultHeaders};
    if (_sessionId != null && _sessionId!.isNotEmpty) {
      headers['X-Session-Id'] = _sessionId!;
    }
    if (_bearerToken != null && _bearerToken!.isNotEmpty) {
      headers[HttpHeaders.authorizationHeader] = 'Bearer $_bearerToken';
    }
    if (extra != null) headers.addAll(extra);
    return headers;
  }

  Future<http.Response> get(String endpoint) {
    return http
        .get(
      Uri.parse('$_baseUrl/$endpoint'),
      headers: _buildHeaders(),
    )
        .timeout(const Duration(seconds: 12));
  }

  Future<http.Response> post(String endpoint, dynamic data) {
    return http
        .post(
      Uri.parse('$_baseUrl/$endpoint'),
      headers: _buildHeaders(),
      body: data,
    )
        .timeout(const Duration(seconds: 12));
  }
}
