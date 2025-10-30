import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';

class ForgotPasswordScreen extends StatefulWidget {
  const ForgotPasswordScreen({super.key});

  @override
  State<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends State<ForgotPasswordScreen> {
  final _formKey = GlobalKey<FormState>();
  final _usernameController = TextEditingController();
  final _emailController = TextEditingController();
  bool _submitting = false;

  @override
  void dispose() {
    _usernameController.dispose();
    _emailController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _submitting = true);
    await Future.delayed(const Duration(milliseconds: 600));

    if (!mounted) return;
    setState(() => _submitting = false);

    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: RetroColors.vintageWhite,
        shape: const RoundedRectangleBorder(
          side: BorderSide(color: RetroColors.vintageBrown, width: 3),
        ),
        title: Row(
          children: const [
            Icon(Icons.info_outline, color: RetroColors.vintageBrown),
            SizedBox(width: 8),
            Text(
              'QUÊN MẬT KHẨU',
              style: TextStyle(fontWeight: FontWeight.bold, color: RetroColors.vintageDarkBrown),
            )
          ],
        ),
        content: const Text(
          'Chức năng đặt lại mật khẩu hiện chưa khả dụng trên hệ thống API.\n\nVui lòng liên hệ trung tâm để được hỗ trợ, hoặc thử lại sau khi backend cập nhật tính năng này.',
          style: TextStyle(color: RetroColors.vintageDarkBrown),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            style: TextButton.styleFrom(
              backgroundColor: RetroColors.vintageBrown,
              foregroundColor: RetroColors.vintageCream,
            ),
            child: const Text('ĐÓNG'),
          )
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: RetroColors.vintageCream,
      appBar: AppBar(
        title: const Text('🔒 QUÊN MẬT KHẨU'),
      ),
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
            padding: const EdgeInsets.all(16),
            child: Container(
              constraints: const BoxConstraints(maxWidth: 480),
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
              child: Form(
                key: _formKey,
                child: Padding(
                  padding: const EdgeInsets.all(20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      const Text(
                        'NHẬP THÔNG TIN TÀI KHOẢN',
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                          color: RetroColors.vintageDarkBrown,
                          letterSpacing: 1.5,
                        ),
                      ),
                      const SizedBox(height: 16),
                      TextFormField(
                        controller: _usernameController,
                        decoration: const InputDecoration(
                          labelText: 'Tên đăng nhập',
                          prefixIcon: Icon(Icons.person),
                        ),
                        validator: (v) {
                          if ((v ?? '').isEmpty && _emailController.text.isEmpty) {
                            return 'Nhập tên đăng nhập hoặc email';
                          }
                          return null;
                        },
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _emailController,
                        keyboardType: TextInputType.emailAddress,
                        decoration: const InputDecoration(
                          labelText: 'Email',
                          prefixIcon: Icon(Icons.email),
                        ),
                        validator: (v) {
                          final val = v ?? '';
                          if (val.isEmpty && _usernameController.text.isEmpty) {
                            return 'Nhập email hoặc tên đăng nhập';
                          }
                          if (val.isNotEmpty && !val.contains('@')) {
                            return 'Email không hợp lệ';
                          }
                          return null;
                        },
                      ),
                      const SizedBox(height: 20),
                      SizedBox(
                        height: 48,
                        child: ElevatedButton(
                          onPressed: _submitting ? null : _submit,
                          child: _submitting
                              ? const SizedBox(
                                  height: 22,
                                  width: 22,
                                  child: CircularProgressIndicator(
                                    color: RetroColors.vintageCream,
                                    strokeWidth: 2,
                                  ),
                                )
                              : const Text('GỬI YÊU CẦU ĐẶT LẠI'),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
