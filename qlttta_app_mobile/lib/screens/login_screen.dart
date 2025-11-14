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
      backgroundColor: RetroColors.vintageCream,
      body: Container(
        // Removed SVG data-URI background that caused runtime errors on Android.
        // If a patterned background is desired, consider using an asset image
        // with AssetImage and declare it in pubspec.yaml, or use flutter_svg
        // to render an SVG widget behind the content.
        decoration: const BoxDecoration(
          color: RetroColors.vintageCream,
        ),
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 20.0, vertical: 32.0),
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
                        colors: [
                          RetroColors.vintageBrown,
                          RetroColors.vintageDarkBrown
                        ],
                        begin: Alignment.topCenter,
                        end: Alignment.bottomCenter,
                      ),
                      border: const Border(
                        bottom: BorderSide(
                            color: RetroColors.vintageGold, width: 3),
                      ),
                    ),
                    child: const Column(
                      children: [
                        Icon(
                          Icons.school,
                          size: 48,
                          color: RetroColors.vintageCream,
                        ),
                        SizedBox(height: 12),
                        Text(
                          'HỆ THỐNG QUẢN LÝ',
                          style: TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.bold,
                            color: RetroColors.vintageCream,
                            letterSpacing: 2,
                          ),
                          textAlign: TextAlign.center,
                        ),
                        SizedBox(height: 4),
                        Text(
                          '~ TIẾNG ANH ~',
                          style: TextStyle(
                            fontSize: 14,
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
                    padding: const EdgeInsets.all(20),
                    decoration: const BoxDecoration(
                      color: RetroColors.vintageOffWhite,
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        const Text(
                          'ĐĂNG NHẬP',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                            color: RetroColors.vintageDarkBrown,
                            letterSpacing: 2,
                          ),
                          textAlign: TextAlign.center,
                        ),
                        const SizedBox(height: 20),
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
                            border: Border.all(
                                color: RetroColors.vintageBrown, width: 2),
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
                              prefixIcon: Icon(Icons.person,
                                  color: RetroColors.vintageBrown),
                            ),
                          ),
                        ),
                        const SizedBox(height: 16),
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
                            border: Border.all(
                                color: RetroColors.vintageBrown, width: 2),
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
                              prefixIcon: Icon(Icons.lock,
                                  color: RetroColors.vintageBrown),
                            ),
                          ),
                        ),
                        const SizedBox(height: 32),
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
                                      color: RetroColors.vintageDarkBrown
                                          .withOpacity(1),
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
                                    padding: const EdgeInsets.symmetric(
                                        vertical: 16),
                                    shape: const RoundedRectangleBorder(
                                      borderRadius: BorderRadius.zero,
                                      side: BorderSide(
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
                        // Removed QR-login helper button from login screen to keep UI minimal.
                        const SizedBox(height: 16),
                        // Register link
                        Container(
                          padding: const EdgeInsets.all(16),
                          decoration: BoxDecoration(
                            border: Border.all(
                              color: RetroColors.vintageBrown.withOpacity(0.3),
                              width: 1,
                            ),
                          ),
                          child: Column(
                            children: [
                              Row(
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
                                          builder: (context) =>
                                              const RegisterScreen(),
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
                                  children: const [
                                    Icon(
                                      Icons.lock_reset_rounded,
                                      size: 18,
                                      color: RetroColors.vintageGold,
                                    ),
                                    SizedBox(width: 8),
                                    Text(
                                      'Quên mật khẩu?',
                                      style: TextStyle(
                                        color: RetroColors.vintageGold,
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
    );
  }
}
