import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:qlttta_app_mobile/models/registration.dart';
import 'package:qlttta_app_mobile/services/registration_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';
import 'package:qlttta_app_mobile/screens/qr_scan_registration_screen.dart';

class RegistrationsManagementScreen extends StatefulWidget {
  const RegistrationsManagementScreen({super.key});

  @override
  State<RegistrationsManagementScreen> createState() =>
      _RegistrationsManagementScreenState();
}

class _RegistrationsManagementScreenState
    extends State<RegistrationsManagementScreen> {
  final RegistrationService _registrationService = RegistrationService();
  List<Registration> _registrations = [];
  bool _isLoading = false;
  String? _filterStatus;

  @override
  void initState() {
    super.initState();
    _loadData();
  }

  Future<void> _loadData() async {
    setState(() => _isLoading = true);
    final registrations = await _registrationService.getAllRegistrations(
      status: _filterStatus,
    );
    setState(() {
      _registrations = registrations;
      _isLoading = false;
    });
  }

  Future<void> _approveRegistration(Registration reg) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Duyệt đơn'),
        content: Text(
          'Duyệt đơn đăng ký của ${reg.studentName ?? "học viên"}?\n'
          'Khóa học: ${reg.courseName ?? "N/A"}'
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Hủy'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.pop(context, true),
            style: ElevatedButton.styleFrom(
              backgroundColor: RetroColors.success,
            ),
            child: const Text('Duyệt'),
          ),
        ],
      ),
    );

    if (confirmed == true && mounted) {
      final result = await _registrationService.approveRegistration(reg.registrationId);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(result['message'] ?? 'Hoàn tất'),
            backgroundColor: result['success'] ? RetroColors.success : RetroColors.error,
          ),
        );
        if (result['success']) _loadData();
      }
    }
  }

  Future<void> _rejectRegistration(Registration reg) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Từ chối đơn'),
        content: Text(
          'Từ chối đơn đăng ký của ${reg.studentName ?? "học viên"}?\n'
          'Khóa học: ${reg.courseName ?? "N/A"}'
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Hủy'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.pop(context, true),
            style: ElevatedButton.styleFrom(
              backgroundColor: RetroColors.error,
            ),
            child: const Text('Từ chối'),
          ),
        ],
      ),
    );

    if (confirmed == true && mounted) {
      final result = await _registrationService.rejectRegistration(reg.registrationId);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(result['message'] ?? 'Hoàn tất'),
            backgroundColor: result['success'] ? RetroColors.success : RetroColors.error,
          ),
        );
        if (result['success']) _loadData();
      }
    }
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
          'Quản lý đăng ký',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.w600),
        ),
        actions: [
          IconButton(
            tooltip: 'Quét QR để tìm',
            icon: Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: Colors.white.withOpacity(0.2),
                borderRadius: BorderRadius.circular(12),
              ),
              child: const Icon(Icons.qr_code_scanner_rounded, size: 20),
            ),
            onPressed: () async {
              final result = await Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const QrScanRegistrationScreen()),
              );
              if (result != null) {
                _loadData();
              }
            },
          ),
          PopupMenuButton<String>(
            icon: Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: Colors.white.withOpacity(0.2),
                borderRadius: BorderRadius.circular(12),
              ),
              child: const Icon(Icons.filter_list_rounded, size: 20),
            ),
            tooltip: 'Lọc theo trạng thái',
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
            onSelected: (value) {
              setState(() {
                _filterStatus = value == 'all' ? null : value;
              });
              _loadData();
            },
            itemBuilder: (context) => [
              const PopupMenuItem(value: 'all', child: Text('Tất cả')),
              const PopupMenuItem(value: 'pending', child: Text('Chờ duyệt')),
              const PopupMenuItem(value: 'approved', child: Text('Đã duyệt')),
              const PopupMenuItem(value: 'rejected', child: Text('Từ chối')),
            ],
          ),
        ],
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
        child: _isLoading
            ? const Center(child: CircularProgressIndicator(color: Colors.white))
            : RefreshIndicator(
                onRefresh: _loadData,
                color: Colors.white,
                child: _registrations.isEmpty
                    ? Center(
                        child: Container(
                          margin: const EdgeInsets.symmetric(horizontal: 32),
                          padding: const EdgeInsets.all(24),
                          decoration: BoxDecoration(
                            color: Colors.white.withOpacity(0.9),
                            borderRadius: BorderRadius.circular(20),
                          ),
                          child: Column(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Icon(
                                Icons.assignment_outlined,
                                size: 64,
                                color: const Color(0xFF8B4513).withOpacity(0.6),
                              ),
                              const SizedBox(height: 16),
                              Text(
                                'Không có đơn đăng ký',
                                style: TextStyle(
                                  fontSize: 16,
                                  color: Colors.grey.shade700,
                                  fontWeight: FontWeight.w500,
                                ),
                              ),
                            ],
                          ),
                        ),
                      )
                    : ListView.builder(
                        padding: EdgeInsets.fromLTRB(16, MediaQuery.of(context).padding.top + 80, 16, 16),
                        itemCount: _registrations.length,
                        itemBuilder: (context, index) {
                          final reg = _registrations[index];
                          return _buildRegistrationCard(reg);
                        },
                      ),
              ),
      ),
    );
  }

  Widget _buildRegistrationCard(Registration reg) {
    final isPending = reg.status.toLowerCase() == 'pending' ||
        reg.status.toLowerCase() == 'chờ duyệt';

    Color modernStatusColor;
    IconData statusIcon;
    final status = reg.status.toLowerCase();
    
    if (status.contains('approved') || status.contains('duyệt')) {
      modernStatusColor = const Color(0xFF4CAF50);
      statusIcon = Icons.check_circle_rounded;
    } else if (status.contains('pending') || status.contains('chờ')) {
      modernStatusColor = const Color(0xFFFF9800);
      statusIcon = Icons.schedule_rounded;
    } else if (status.contains('reject') || status.contains('từ chối')) {
      modernStatusColor = const Color(0xFFF44336);
      statusIcon = Icons.cancel_rounded;
    } else {
      modernStatusColor = const Color(0xFF2196F3);
      statusIcon = Icons.info_rounded;
    }

    return Container(
      margin: const EdgeInsets.only(bottom: 16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.1),
            blurRadius: 12,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'Đơn #${reg.registrationId}',
                  style: const TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                    color: Color(0xFF1a1a1a),
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                  decoration: BoxDecoration(
                    color: modernStatusColor.withOpacity(0.15),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(statusIcon, size: 16, color: modernStatusColor),
                      const SizedBox(width: 4),
                      Text(
                        reg.getStatusText(),
                        style: TextStyle(
                          color: modernStatusColor,
                          fontWeight: FontWeight.w600,
                          fontSize: 13,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            _buildInfoRow(Icons.person_rounded, 'Học viên', reg.studentName ?? '-'),
            _buildInfoRow(Icons.menu_book_rounded, 'Khóa học', reg.courseName ?? '-'),
            if (reg.className != null)
              _buildInfoRow(Icons.class_rounded, 'Lớp', reg.className!),
            _buildInfoRow(
              Icons.calendar_today_rounded,
              'Ngày đăng ký',
              DateFormat('dd/MM/yyyy').format(reg.registrationDate),
            ),
            if (reg.notes != null && reg.notes!.isNotEmpty)
              _buildInfoRow(Icons.note_rounded, 'Ghi chú', reg.notes!),
            if (isPending) ...[
              const SizedBox(height: 16),
              Row(
                children: [
                  Expanded(
                    child: ElevatedButton.icon(
                      onPressed: () => _rejectRegistration(reg),
                      icon: const Icon(Icons.close_rounded, size: 20),
                      label: const Text('Từ chối', style: TextStyle(fontWeight: FontWeight.w600)),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: const Color(0xFFF44336),
                        foregroundColor: Colors.white,
                        padding: const EdgeInsets.symmetric(vertical: 12),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12),
                        ),
                        elevation: 0,
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: ElevatedButton.icon(
                      onPressed: () => _approveRegistration(reg),
                      icon: const Icon(Icons.check_rounded, size: 20),
                      label: const Text('Duyệt', style: TextStyle(fontWeight: FontWeight.w600)),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: const Color(0xFF4CAF50),
                        foregroundColor: Colors.white,
                        padding: const EdgeInsets.symmetric(vertical: 12),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12),
                        ),
                        elevation: 0,
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildInfoRow(IconData icon, String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: const Color(0xFF8B4513).withOpacity(0.1),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Icon(icon, size: 18, color: const Color(0xFF8B4513)),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  style: TextStyle(
                    fontSize: 12,
                    color: Colors.grey.shade600,
                    fontWeight: FontWeight.w500,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  value,
                  style: const TextStyle(
                    fontSize: 15,
                    color: Color(0xFF1a1a1a),
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
