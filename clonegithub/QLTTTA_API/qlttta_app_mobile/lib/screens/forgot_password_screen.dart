import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/screens/forgot_verify_screen.dart';
import 'package:qlttta_app_mobile/screens/register_screen.dart';
import 'package:qlttta_app_mobile/services/auth_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';

class ForgotPasswordScreen extends StatefulWidget {
  const ForgotPasswordScreen({super.key});

  @override
  State<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends State<ForgotPasswordScreen> {
  final _usernameController = TextEditingController();
  final _emailController = TextEditingController();
  final AuthService _authService = AuthService();
  bool _isLoading = false;

  void _sendOtp() async {
    if (_usernameController.text.trim().isEmpty ||
        _emailController.text.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Vui lòng nhập đầy đủ thông tin'),
          backgroundColor: RetroColors.error,
        ),
      );
      return;
    }

    setState(() {
      _isLoading = true;
    });

    final result = await _authService.initiateForgotPassword(
      username: _usernameController.text.trim(),
      email: _emailController.text.trim(),
    );

    setState(() {
      _isLoading = false;
    });

    if (!mounted) return;

    if (result['success'] == true) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(result['message'] ?? 'Đã gửi OTP'),
          backgroundColor: RetroColors.success,
        ),
      );
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(
          builder: (context) => ForgotVerifyScreen(
            correlationId: result['correlationId'],
          ),
        ),
      );
    } else {
      if (result['shouldRegister'] == true) {
        showDialog(
          context: context,
          builder: (ctx) => AlertDialog(
            title: const Text('Tài khoản không tồn tại'),
            content:
                const Text('Tài khoản chưa được đăng ký. Bạn có muốn đăng ký mới không?'),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(ctx),
                child: const Text('Hủy'),
              ),
              ElevatedButton(
                onPressed: () {
                  Navigator.pop(ctx);
                  Navigator.of(context).pushReplacement(
                    MaterialPageRoute(
                      builder: (context) => const RegisterScreen(),
                    ),
                  );
                },
                child: const Text('Đăng ký'),
              ),
            ],
          ),
        );
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(result['message'] ?? 'Có lỗi xảy ra'),
            backgroundColor: RetroColors.error,
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: RetroColors.background,
      appBar: AppBar(
        title: const Text('QUÊN MẬT KHẨU'),
        backgroundColor: RetroColors.primary,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const SizedBox(height: 20),
            Icon(
              Icons.lock_reset_rounded,
              size: 80,
              color: RetroColors.primary,
            ),
            const SizedBox(height: 16),
            const Text(
              'Đặt lại mật khẩu',
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 24,
                fontWeight: FontWeight.bold,
                color: RetroColors.textPrimary,
              ),
            ),
            const SizedBox(height: 8),
            const Text(
              'Nhập tên đăng nhập và email của bạn.\nChúng tôi sẽ gửi mã OTP để xác thực.',
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 14,
                color: RetroColors.textSecondary,
              ),
            ),
            const SizedBox(height: 32),
            // Username
            Container(
              decoration: BoxDecoration(
                color: RetroColors.surface,
                borderRadius: BorderRadius.circular(12),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.05),
                    blurRadius: 4,
                    offset: const Offset(0, 2),
                  ),
                ],
              ),
              child: TextField(
                controller: _usernameController,
                decoration: InputDecoration(
                  labelText: 'Tên đăng nhập',
                  prefixIcon:
                      const Icon(Icons.person, color: RetroColors.primary),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(12),
                    borderSide: BorderSide.none,
                  ),
                  filled: true,
                  fillColor: Colors.transparent,
                ),
              ),
            ),
            const SizedBox(height: 16),
            // Email
            Container(
              decoration: BoxDecoration(
                color: RetroColors.surface,
                borderRadius: BorderRadius.circular(12),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.05),
                    blurRadius: 4,
                    offset: const Offset(0, 2),
                  ),
                ],
              ),
              child: TextField(
                controller: _emailController,
                keyboardType: TextInputType.emailAddress,
                decoration: InputDecoration(
                  labelText: 'Email',
                  prefixIcon:
                      const Icon(Icons.email, color: RetroColors.primary),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(12),
                    borderSide: BorderSide.none,
                  ),
                  filled: true,
                  fillColor: Colors.transparent,
                ),
              ),
            ),
            const SizedBox(height: 32),
            // Send OTP button
            _isLoading
                ? const Center(
                    child: CircularProgressIndicator(
                      color: RetroColors.primary,
                    ),
                  )
                : ElevatedButton(
                    onPressed: _sendOtp,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: RetroColors.primary,
                      foregroundColor: RetroColors.textLight,
                      padding: const EdgeInsets.symmetric(vertical: 16),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                      elevation: 2,
                    ),
                    child: const Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.send_rounded, size: 20),
                        SizedBox(width: 8),
                        Text(
                          'GỬI MÃ OTP',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                            letterSpacing: 1,
                          ),
                        ),
                      ],
                    ),
                  ),
            const SizedBox(height: 24),
            // Back to login
            Center(
              child: TextButton.icon(
                onPressed: () => Navigator.pop(context),
                icon: const Icon(Icons.arrow_back, size: 18),
                label: const Text('Quay lại đăng nhập'),
                style: TextButton.styleFrom(
                  foregroundColor: RetroColors.primary,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  @override
  void dispose() {
    _usernameController.dispose();
    _emailController.dispose();
    super.dispose();
  }
}
