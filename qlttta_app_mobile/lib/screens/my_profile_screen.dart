import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:qlttta_app_mobile/models/student_profile_details.dart';
import 'package:qlttta_app_mobile/services/profile_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';

class MyProfileScreen extends StatefulWidget {
  const MyProfileScreen({super.key});

  @override
  State<MyProfileScreen> createState() => _MyProfileScreenState();
}

class _MyProfileScreenState extends State<MyProfileScreen> {
  final ProfileService _profileService = ProfileService();
  late Future<StudentProfileDetails?> _profileFuture;

  @override
  void initState() {
    super.initState();
    _profileFuture = _profileService.getMyProfile();
  }

  Future<void> _reload() async {
    setState(() {
      _profileFuture = _profileService.getMyProfile();
    });
    await _profileFuture;
  }

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
          'Hồ sơ của tôi',
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
          child: Material(
            color: Colors.transparent,
            child: RefreshIndicator(
              onRefresh: _reload,
              color: Colors.white,
              child: FutureBuilder<StudentProfileDetails?>(
                future: _profileFuture,
                builder: (context, snapshot) {
                  if (snapshot.connectionState == ConnectionState.waiting) {
                    return _buildScrollWrapper(
                      const Center(
                        child: CircularProgressIndicator(color: Colors.white),
                      ),
                    );
                  }

                  if (snapshot.hasError) {
                    return _buildScrollWrapper(
                      _buildStatusCard(
                        icon: Icons.error_outline,
                        title: 'Không thể tải hồ sơ',
                        message: snapshot.error.toString(),
                      ),
                    );
                  }

                  final profile = snapshot.data;
                  if (profile == null) {
                    return _buildScrollWrapper(
                      _buildStatusCard(
                        icon: Icons.person_off_rounded,
                        title: 'Chưa có thông tin',
                        message: 'Hệ thống chưa tìm thấy hồ sơ của bạn.',
                      ),
                    );
                  }

                  return _buildScrollWrapper(
                    Column(
                      children: [
                        _buildHeader(profile),
                        const SizedBox(height: 20),
                        _buildDetailsCard(profile),
                      ],
                    ),
                  );
                },
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildScrollWrapper(Widget child) {
    final topPadding = MediaQuery.of(context).padding.top;
    return SingleChildScrollView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: EdgeInsets.fromLTRB(20, topPadding + 60, 20, 28),
      child: child,
    );
  }

  Widget _buildHeader(StudentProfileDetails profile) {
    final trimmed = profile.fullName.trim();
    final initials = trimmed.isNotEmpty ? trimmed.substring(0, 1).toUpperCase() : '?';
    return Container(
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.15),
            blurRadius: 20,
            offset: const Offset(0, 8),
          ),
        ],
      ),
      child: Column(
        children: [
          Container(
            width: 100,
            height: 100,
            decoration: BoxDecoration(
              gradient: const LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [Color(0xFF8B4513), Color(0xFFD2691E)],
              ),
              borderRadius: BorderRadius.circular(24),
              boxShadow: [
                BoxShadow(
                  color: const Color(0xFF8B4513).withOpacity(0.4),
                  blurRadius: 16,
                  offset: const Offset(0, 8),
                ),
              ],
            ),
            child: Center(
              child: Text(
                initials,
                style: const TextStyle(
                  fontSize: 48,
                  fontWeight: FontWeight.bold,
                  color: Colors.white,
                ),
              ),
            ),
          ),
          const SizedBox(height: 20),
          Text(
            profile.fullName,
            style: const TextStyle(
              fontSize: 24,
              fontWeight: FontWeight.bold,
              color: Color(0xFF1a1a1a),
            ),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            decoration: BoxDecoration(
              color: const Color(0xFF8B4513).withOpacity(0.1),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Text(
              profile.studentCode.isNotEmpty ? profile.studentCode : 'Đang cập nhật mã học viên',
              style: const TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w600,
                color: Color(0xFF8B4513),
                letterSpacing: 1.2,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildDetailsCard(StudentProfileDetails profile) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.1),
            blurRadius: 16,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        children: [
          _buildDetailRow(
            Icons.person_outline_rounded,
            'Giới tính',
            _formatValue(profile.gender),
            isFirst: true,
          ),
          _buildDetailRow(Icons.cake_outlined, 'Ngày sinh', _formatDate(profile.dateOfBirth)),
          _buildDetailRow(Icons.phone_rounded, 'Số điện thoại', _formatValue(profile.phoneNumber)),
          _buildDetailRow(Icons.mail_outline_rounded, 'Email', _formatValue(profile.email)),
          _buildDetailRow(
            Icons.home_outlined,
            'Địa chỉ',
            _formatValue(profile.address),
            isLast: true,
          ),
        ],
      ),
    );
  }

  Widget _buildDetailRow(
    IconData icon,
    String label,
    String value, {
    bool isFirst = false,
    bool isLast = false,
  }) {
    return Container(
      decoration: BoxDecoration(
        border: Border(
          top: isFirst ? BorderSide.none : BorderSide(color: Colors.grey.shade200, width: 1),
          bottom: isLast ? BorderSide.none : BorderSide(color: Colors.grey.shade100, width: 1),
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: const Color(0xFF8B4513).withOpacity(0.1),
                borderRadius: BorderRadius.circular(12),
              ),
              child: Icon(icon, color: const Color(0xFF8B4513), size: 24),
            ),
            const SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    label,
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w500,
                      color: Colors.grey.shade600,
                      letterSpacing: 0.3,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    value,
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w600,
                      color: Color(0xFF1a1a1a),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildStatusCard({required IconData icon, required String title, required String message}) {
    return Container(
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: RetroColors.vintageWhite,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: RetroColors.vintageBrown, width: 3),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 48, color: RetroColors.vintageBurgundy),
          const SizedBox(height: 16),
          Text(
            title,
            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 8),
          Text(
            message,
            style: const TextStyle(fontSize: 14, color: RetroColors.vintageGray),
            textAlign: TextAlign.center,
          ),
        ],
      ),
    );
  }

  String _formatValue(String? value) {
    return (value == null || value.trim().isEmpty) ? 'Chưa cập nhật' : value.trim();
  }

  String _formatDate(DateTime? date) {
    if (date == null) return 'Chưa cập nhật';
    return DateFormat('dd/MM/yyyy').format(date.toLocal());
  }
}
