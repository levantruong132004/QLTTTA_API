import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/services/auth_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';
import 'package:intl/intl.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final AuthService _authService = AuthService();

  // Form controllers
  final _hoTenController = TextEditingController();
  final _soDienThoaiController = TextEditingController();
  final _emailController = TextEditingController();
  final _diaChiController = TextEditingController();
  final _usernameController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();

  String _gioiTinh = '';
  DateTime? _ngaySinh;
  bool _isLoading = false;
  bool _obscurePassword = true;
  bool _obscureConfirmPassword = true;

  @override
  void dispose() {
    _hoTenController.dispose();
    _soDienThoaiController.dispose();
    _emailController.dispose();
    _diaChiController.dispose();
    _usernameController.dispose();
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  Future<void> _selectDate() async {
    final DateTime? picked = await showDatePicker(
      context: context,
      initialDate: DateTime(2000),
      firstDate: DateTime(1950),
      lastDate: DateTime.now(),
      builder: (context, child) {
        return Theme(
          data: Theme.of(context).copyWith(
            colorScheme: const ColorScheme.light(
              primary: RetroColors.vintageBrown,
              onPrimary: RetroColors.vintageCream,
              surface: RetroColors.vintageWhite,
              onSurface: RetroColors.vintageDarkBrown,
            ),
          ),
          child: child!,
        );
      },
    );
    if (picked != null && picked != _ngaySinh) {
      setState(() {
        _ngaySinh = picked;
      });
    }
  }

  Future<void> _register() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    if (_gioiTinh.isEmpty) {
      _showErrorDialog('Vui lòng chọn giới tính');
      return;
    }

    if (_ngaySinh == null) {
      _showErrorDialog('Vui lòng chọn ngày sinh');
      return;
    }

    if (_passwordController.text != _confirmPasswordController.text) {
      _showErrorDialog('Mật khẩu xác nhận không khớp');
      return;
    }

    setState(() {
      _isLoading = true;
    });

    try {
      final success = await _authService.register(
        fullName: _hoTenController.text,
        sex: _gioiTinh,
        dateOfBirth: _ngaySinh!,
        phoneNumber: _soDienThoaiController.text,
        email: _emailController.text,
        address: _diaChiController.text.isEmpty ? null : _diaChiController.text,
        username: _usernameController.text,
        password: _passwordController.text,
        confirmPassword: _confirmPasswordController.text,
      );

      if (success && mounted) {
        _showSuccessDialog();
      }
    } catch (e) {
      if (mounted) {
        _showErrorDialog(e.toString());
      }
    } finally {
      if (mounted) {
        setState(() {
          _isLoading = false;
        });
      }
    }
  }

  void _showErrorDialog(String message) {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: RetroColors.vintageWhite,
        shape: const RoundedRectangleBorder(
          side: BorderSide(color: RetroColors.vintageBrown, width: 3),
        ),
        title: Row(
          children: [
            Icon(Icons.error_outline, color: RetroColors.vintageBurgundy),
            SizedBox(width: 8),
            Text(
              'LỖI',
              style: TextStyle(
                color: RetroColors.vintageBurgundy,
                fontWeight: FontWeight.bold,
              ),
            ),
          ],
        ),
        content: Text(
          message,
          style: const TextStyle(color: RetroColors.vintageDarkBrown),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            style: TextButton.styleFrom(
              backgroundColor: RetroColors.vintageBrown,
              foregroundColor: RetroColors.vintageCream,
            ),
            child: const Text('ĐÓNG'),
          ),
        ],
      ),
    );
  }

  void _showSuccessDialog() {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (context) => AlertDialog(
        backgroundColor: RetroColors.vintageWhite,
        shape: const RoundedRectangleBorder(
          side: BorderSide(color: RetroColors.vintageBrown, width: 3),
        ),
        title: Row(
          children: [
            Icon(Icons.check_circle_outline, color: RetroColors.vintageGreen),
            SizedBox(width: 8),
            Text(
              'THÀNH CÔNG',
              style: TextStyle(
                color: RetroColors.vintageGreen,
                fontWeight: FontWeight.bold,
              ),
            ),
          ],
        ),
        content: const Text(
          'Đăng ký tài khoản thành công!\nVui lòng đăng nhập.',
          style: TextStyle(color: RetroColors.vintageDarkBrown),
        ),
        actions: [
          TextButton(
            onPressed: () {
              Navigator.pop(context); // Close dialog
              Navigator.pop(context); // Back to login
            },
            style: TextButton.styleFrom(
              backgroundColor: RetroColors.vintageBrown,
              foregroundColor: RetroColors.vintageCream,
            ),
            child: const Text('ĐĂNG NHẬP NGAY'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: RetroColors.vintageCream,
      appBar: AppBar(
        title: const Text('📝 ĐĂNG KÝ TÀI KHOẢN'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => Navigator.pop(context),
        ),
      ),
      body: Container(
        decoration: BoxDecoration(
          image: DecorationImage(
            image: NetworkImage(
                'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNDAiIGhlaWdodD0iNDAiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+PGRlZnM+PHBhdHRlcm4gaWQ9InBhdHRlcm4iIHBhdHRlcm5Vbml0cz0idXNlclNwYWNlT25Vc2UiIHdpZHRoPSI0MCIgaGVpZ2h0PSI0MCI+PHJlY3Qgd2lkdGg9IjQwIiBoZWlnaHQ9IjQwIiBmaWxsPSIjZjRlYWQ1Ii8+PHBhdGggZD0iTTAgMGgyMHYyMEgweiIgZmlsbD0iI2U4ZGNjNCIgb3BhY2l0eT0iMC4zIi8+PHBhdGggZD0iTTIwIDIwaDIwdjIwSDIweiIgZmlsbD0iI2U4ZGNjNCIgb3BhY2l0eT0iMC4zIi8+PC9wYXR0ZXJuPjwvZGVmcz48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSJ1cmwoI3BhdHRlcm4pIi8+PC9zdmc+'),
            repeat: ImageRepeat.repeat,
            opacity: 0.3,
          ),
        ),
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(16),
          child: Form(
            key: _formKey,
            child: Container(
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
                children: [
                  // Header
                  Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(16),
                    decoration: BoxDecoration(
                      gradient: LinearGradient(
                        colors: [
                          RetroColors.vintageBrown,
                          RetroColors.vintageDarkBrown
                        ],
                        begin: Alignment.topCenter,
                        end: Alignment.bottomCenter,
                      ),
                      border: Border(
                        bottom:
                            BorderSide(color: RetroColors.vintageGold, width: 3),
                      ),
                    ),
                    child: Column(
                      children: const [
                        Icon(Icons.person_add, size: 48, color: RetroColors.vintageCream),
                        SizedBox(height: 8),
                        Text(
                          'TẠO TÀI KHOẢN MỚI',
                          style: TextStyle(
                            fontSize: 20,
                            fontWeight: FontWeight.bold,
                            color: RetroColors.vintageCream,
                            letterSpacing: 2,
                          ),
                        ),
                        SizedBox(height: 4),
                        Text(
                          '~ Trung tâm Tiếng Anh LDA ~',
                          style: TextStyle(
                            fontSize: 14,
                            fontStyle: FontStyle.italic,
                            color: RetroColors.vintageGold,
                          ),
                        ),
                      ],
                    ),
                  ),

                  // Form Body
                  Container(
                    padding: const EdgeInsets.all(20),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        // Họ tên
                        _buildTextField(
                          controller: _hoTenController,
                          label: 'HỌ VÀ TÊN *',
                          icon: Icons.person,
                          hint: 'Nhập họ và tên đầy đủ',
                          validator: (value) {
                            if (value == null || value.isEmpty) {
                              return 'Vui lòng nhập họ tên';
                            }
                            return null;
                          },
                        ),
                        const SizedBox(height: 16),

                        // Giới tính
                        const Text(
                          'GIỚI TÍNH *',
                          style: TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                            color: RetroColors.vintageDarkBrown,
                            letterSpacing: 1,
                          ),
                        ),
                        const SizedBox(height: 8),
                        Row(
                          children: [
                            Expanded(
                              child: _buildGenderOption('Nam', Icons.male),
                            ),
                            const SizedBox(width: 16),
                            Expanded(
                              child: _buildGenderOption('Nữ', Icons.female),
                            ),
                          ],
                        ),
                        const SizedBox(height: 16),

                        // Ngày sinh
                        const Text(
                          'NGÀY SINH *',
                          style: TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                            color: RetroColors.vintageDarkBrown,
                            letterSpacing: 1,
                          ),
                        ),
                        const SizedBox(height: 8),
                        InkWell(
                          onTap: _selectDate,
                          child: Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: RetroColors.vintageOffWhite,
                              border: Border.all(
                                  color: RetroColors.vintageBrown, width: 2),
                            ),
                            child: Row(
                              children: [
                                const Icon(Icons.calendar_today,
                                    color: RetroColors.vintageBrown),
                                const SizedBox(width: 12),
                                Text(
                                  _ngaySinh == null
                                      ? 'Chọn ngày sinh'
                                      : DateFormat('dd/MM/yyyy')
                                          .format(_ngaySinh!),
                                  style: TextStyle(
                                    fontSize: 16,
                                    color: _ngaySinh == null
                                        ? RetroColors.vintageGray
                                        : RetroColors.vintageDarkBrown,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                        const SizedBox(height: 16),

                        // Số điện thoại
                        _buildTextField(
                          controller: _soDienThoaiController,
                          label: 'SỐ ĐIỆN THOẠI *',
                          icon: Icons.phone,
                          hint: 'Nhập số điện thoại',
                          keyboardType: TextInputType.phone,
                          validator: (value) {
                            if (value == null || value.isEmpty) {
                              return 'Vui lòng nhập số điện thoại';
                            }
                            return null;
                          },
                        ),
                        const SizedBox(height: 16),

                        // Email
                        _buildTextField(
                          controller: _emailController,
                          label: 'EMAIL *',
                          icon: Icons.email,
                          hint: 'Nhập địa chỉ email',
                          keyboardType: TextInputType.emailAddress,
                          validator: (value) {
                            if (value == null || value.isEmpty) {
                              return 'Vui lòng nhập email';
                            }
                            if (!value.contains('@')) {
                              return 'Email không hợp lệ';
                            }
                            return null;
                          },
                        ),
                        const SizedBox(height: 16),

                        // Địa chỉ (optional)
                        _buildTextField(
                          controller: _diaChiController,
                          label: 'ĐỊA CHỈ',
                          icon: Icons.home,
                          hint: 'Nhập địa chỉ (tùy chọn)',
                        ),
                        const SizedBox(height: 16),

                        // Divider
                        Container(
                          height: 2,
                          color: RetroColors.vintageBrown.withOpacity(0.3),
                          margin: const EdgeInsets.symmetric(vertical: 8),
                        ),

                        const Text(
                          'THÔNG TIN TÀI KHOẢN',
                          style: TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.bold,
                            color: RetroColors.vintageBrown,
                            letterSpacing: 1,
                          ),
                        ),
                        const SizedBox(height: 16),

                        // Tên tài khoản
                        _buildTextField(
                          controller: _usernameController,
                          label: 'TÊN TÀI KHOẢN *',
                          icon: Icons.account_circle,
                          hint: 'Nhập tên tài khoản (3-50 ký tự)',
                          validator: (value) {
                            if (value == null || value.isEmpty) {
                              return 'Vui lòng nhập tên tài khoản';
                            }
                            if (value.length < 3 || value.length > 50) {
                              return 'Tên tài khoản phải từ 3-50 ký tự';
                            }
                            return null;
                          },
                        ),
                        const SizedBox(height: 16),

                        // Mật khẩu
                        _buildTextField(
                          controller: _passwordController,
                          label: 'MẬT KHẨU *',
                          icon: Icons.lock,
                          hint: 'Nhập mật khẩu (tối thiểu 6 ký tự)',
                          obscureText: _obscurePassword,
                          suffixIcon: IconButton(
                            icon: Icon(
                              _obscurePassword
                                  ? Icons.visibility_off
                                  : Icons.visibility,
                              color: RetroColors.vintageBrown,
                            ),
                            onPressed: () {
                              setState(() {
                                _obscurePassword = !_obscurePassword;
                              });
                            },
                          ),
                          validator: (value) {
                            if (value == null || value.isEmpty) {
                              return 'Vui lòng nhập mật khẩu';
                            }
                            if (value.length < 6) {
                              return 'Mật khẩu phải có ít nhất 6 ký tự';
                            }
                            return null;
                          },
                        ),
                        const SizedBox(height: 16),

                        // Xác nhận mật khẩu
                        _buildTextField(
                          controller: _confirmPasswordController,
                          label: 'XÁC NHẬN MẬT KHẨU *',
                          icon: Icons.lock_outline,
                          hint: 'Nhập lại mật khẩu',
                          obscureText: _obscureConfirmPassword,
                          suffixIcon: IconButton(
                            icon: Icon(
                              _obscureConfirmPassword
                                  ? Icons.visibility_off
                                  : Icons.visibility,
                              color: RetroColors.vintageBrown,
                            ),
                            onPressed: () {
                              setState(() {
                                _obscureConfirmPassword =
                                    !_obscureConfirmPassword;
                              });
                            },
                          ),
                          validator: (value) {
                            if (value == null || value.isEmpty) {
                              return 'Vui lòng xác nhận mật khẩu';
                            }
                            if (value != _passwordController.text) {
                              return 'Mật khẩu xác nhận không khớp';
                            }
                            return null;
                          },
                        ),
                        const SizedBox(height: 24),

                        // Submit button
                        SizedBox(
                          width: double.infinity,
                          height: 50,
                          child: ElevatedButton(
                            onPressed: _isLoading ? null : _register,
                            style: ElevatedButton.styleFrom(
                              backgroundColor: RetroColors.vintageBrown,
                              foregroundColor: RetroColors.vintageCream,
                              shape: const RoundedRectangleBorder(),
                            ),
                            child: _isLoading
                                ? const SizedBox(
                                    height: 24,
                                    width: 24,
                                    child: CircularProgressIndicator(
                                      color: RetroColors.vintageCream,
                                      strokeWidth: 2,
                                    ),
                                  )
                                : Row(
                                    mainAxisAlignment: MainAxisAlignment.center,
                                    children: const [
                                      Icon(Icons.person_add),
                                      SizedBox(width: 8),
                                      Text(
                                        'ĐĂNG KÝ TÀI KHOẢN',
                                        style: TextStyle(
                                          fontSize: 16,
                                          fontWeight: FontWeight.bold,
                                          letterSpacing: 1,
                                        ),
                                      ),
                                    ],
                                  ),
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

  Widget _buildTextField({
    required TextEditingController controller,
    required String label,
    required IconData icon,
    required String hint,
    bool obscureText = false,
    TextInputType? keyboardType,
    String? Function(String?)? validator,
    Widget? suffixIcon,
  }) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.bold,
            color: RetroColors.vintageDarkBrown,
            letterSpacing: 1,
          ),
        ),
        const SizedBox(height: 8),
        TextFormField(
          controller: controller,
          obscureText: obscureText,
          keyboardType: keyboardType,
          validator: validator,
          decoration: InputDecoration(
            hintText: hint,
            hintStyle: TextStyle(
              color: RetroColors.vintageGray.withOpacity(0.6),
              fontStyle: FontStyle.italic,
            ),
            prefixIcon: Icon(icon, color: RetroColors.vintageBrown),
            suffixIcon: suffixIcon,
            filled: true,
            fillColor: RetroColors.vintageOffWhite,
            border: OutlineInputBorder(
              borderRadius: BorderRadius.zero,
              borderSide:
                  BorderSide(color: RetroColors.vintageBrown, width: 2),
            ),
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.zero,
              borderSide:
                  BorderSide(color: RetroColors.vintageBrown, width: 2),
            ),
            focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.zero,
              borderSide:
                  BorderSide(color: RetroColors.vintageDarkBrown, width: 3),
            ),
            errorBorder: OutlineInputBorder(
              borderRadius: BorderRadius.zero,
              borderSide:
                  BorderSide(color: RetroColors.vintageBurgundy, width: 2),
            ),
            focusedErrorBorder: OutlineInputBorder(
              borderRadius: BorderRadius.zero,
              borderSide:
                  BorderSide(color: RetroColors.vintageBurgundy, width: 3),
            ),
            contentPadding:
                const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          ),
          style: const TextStyle(
            fontSize: 16,
            color: RetroColors.vintageDarkBrown,
          ),
        ),
      ],
    );
  }

  Widget _buildGenderOption(String gender, IconData icon) {
    final isSelected = _gioiTinh == gender;
    return InkWell(
      onTap: () {
        setState(() {
          _gioiTinh = gender;
        });
      },
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: isSelected
              ? RetroColors.vintageBrown
              : RetroColors.vintageOffWhite,
          border: Border.all(
            color: isSelected
                ? RetroColors.vintageDarkBrown
                : RetroColors.vintageBrown,
            width: isSelected ? 3 : 2,
          ),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              icon,
              color: isSelected
                  ? RetroColors.vintageCream
                  : RetroColors.vintageBrown,
            ),
            const SizedBox(width: 8),
            Text(
              gender,
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.bold,
                color: isSelected
                    ? RetroColors.vintageCream
                    : RetroColors.vintageDarkBrown,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
