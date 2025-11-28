import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/screens/home_screen.dart';
import 'package:qlttta_app_mobile/screens/register_screen.dart';
import 'package:qlttta_app_mobile/screens/forgot_password_screen.dart';
import 'package:qlttta_app_mobile/services/auth_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';
import 'package:shared_preferences/shared_preferences.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _usernameController = TextEditingController();
  final _passwordController = TextEditingController();
  final AuthService _authService = AuthService();
  bool _isLoading = false;
  // QR-login flow removed from login screen; scanner remains accessible in Home.

  void _login() async {
    setState(() {
      _isLoading = true;
    });

    final success = await _authService.login(
      _usernameController.text,
      _passwordController.text,
    );

    if (success) {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString('username', _usernameController.text);
      if (!mounted) return;
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(builder: (context) => const HomeScreen()),
      );
    } else {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: const Text('Đăng nhập thất bại!'),
          backgroundColor: RetroColors.vintageBurgundy,
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.zero,
            side:
                const BorderSide(color: RetroColors.vintageDarkBrown, width: 2),
          ),
        ),
      );
    }

    setState(() {
      _isLoading = false;
    });
  }

  @override
  void dispose() {
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Container(
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [
              const Color(0xFF8B4513),
              const Color(0xFFD2691E),
              const Color(0xFFF4A460),
            ],
          ),
        ),
        child: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(horizontal: 24.0, vertical: 32.0),
              child: Container(
                constraints: const BoxConstraints(maxWidth: 400),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(24),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withOpacity(0.2),
                      blurRadius: 30,
                      offset: const Offset(0, 15),
                    ),
                  ],
                ),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    // Header
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(32),
                      decoration: BoxDecoration(
                        gradient: LinearGradient(
                          colors: [
                            const Color(0xFF8B4513),
                            const Color(0xFFD2691E),
                          ],
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                        ),
                        borderRadius: const BorderRadius.vertical(top: Radius.circular(24)),
                      ),
                      child: Column(
                        children: [
                          Container(
                            padding: const EdgeInsets.all(16),
                            decoration: BoxDecoration(
                              color: Colors.white.withOpacity(0.2),
                              borderRadius: BorderRadius.circular(20),
                            ),
                            child: const Icon(
                              Icons.school_rounded,
                              size: 56,
                              color: Colors.white,
                            ),
                          ),
                          const SizedBox(height: 16),
                          const Text(
                            'HỆ THỐNG QUẢN LÝ',
                            style: TextStyle(
                              fontSize: 20,
                              fontWeight: FontWeight.bold,
                              color: Colors.white,
                              letterSpacing: 1.5,
                            ),
                            textAlign: TextAlign.center,
                          ),
                          const SizedBox(height: 4),
                          Text(
                            '~ TIẾNG ANH ~',
                            style: TextStyle(
                              fontSize: 14,
                              fontStyle: FontStyle.italic,
                              color: Colors.white.withOpacity(0.9),
                            ),
                            textAlign: TextAlign.center,
                          ),
                        ],
                      ),
                    ),
                    // Form Body
                    Padding(
                      padding: const EdgeInsets.all(28),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          const Text(
                            'ĐĂNG NHẬP',
                            style: TextStyle(
                              fontSize: 18,
                              fontWeight: FontWeight.bold,
                              color: Color(0xFF1a1a1a),
                              letterSpacing: 1.2,
                            ),
                            textAlign: TextAlign.center,
                          ),
                          const SizedBox(height: 28),
                          // Username field
                          Text(
                            'TÊN ĐĂNG NHẬP',
                            style: TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w600,
                              color: Colors.grey.shade700,
                              letterSpacing: 0.5,
                            ),
                          ),
                          const SizedBox(height: 8),
                          Container(
                            decoration: BoxDecoration(
                              color: Colors.grey.shade50,
                              borderRadius: BorderRadius.circular(12),
                              border: Border.all(color: Colors.grey.shade200),
                            ),
                            child: TextField(
                              controller: _usernameController,
                              style: const TextStyle(
                                fontSize: 15,
                                color: Color(0xFF1a1a1a),
                              ),
                              decoration: InputDecoration(
                                hintText: 'Nhập tên đăng nhập...',
                                hintStyle: TextStyle(
                                  fontStyle: FontStyle.italic,
                                  color: Colors.grey.shade400,
                                  fontSize: 14,
                                ),
                                border: InputBorder.none,
                                contentPadding: const EdgeInsets.all(16),
                                prefixIcon: Icon(Icons.person_rounded,
                                    color: const Color(0xFF8B4513), size: 22),
                              ),
                            ),
                          ),
                          const SizedBox(height: 20),
                          // Password field
                          Text(
                            'MẬT KHẨU',
                            style: TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w600,
                              color: Colors.grey.shade700,
                              letterSpacing: 0.5,
                            ),
                          ),
                          const SizedBox(height: 8),
                          Container(
                            decoration: BoxDecoration(
                              color: Colors.grey.shade50,
                              borderRadius: BorderRadius.circular(12),
                              border: Border.all(color: Colors.grey.shade200),
                            ),
                            child: TextField(
                              controller: _passwordController,
                              obscureText: true,
                              style: const TextStyle(
                                fontSize: 15,
                                color: Color(0xFF1a1a1a),
                              ),
                              decoration: InputDecoration(
                                hintText: 'Nhập mật khẩu...',
                                hintStyle: TextStyle(
                                  fontStyle: FontStyle.italic,
                                  color: Colors.grey.shade400,
                                  fontSize: 14,
                                ),
                                border: InputBorder.none,
                                contentPadding: const EdgeInsets.all(16),
                                prefixIcon: Icon(Icons.lock_rounded,
                                    color: const Color(0xFF8B4513), size: 22),
                              ),
                            ),
                          ),
                          const SizedBox(height: 32),
                          // Login button
                          _isLoading
                              ? const Center(
                                  child: CircularProgressIndicator(
                                    color: Color(0xFF8B4513),
                                  ),
                                )
                              : Container(
                                  decoration: BoxDecoration(
                                    borderRadius: BorderRadius.circular(12),
                                    gradient: LinearGradient(
                                      colors: [
                                        const Color(0xFF8B4513),
                                        const Color(0xFFD2691E),
                                      ],
                                    ),
                                    boxShadow: [
                                      BoxShadow(
                                        color: const Color(0xFF8B4513).withOpacity(0.3),
                                        blurRadius: 12,
                                        offset: const Offset(0, 6),
                                      ),
                                    ],
                                  ),
                                  child: ElevatedButton(
                                    onPressed: _login,
                                    style: ElevatedButton.styleFrom(
                                      backgroundColor: Colors.transparent,
                                      foregroundColor: Colors.white,
                                      shadowColor: Colors.transparent,
                                      padding: const EdgeInsets.symmetric(vertical: 16),
                                      shape: RoundedRectangleBorder(
                                        borderRadius: BorderRadius.circular(12),
                                      ),
                                    ),
                                    child: const Text(
                                      'ĐĂNG NHẬP',
                                      style: TextStyle(
                                        fontSize: 16,
                                        fontWeight: FontWeight.bold,
                                        letterSpacing: 1.5,
                                      ),
                                    ),
                                  ),
                                ),
                          const SizedBox(height: 28),
                          // Register link
                          Container(
                            padding: const EdgeInsets.all(20),
                            decoration: BoxDecoration(
                              color: Colors.grey.shade50,
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: Column(
                              children: [
                                Row(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: [
                                    Icon(
                                      Icons.info_outline_rounded,
                                      size: 18,
                                      color: Colors.grey.shade600,
                                    ),
                                    const SizedBox(width: 8),
                                    Text(
                                      'Chưa có tài khoản? ',
                                      style: TextStyle(
                                        color: Colors.grey.shade600,
                                        fontSize: 14,
                                      ),
                                    ),
                                    GestureDetector(
                                      onTap: () {
                                        Navigator.push(
                                          context,
                                          MaterialPageRoute(
                                            builder: (context) =>
                                                const RegisterScreen(),
                                          ),
                                        );
                                      },
                                      child: const Text(
                                        'Đăng ký ngay',
                                        style: TextStyle(
                                          color: Color(0xFF8B4513),
                                          fontSize: 14,
                                          fontWeight: FontWeight.bold,
                                          decoration: TextDecoration.underline,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 12),
                                GestureDetector(
                                  onTap: () {
                                    Navigator.push(
                                      context,
                                      MaterialPageRoute(
                                        builder: (context) =>
                                            const ForgotPasswordScreen(),
                                      ),
                                    );
                                  },
                                  child: Row(
                                    mainAxisAlignment: MainAxisAlignment.center,
                                    children: [
                                      Icon(
                                        Icons.lock_reset_rounded,
                                        size: 18,
                                        color: const Color(0xFFD2691E),
                                      ),
                                      const SizedBox(width: 8),
                                      const Text(
                                        'Quên mật khẩu?',
                                        style: TextStyle(
                                          color: Color(0xFFD2691E),
                                          fontSize: 14,
                                          fontWeight: FontWeight.bold,
                                          decoration: TextDecoration.underline,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
