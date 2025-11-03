import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/screens/login_screen.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';
import 'package:qlttta_app_mobile/services/auth_manager.dart';
import 'package:qlttta_app_mobile/services/session_monitor.dart';
import 'package:qlttta_app_mobile/services/auth_service.dart';

final GlobalKey<NavigatorState> _navKey = GlobalKey<NavigatorState>();

void main() {
  runApp(const MyApp());
}

class MyApp extends StatefulWidget {
  const MyApp({super.key});

  @override
  State<MyApp> createState() => _MyAppState();
}

class _MyAppState extends State<MyApp> {
  late final Stream<AuthEvent> _authStream;

  @override
  void initState() {
    super.initState();
    // Start session monitor (poll every 5s)
    SessionMonitor().start();
    // Listen for unauthorized events -> navigate to Login and clear local session
    _authStream = AuthManager().stream;
    _authStream.listen((evt) async {
      if (evt.type == 'unauthorized') {
        try {
          await AuthService().logout(notifyServer: false);
        } catch (_) {}

        final ctx = _navKey.currentContext;
        if (ctx != null) {
          ScaffoldMessenger.of(ctx).showSnackBar(
            SnackBar(
              content: Text(evt.message),
              duration: const Duration(seconds: 3),
            ),
          );
          Navigator.of(ctx).pushAndRemoveUntil(
            MaterialPageRoute(builder: (_) => const LoginScreen()),
            (route) => false,
          );
        }
      }
    });
  }

  @override
  void dispose() {
    SessionMonitor().stop();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'QLTTTA',
      theme: RetroTheme.theme,
      navigatorKey: _navKey,
      home: const LoginScreen(),
    );
  }
}
