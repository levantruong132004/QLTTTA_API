import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/screens/login_screen.dart';
import 'package:qlttta_app_mobile/services/auth_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';

class RegisterVerifyScreen extends StatefulWidget {
  final String correlationId;
  final String email;
  const RegisterVerifyScreen({super.key, required this.correlationId, required this.email});

  @override
  State<RegisterVerifyScreen> createState() => _RegisterVerifyScreenState();
}

class _RegisterVerifyScreenState extends State<RegisterVerifyScreen> {
  final _otpController = TextEditingController();
  final AuthService _authService = AuthService();
  bool _isLoading = false;

  Future<void> _verify() async {
    if (_otpController.text.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Vui lòng nhập mã OTP'), backgroundColor: RetroColors.error),
      );
      return;
    }
    setState(() { _isLoading = true; });
    final res = await _authService.verifyRegisterOtp(
      correlationId: widget.correlationId,
      otp: _otpController.text.trim(),
    );
    setState(() { _isLoading = false; });
    if (!mounted) return;
    if (res['success'] == true) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(res['message'] ?? 'Đăng ký thành công'), backgroundColor: RetroColors.success),
      );
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const LoginScreen()), (route) => false,
      );
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(res['message'] ?? 'Xác thực OTP thất bại'), backgroundColor: RetroColors.error),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: RetroColors.background,
      appBar: AppBar(
        title: const Text('XÁC THỰC OTP ĐĂNG KÝ'),
        backgroundColor: RetroColors.primary,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const SizedBox(height: 20),
            const Icon(Icons.mark_email_read, size: 80, color: RetroColors.primary),
            const SizedBox(height: 12),
            Text(
              'Nhập mã OTP gửi tới ${"${"" + widget.email}"}',
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 16, color: RetroColors.textSecondary),
            ),
            const SizedBox(height: 24),
            Container(
              decoration: BoxDecoration(
                color: RetroColors.surface,
                borderRadius: BorderRadius.circular(12),
                boxShadow: [BoxShadow(color: Colors.black12, blurRadius: 4, offset: Offset(0,2))],
              ),
              child: TextField(
                controller: _otpController,
                keyboardType: TextInputType.number,
                maxLength: 6,
                decoration: InputDecoration(
                  labelText: 'Mã OTP',
                  prefixIcon: const Icon(Icons.pin, color: RetroColors.primary),
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide.none),
                  filled: true, fillColor: Colors.transparent, counterText: ''
                ),
              ),
            ),
            const SizedBox(height: 24),
            _isLoading
                ? const Center(child: CircularProgressIndicator(color: RetroColors.primary))
                : ElevatedButton(
                    onPressed: _verify,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: RetroColors.success,
                      foregroundColor: RetroColors.textLight,
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                    ),
                    child: const Text('XÁC THỰC & HOÀN TẤT'),
                  ),
          ],
        ),
      ),
    );
  }

  @override
  void dispose() {
    _otpController.dispose();
    super.dispose();
  }
}
