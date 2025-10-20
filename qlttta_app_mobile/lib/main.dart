import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/screens/login_screen.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';

void main() {
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'QLTTTA',
      theme: RetroTheme.theme,
      home: const LoginScreen(),
    );
  }
}
