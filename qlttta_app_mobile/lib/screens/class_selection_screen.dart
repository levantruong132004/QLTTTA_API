import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/models/open_class.dart';
import 'package:qlttta_app_mobile/services/registration_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';

class ClassSelectionScreen extends StatefulWidget {
  final String courseCode;
  final String courseName;

  const ClassSelectionScreen({super.key, required this.courseCode, required this.courseName});

  @override
  State<ClassSelectionScreen> createState() => _ClassSelectionScreenState();
}

class _ClassSelectionScreenState extends State<ClassSelectionScreen> {
  final RegistrationService _registrationService = RegistrationService();
  late Future<List<OpenClassItem>> _futureClasses;
  bool _registering = false;

  @override
  void initState() {
    super.initState();
    _futureClasses = _registrationService.getOpenClasses(widget.courseCode);
  }

  Future<void> _registerClass(OpenClassItem item) async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Xác nhận đăng ký lớp'),
        content: Text('Đăng ký vào lớp: ${item.className} (Mã: ${item.classCode})?'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Hủy')),
          ElevatedButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: ElevatedButton.styleFrom(backgroundColor: RetroColors.primary, foregroundColor: Colors.white),
            child: const Text('Đăng ký'),
          ),
        ],
      ),
    );
    if (confirm != true) return;

    setState(() => _registering = true);
    final result = await _registrationService.registerClass(item.classId);
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(result['message'] ?? ''),
        backgroundColor: result['success'] == true ? RetroColors.success : RetroColors.error,
      ),
    );
    setState(() => _registering = false);
    if (result['success'] == true) {
      // Pop twice: selection screen -> course detail
      await Future.delayed(const Duration(milliseconds: 400));
      if (mounted) Navigator.of(context).pop(true);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: RetroColors.background,
      appBar: AppBar(
        title: Text('CHỌN LỚP - ${widget.courseCode}'),
        backgroundColor: RetroColors.info,
      ),
      body: FutureBuilder<List<OpenClassItem>>(
        future: _futureClasses,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return Center(child: Text('Lỗi tải danh sách lớp: ${snapshot.error}'));
          }
          final classes = snapshot.data ?? [];
          if (classes.isEmpty) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(24.0),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Icon(Icons.info_outline, size: 48, color: RetroColors.warning),
                    const SizedBox(height: 16),
                    Text(
                      'Hiện chưa có lớp mở cho khóa "${widget.courseName}". Bạn có thể gửi đăng ký khóa để chờ mở lớp.',
                      textAlign: TextAlign.center,
                      style: const TextStyle(color: RetroColors.textSecondary),
                    ),
                  ],
                ),
              ),
            );
          }
          return ListView.separated(
            padding: const EdgeInsets.all(12),
            itemCount: classes.length,
            separatorBuilder: (_, __) => const SizedBox(height: 12),
            itemBuilder: (context, idx) {
              final item = classes[idx];
              return Container(
                decoration: BoxDecoration(
                  color: RetroColors.surface,
                  borderRadius: BorderRadius.circular(12),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withValues(alpha: 0.05),
                      blurRadius: 6,
                      offset: const Offset(0, 2),
                    ),
                  ],
                ),
                child: ListTile(
                  title: Text(item.className, style: const TextStyle(fontWeight: FontWeight.bold)),
                  subtitle: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const SizedBox(height: 4),
                      Text('Mã lớp: ${item.classCode}'),
                      if (item.scheduleText != null && item.scheduleText!.isNotEmpty)
                        Text('Lịch: ${item.scheduleText}', maxLines: 2, overflow: TextOverflow.ellipsis),
                      Text('Sĩ số tối đa: ${item.maxSize}')
                    ],
                  ),
                  trailing: _registering
                      ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                      : ElevatedButton(
                          onPressed: _registering ? null : () => _registerClass(item),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: RetroColors.primary,
                            foregroundColor: Colors.white,
                          ),
                          child: const Text('Đăng ký'),
                        ),
                ),
              );
            },
          );
        },
      ),
    );
  }
}