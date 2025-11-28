import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/services/auth_service.dart';
import 'package:qlttta_app_mobile/screens/register_verify_screen.dart';
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
              primary: Color(0xFF8B4513),
              onPrimary: Colors.white,
              surface: Colors.white,
              onSurface: Color(0xFF1a1a1a),
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
      // Use OTP registration flow
      final res = await _authService.initiateRegisterOtp(
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

      if (!mounted) return;

      if (res['success'] == true && res['correlationId'] != null) {
        // Navigate to OTP verification screen
        Navigator.of(context).push(
          MaterialPageRoute(
            builder: (_) => RegisterVerifyScreen(
              correlationId: res['correlationId'],
              email: _emailController.text,
            ),
          ),
        );
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(res['message'] ?? 'Đã gửi OTP đến email'),
            backgroundColor: const Color(0xFF4CAF50),
          ),
        );
      } else {
        _showErrorDialog(res['message'] ?? 'Không thể gửi OTP');
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
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Row(
          children: [
            Icon(Icons.error_outline_rounded, color: Color(0xFFF44336)),
            SizedBox(width: 8),
            Text(
              'Lỗi',
              style: TextStyle(
                color: Color(0xFFF44336),
                fontWeight: FontWeight.bold,
              ),
            ),
          ],
        ),
        content: Text(
          message,
          style: const TextStyle(color: Color(0xFF1a1a1a)),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            style: TextButton.styleFrom(
              backgroundColor: const Color(0xFF8B4513),
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
            ),
            child: const Text('Đóng'),
          ),
        ],
      ),
    );
  }

  // Success dialog removed; OTP flow navigates to verify screen instead.

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        elevation: 0,
        backgroundColor: Colors.transparent,
        leading: IconButton(
          icon: Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: Colors.white.withOpacity(0.2),
              borderRadius: BorderRadius.circular(12),
            ),
            child: const Icon(Icons.arrow_back_rounded, size: 20),
          ),
          onPressed: () => Navigator.of(context).pop(),
        ),
        title: const Text(
          'Đăng ký tài khoản',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.w600),
        ),
      ),
      body: Container(
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [
              const Color(0xFF8B4513).withOpacity(0.85),
              const Color(0xFFD2691E).withOpacity(0.75),
              const Color(0xFFF4A460).withOpacity(0.65),
            ],
          ),
        ),
        child: SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(16, 80, 16, 16),
            child: Form(
              key: _formKey,
              child: Container(
                padding: const EdgeInsets.all(28),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(24),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withOpacity(0.2),
                      blurRadius: 20,
                      offset: const Offset(0, 10),
                    ),
                  ],
                ),
                child: Column(
                  children: [
                    // Header with gradient
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(24),
                      decoration: BoxDecoration(
                        gradient: const LinearGradient(
                          colors: [Color(0xFF8B4513), Color(0xFFD2691E)],
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                        ),
                        borderRadius: BorderRadius.circular(16),
                        boxShadow: [
                          BoxShadow(
                            color: const Color(0xFF8B4513).withOpacity(0.3),
                            blurRadius: 8,
                            offset: const Offset(0, 4),
                          ),
                        ],
                      ),
                      child: const Column(
                        children: [
                          Icon(Icons.person_add_rounded, size: 64, color: Colors.white),
                          SizedBox(height: 12),
                          Text(
                            'Tạo tài khoản mới',
                            style: TextStyle(
                              fontSize: 22,
                              fontWeight: FontWeight.w700,
                              color: Colors.white,
                            ),
                          ),
                          SizedBox(height: 4),
                          Text(
                            'Trung tâm Tiếng Anh LDA',
                            style: TextStyle(
                              fontSize: 14,
                              fontStyle: FontStyle.italic,
                              color: Colors.white70,
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 28),

                    // Họ tên
                    _buildTextField(
                      controller: _hoTenController,
                      label: 'Họ và tên',
                      icon: Icons.person_rounded,
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
                    const Align(
                      alignment: Alignment.centerLeft,
                      child: Text(
                        'Giới tính',
                        style: TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.w600,
                          color: Color(0xFF1a1a1a),
                        ),
                      ),
                    ),
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        Expanded(
                          child: _buildGenderOption('Nam', Icons.male_rounded),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: _buildGenderOption('Nữ', Icons.female_rounded),
                        ),
                      ],
                    ),
                    const SizedBox(height: 16),

                    // Ngày sinh
                    const Align(
                      alignment: Alignment.centerLeft,
                      child: Text(
                        'Ngày sinh',
                        style: TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.w600,
                          color: Color(0xFF1a1a1a),
                        ),
                      ),
                    ),
                    const SizedBox(height: 8),
                    InkWell(
                      onTap: _selectDate,
                      borderRadius: BorderRadius.circular(16),
                      child: Container(
                        padding: const EdgeInsets.all(16),
                        decoration: BoxDecoration(
                          color: Colors.grey.shade100,
                          borderRadius: BorderRadius.circular(16),
                        ),
                        child: Row(
                          children: [
                            Icon(Icons.calendar_today_rounded, size: 20, color: Colors.grey.shade600),
                            const SizedBox(width: 12),
                            Text(
                              _ngaySinh == null
                                  ? 'Chọn ngày sinh'
                                  : DateFormat('dd/MM/yyyy').format(_ngaySinh!),
                              style: TextStyle(
                                fontSize: 15,
                                color: _ngaySinh == null ? Colors.grey.shade500 : const Color(0xFF1a1a1a),
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
                      label: 'Số điện thoại',
                      icon: Icons.phone_rounded,
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
                      label: 'Email',
                      icon: Icons.email_rounded,
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
                      label: 'Địa chỉ (tuỳ chọn)',
                      icon: Icons.home_rounded,
                      hint: 'Nhập địa chỉ',
                    ),
                    const SizedBox(height: 24),

                    // Divider
                    Divider(color: Colors.grey.shade300, thickness: 1),
                    const SizedBox(height: 4),
                    const Text(
                      'Thông tin tài khoản',
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                        color: Color(0xFF8B4513),
                      ),
                    ),
                    const SizedBox(height: 20),

                    // Tên tài khoản
                    _buildTextField(
                      controller: _usernameController,
                      label: 'Tên tài khoản',
                      icon: Icons.account_circle_rounded,
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
                      label: 'Mật khẩu',
                      icon: Icons.lock_rounded,
                      hint: 'Nhập mật khẩu (tối thiểu 6 ký tự)',
                      obscureText: _obscurePassword,
                      suffixIcon: IconButton(
                        icon: Icon(
                          _obscurePassword ? Icons.visibility_off_rounded : Icons.visibility_rounded,
                          color: Colors.grey.shade600,
                          size: 20,
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
                      label: 'Xác nhận mật khẩu',
                      icon: Icons.lock_outline_rounded,
                      hint: 'Nhập lại mật khẩu',
                      obscureText: _obscureConfirmPassword,
                      suffixIcon: IconButton(
                        icon: Icon(
                          _obscureConfirmPassword ? Icons.visibility_off_rounded : Icons.visibility_rounded,
                          color: Colors.grey.shade600,
                          size: 20,
                        ),
                        onPressed: () {
                          setState(() {
                            _obscureConfirmPassword = !_obscureConfirmPassword;
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
                    const SizedBox(height: 32),

                    // Submit button
                    SizedBox(
                      width: double.infinity,
                      height: 56,
                      child: ElevatedButton(
                        onPressed: _isLoading ? null : _register,
                        style: ElevatedButton.styleFrom(
                          backgroundColor: Colors.transparent,
                          foregroundColor: Colors.white,
                          padding: EdgeInsets.zero,
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(16),
                          ),
                          elevation: 0,
                          shadowColor: Colors.transparent,
                          disabledBackgroundColor: Colors.grey.shade300,
                        ),
                        child: _isLoading
                            ? const SizedBox(
                                height: 24,
                                width: 24,
                                child: CircularProgressIndicator(
                                  color: Colors.white,
                                  strokeWidth: 2,
                                ),
                              )
                            : Ink(
                                decoration: BoxDecoration(
                                  gradient: const LinearGradient(
                                    colors: [Color(0xFF8B4513), Color(0xFFD2691E)],
                                    begin: Alignment.centerLeft,
                                    end: Alignment.centerRight,
                                  ),
                                  borderRadius: BorderRadius.circular(16),
                                  boxShadow: [
                                    BoxShadow(
                                      color: const Color(0xFF8B4513).withOpacity(0.4),
                                      blurRadius: 12,
                                      offset: const Offset(0, 6),
                                    ),
                                  ],
                                ),
                                child: Container(
                                  alignment: Alignment.center,
                                  child: const Row(
                                    mainAxisAlignment: MainAxisAlignment.center,
                                    children: [
                                      Icon(Icons.person_add_rounded, size: 20),
                                      SizedBox(width: 10),
                                      Text(
                                        'Đăng ký tài khoản',
                                        style: TextStyle(
                                          fontSize: 16,
                                          fontWeight: FontWeight.w600,
                                          letterSpacing: 0.5,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              ),
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
            fontSize: 14,
            fontWeight: FontWeight.w600,
            color: Color(0xFF1a1a1a),
          ),
        ),
        const SizedBox(height: 8),
        Container(
          decoration: BoxDecoration(
            color: Colors.grey.shade100,
            borderRadius: BorderRadius.circular(16),
          ),
          child: TextFormField(
            controller: controller,
            obscureText: obscureText,
            keyboardType: keyboardType,
            validator: validator,
            decoration: InputDecoration(
              hintText: hint,
              hintStyle: TextStyle(
                color: Colors.grey.shade500,
                fontSize: 14,
              ),
              prefixIcon: Icon(icon, color: Colors.grey.shade600, size: 20),
              suffixIcon: suffixIcon,
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(16),
                borderSide: BorderSide.none,
              ),
              filled: true,
              fillColor: Colors.transparent,
              contentPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
              errorStyle: const TextStyle(fontSize: 12),
            ),
            style: const TextStyle(
              fontSize: 15,
              color: Color(0xFF1a1a1a),
            ),
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
      borderRadius: BorderRadius.circular(16),
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 14),
        decoration: BoxDecoration(
          gradient: isSelected
              ? const LinearGradient(
                  colors: [Color(0xFF8B4513), Color(0xFFD2691E)],
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                )
              : null,
          color: isSelected ? null : Colors.grey.shade100,
          borderRadius: BorderRadius.circular(16),
          border: isSelected ? Border.all(color: const Color(0xFF8B4513), width: 2) : null,
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              icon,
              color: isSelected ? Colors.white : Colors.grey.shade600,
              size: 20,
            ),
            const SizedBox(width: 8),
            Text(
              gender,
              style: TextStyle(
                fontSize: 15,
                fontWeight: FontWeight.w600,
                color: isSelected ? Colors.white : const Color(0xFF1a1a1a),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
