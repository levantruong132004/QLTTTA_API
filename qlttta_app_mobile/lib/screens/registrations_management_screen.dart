import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:qlttta_app_mobile/models/registration.dart';
import 'package:qlttta_app_mobile/services/registration_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';

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

  Color _getStatusColor(String status) {
    switch (status.toLowerCase()) {
      case 'approved':
      case 'đã duyệt':
        return RetroColors.success;
      case 'pending':
      case 'chờ duyệt':
        return RetroColors.warning;
      case 'rejected':
      case 'từ chối':
        return RetroColors.error;
      default:
        return RetroColors.textSecondary;
    }
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
      backgroundColor: RetroColors.background,
      appBar: AppBar(
        elevation: 0,
        backgroundColor: RetroColors.primary,
        title: const Text('QUẢN LÝ ĐĂNG KÝ'),
        actions: [
          PopupMenuButton<String>(
            icon: const Icon(Icons.filter_list_rounded),
            tooltip: 'Lọc theo trạng thái',
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
      body: _isLoading
          ? const Center(child: CircularProgressIndicator(color: RetroColors.primary))
          : RefreshIndicator(
              onRefresh: _loadData,
              child: _registrations.isEmpty
                  ? Center(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Icon(Icons.assignment_outlined,
                              size: 64, color: RetroColors.textSecondary),
                          const SizedBox(height: 16),
                          Text(
                            'Không có đơn đăng ký',
                            style: TextStyle(
                                fontSize: 16, color: RetroColors.textSecondary),
                          ),
                        ],
                      ),
                    )
                  : ListView.builder(
                      padding: const EdgeInsets.all(16),
                      itemCount: _registrations.length,
                      itemBuilder: (context, index) {
                        final reg = _registrations[index];
                        return _buildRegistrationCard(reg);
                      },
                    ),
            ),
    );
  }

  Widget _buildRegistrationCard(Registration reg) {
    final statusColor = _getStatusColor(reg.status);
    final isPending = reg.status.toLowerCase() == 'pending' ||
        reg.status.toLowerCase() == 'chờ duyệt';

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      elevation: 2,
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
                    fontSize: 16,
                    fontWeight: FontWeight.bold,
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  decoration: BoxDecoration(
                    color: statusColor.withValues(alpha: 0.15),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Text(
                    reg.getStatusText(),
                    style: TextStyle(
                      color: statusColor,
                      fontWeight: FontWeight.bold,
                      fontSize: 12,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            _buildInfoRow(Icons.person_outlined, 'Học viên', reg.studentName ?? '-'),
            _buildInfoRow(Icons.book_outlined, 'Khóa học', reg.courseName ?? '-'),
            if (reg.className != null)
              _buildInfoRow(Icons.class_outlined, 'Lớp', reg.className!),
            _buildInfoRow(
              Icons.calendar_today,
              'Ngày đăng ký',
              DateFormat('dd/MM/yyyy').format(reg.registrationDate),
            ),
            if (reg.notes != null && reg.notes!.isNotEmpty)
              _buildInfoRow(Icons.note_outlined, 'Ghi chú', reg.notes!),
            if (isPending) ...[
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: ElevatedButton.icon(
                      onPressed: () => _rejectRegistration(reg),
                      icon: const Icon(Icons.close_rounded, size: 18),
                      label: const Text('Từ chối'),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: RetroColors.error,
                        foregroundColor: RetroColors.textLight,
                        padding: const EdgeInsets.symmetric(vertical: 10),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(8),
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: ElevatedButton.icon(
                      onPressed: () => _approveRegistration(reg),
                      icon: const Icon(Icons.check_rounded, size: 18),
                      label: const Text('Duyệt'),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: RetroColors.success,
                        foregroundColor: RetroColors.textLight,
                        padding: const EdgeInsets.symmetric(vertical: 10),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(8),
                        ),
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
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 18, color: RetroColors.textSecondary),
          const SizedBox(width: 8),
          Expanded(
            child: RichText(
              text: TextSpan(
                style: TextStyle(fontSize: 14, color: RetroColors.textPrimary),
                children: [
                  TextSpan(
                    text: '$label: ',
                    style: TextStyle(color: RetroColors.textSecondary),
                  ),
                  TextSpan(
                    text: value,
                    style: const TextStyle(fontWeight: FontWeight.w500),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
