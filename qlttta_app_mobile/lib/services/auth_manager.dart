import 'dart:async';

class AuthEvent {
  final String type; // e.g., 'unauthorized'
  final String message;
  AuthEvent(this.type, this.message);
}

class AuthManager {
  static final AuthManager _instance = AuthManager._internal();
  factory AuthManager() => _instance;
  AuthManager._internal();

  final _controller = StreamController<AuthEvent>.broadcast();

  Stream<AuthEvent> get stream => _controller.stream;

  void notifyUnauthorized([
    String message = 'Phiên đăng nhập không còn hợp lệ',
  ]) {
    _controller.add(AuthEvent('unauthorized', message));
  }
}
