import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/screens/home_screen.dart';
import 'package:qlttta_app_mobile/screens/register_screen.dart';
import 'package:qlttta_app_mobile/services/auth_service.dart';
import 'package:qlttta_app_mobile/screens/forgot_password_screen.dart';
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

  void _login() async {
    final username = _usernameController.text.trim();
    final password = _passwordController.text;
    if (username.isEmpty || password.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: const Text('Vui lòng nhập đủ tên đăng nhập và mật khẩu'),
          backgroundColor: RetroColors.vintageBurgundy,
        ),
      );
      return;
    }

    setState(() => _isLoading = true);
    try {
      final success = await _authService.login(username, password);
      if (success) {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString('username', username);
        if (!mounted) return;
        Navigator.of(context).pushReplacement(
          MaterialPageRoute(builder: (context) => const HomeScreen()),
        );
      } else {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: const Text('Đăng nhập thất bại. Kiểm tra lại thông tin.'),
            backgroundColor: RetroColors.vintageBurgundy,
            behavior: SnackBarBehavior.floating,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.zero,
              side: const BorderSide(color: RetroColors.vintageDarkBrown, width: 2),
            ),
          ),
        );
      }
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Không thể kết nối máy chủ. Hãy kiểm tra API (10.0.2.2:7158) và mạng.\nChi tiết: $e'),
          backgroundColor: RetroColors.vintageBurgundy,
          behavior: SnackBarBehavior.floating,
        ),
      );
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: RetroColors.vintageCream,
      body: Container(
        decoration: BoxDecoration(
          image: DecorationImage(
            image: NetworkImage('data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNDAiIGhlaWdodD0iNDAiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+PGRlZnM+PHBhdHRlcm4gaWQ9InBhdHRlcm4iIHBhdHRlcm5Vbml0cz0idXNlclNwYWNlT25Vc2UiIHdpZHRoPSI0MCIgaGVpZ2h0PSI0MCI+PHJlY3Qgd2lkdGg9IjQwIiBoZWlnaHQ9IjQwIiBmaWxsPSIjZjRlYWQ1Ii8+PHBhdGggZD0iTTAgMGgyMHYyMEgweiIgZmlsbD0iI2U4ZGNjNCIgb3BhY2l0eT0iMC4zIi8+PHBhdGggZD0iTTIwIDIwaDIwdjIwSDIweiIgZmlsbD0iI2U4ZGNjNCIgb3BhY2l0eT0iMC4zIi8+PC9wYXR0ZXJuPjwvZGVmcz48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSJ1cmwoI3BhdHRlcm4pIi8+PC9zdmc+'),
            repeat: ImageRepeat.repeat,
            opacity: 0.3,
          ),
        ),
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24.0),
            child: Container(
              constraints: const BoxConstraints(maxWidth: 400),
              decoration: BoxDecoration(
                color: RetroColors.vintageWhite,
                border: Border.all(color: RetroColors.vintageBrown, width: 4),
                boxShadow: [
                  BoxShadow(
                    color: RetroColors.vintageDarkBrown.withOpacity(0.3),
                    offset: const Offset(8, 8),
                    blurRadius: 0,
                  ),
                ],
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  // Header
                  Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(24),
                    decoration: BoxDecoration(
                      gradient: LinearGradient(
                        colors: [RetroColors.vintageBrown, RetroColors.vintageDarkBrown],
                        begin: Alignment.topCenter,
                        end: Alignment.bottomCenter,
                      ),
                      border: Border(
                        bottom: BorderSide(color: RetroColors.vintageGold, width: 3),
                      ),
                    ),
                    child: Column(
                      children: [
                        const Icon(
                          Icons.school,
                          size: 60,
                          color: RetroColors.vintageCream,
                        ),
                        const SizedBox(height: 12),
                        const Text(
                          'HỆ THỐNG QUẢN LÝ',
                          style: TextStyle(
                            fontSize: 20,
                            fontWeight: FontWeight.bold,
                            color: RetroColors.vintageCream,
                            letterSpacing: 2,
                          ),
                          textAlign: TextAlign.center,
                        ),
                        const SizedBox(height: 4),
                        const Text(
                          '~ TIẾNG ANH ~',
                          style: TextStyle(
                            fontSize: 16,
                            fontStyle: FontStyle.italic,
                            color: RetroColors.vintageGold,
                          ),
                          textAlign: TextAlign.center,
                        ),
                      ],
                    ),
                  ),
                  // Form Body
                  Container(
                    padding: const EdgeInsets.all(24),
                    decoration: const BoxDecoration(
                      color: RetroColors.vintageOffWhite,
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        const Text(
                          'ĐĂNG NHẬP',
                          style: TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.bold,
                            color: RetroColors.vintageDarkBrown,
                            letterSpacing: 2,
                          ),
                          textAlign: TextAlign.center,
                        ),
                        const SizedBox(height: 24),
                        // Username field
                        const Text(
                          'TÊN ĐĂNG NHẬP',
                          style: TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                            color: RetroColors.vintageDarkBrown,
                            letterSpacing: 1,
                          ),
                        ),
                        const SizedBox(height: 8),
                        Container(
                          decoration: BoxDecoration(
                            border: Border.all(color: RetroColors.vintageBrown, width: 2),
                            color: RetroColors.vintageWhite,
                          ),
                          child: TextField(
                            controller: _usernameController,
                            style: const TextStyle(
                              fontSize: 16,
                              color: RetroColors.vintageDarkBrown,
                            ),
                            decoration: const InputDecoration(
                              hintText: 'Nhập tên đăng nhập...',
                              hintStyle: TextStyle(
                                fontStyle: FontStyle.italic,
                                color: RetroColors.vintageGray,
                              ),
                              border: InputBorder.none,
                              contentPadding: EdgeInsets.all(12),
                              prefixIcon: Icon(Icons.person, color: RetroColors.vintageBrown),
                            ),
                          ),
                        ),
                        const SizedBox(height: 20),
                        // Password field
                        const Text(
                          'MẬT KHẨU',
                          style: TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                            color: RetroColors.vintageDarkBrown,
                            letterSpacing: 1,
                          ),
                        ),
                        const SizedBox(height: 8),
                        Container(
                          decoration: BoxDecoration(
                            border: Border.all(color: RetroColors.vintageBrown, width: 2),
                            color: RetroColors.vintageWhite,
                          ),
                          child: TextField(
                            controller: _passwordController,
                            obscureText: true,
                            style: const TextStyle(
                              fontSize: 16,
                              color: RetroColors.vintageDarkBrown,
                            ),
                            decoration: const InputDecoration(
                              hintText: 'Nhập mật khẩu...',
                              hintStyle: TextStyle(
                                fontStyle: FontStyle.italic,
                                color: RetroColors.vintageGray,
                              ),
                              border: InputBorder.none,
                              contentPadding: EdgeInsets.all(12),
                              prefixIcon: Icon(Icons.lock, color: RetroColors.vintageBrown),
                            ),
                          ),
                        ),
                        const SizedBox(height: 32),
                        // Forgot password link
                        Align(
                          alignment: Alignment.centerRight,
                          child: TextButton(
                            onPressed: () {
                              Navigator.push(
                                context,
                                MaterialPageRoute(
                                  builder: (context) => const ForgotPasswordScreen(),
                                ),
                              );
                            },
                            child: const Text(
                              'Quên mật khẩu?',
                              style: TextStyle(
                                color: RetroColors.vintageRust,
                                fontWeight: FontWeight.bold,
                                decoration: TextDecoration.underline,
                              ),
                            ),
                          ),
                        ),
                        const SizedBox(height: 8),
                        // Login button
                        _isLoading
                            ? const Center(
                                child: CircularProgressIndicator(
                                  color: RetroColors.vintageBrown,
                                ),
                              )
                            : Container(
                                decoration: BoxDecoration(
                                  boxShadow: [
                                    BoxShadow(
                                      color: RetroColors.vintageDarkBrown.withOpacity(1),
                                      offset: const Offset(4, 4),
                                      blurRadius: 0,
                                    ),
                                  ],
                                ),
                                child: ElevatedButton(
                                  onPressed: _login,
                                  style: ElevatedButton.styleFrom(
                                    backgroundColor: RetroColors.vintageBrown,
                                    foregroundColor: RetroColors.vintageCream,
                                    padding: const EdgeInsets.symmetric(vertical: 16),
                                    shape: RoundedRectangleBorder(
                                      borderRadius: BorderRadius.zero,
                                      side: const BorderSide(
                                        color: RetroColors.vintageDarkBrown,
                                        width: 3,
                                      ),
                                    ),
                                  ),
                                  child: const Text(
                                    'ĐĂNG NHẬP',
                                    style: TextStyle(
                                      fontSize: 16,
                                      fontWeight: FontWeight.bold,
                                      letterSpacing: 2,
                                    ),
                                  ),
                                ),
                              ),
                        const SizedBox(height: 24),
                        // Register link
                        Container(
                          padding: const EdgeInsets.all(16),
                          decoration: BoxDecoration(
                            border: Border.all(
                              color: RetroColors.vintageBrown.withOpacity(0.3),
                              width: 1,
                            ),
                          ),
                          child: Row(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              const Icon(
                                Icons.info_outline,
                                size: 18,
                                color: RetroColors.vintageGray,
                              ),
                              const SizedBox(width: 8),
                              const Text(
                                'Chưa có tài khoản? ',
                                style: TextStyle(
                                  color: RetroColors.vintageGray,
                                  fontSize: 14,
                                ),
                              ),
                              GestureDetector(
                                onTap: () {
                                  Navigator.push(
                                    context,
                                    MaterialPageRoute(
                                      builder: (context) => const RegisterScreen(),
                                    ),
                                  );
                                },
                                child: const Text(
                                  'Đăng ký ngay',
                                  style: TextStyle(
                                    color: RetroColors.vintageRust,
                                    fontSize: 14,
                                    fontWeight: FontWeight.bold,
                                    decoration: TextDecoration.underline,
                                  ),
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
    );
  }
}
