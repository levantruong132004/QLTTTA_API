import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/models/dashboard_stats.dart';
import 'package:qlttta_app_mobile/services/dashboard_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';
import 'package:intl/intl.dart';

class ClassesScreen extends StatefulWidget {
  const ClassesScreen({super.key});

  @override
  State<ClassesScreen> createState() => _ClassesScreenState();
}

class _ClassesScreenState extends State<ClassesScreen> {
  final DashboardService _dashboardService = DashboardService();
  late Future<List<Class>> _classesFuture;

  @override
  void initState() {
    super.initState();
    _classesFuture = _dashboardService.getClasses();
  }

  @override
  Widget build(BuildContext context) {
    final dateFormat = DateFormat('dd/MM/yyyy');

    return Scaffold(
      backgroundColor: RetroColors.background,
      appBar: AppBar(
        title: const Text('DANH SÁCH LỚP HỌC'),
        backgroundColor: RetroColors.warning,
      ),
      body: FutureBuilder<List<Class>>(
        future: _classesFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(
              child: CircularProgressIndicator(color: RetroColors.primary),
            );
          } else if (snapshot.hasError) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.error_outline,
                        size: 48, color: RetroColors.error),
                    const SizedBox(height: 16),
                    Text(
                      'Lỗi: ${snapshot.error}',
                      style: const TextStyle(fontSize: 16),
                      textAlign: TextAlign.center,
                    ),
                  ],
                ),
              ),
            );
          } else if (!snapshot.hasData || snapshot.data!.isEmpty) {
            return Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.class_rounded,
                      size: 64, color: RetroColors.textHint),
                  const SizedBox(height: 16),
                  const Text(
                    'Chưa có lớp học nào',
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                  ),
                ],
              ),
            );
          }

          final classes = snapshot.data!;
          return ListView.builder(
            padding: const EdgeInsets.all(16),
            itemCount: classes.length,
            itemBuilder: (context, index) {
              final classItem = classes[index];
              return Container(
                margin: const EdgeInsets.only(bottom: 12),
                decoration: BoxDecoration(
                  color: RetroColors.surface,
                  borderRadius: BorderRadius.circular(12),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withValues(alpha: 0.06),
                      blurRadius: 6,
                      offset: const Offset(0, 2),
                    ),
                  ],
                ),
                child: ListTile(
                  contentPadding:
                      const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                  leading: Container(
                    width: 50,
                    height: 50,
                    decoration: BoxDecoration(
                      color: RetroColors.warning.withValues(alpha: 0.15),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: const Icon(
                      Icons.class_rounded,
                      color: RetroColors.warning,
                      size: 28,
                    ),
                  ),
                  title: Text(
                    classItem.className,
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.bold,
                      color: RetroColors.textPrimary,
                    ),
                  ),
                  subtitle: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const SizedBox(height: 4),
                      Text(
                        'Mã: ${classItem.classCode}',
                        style: const TextStyle(
                          fontSize: 12,
                          color: RetroColors.textSecondary,
                        ),
                      ),
                      if (classItem.courseName != null) ...[
                        const SizedBox(height: 2),
                        Text(
                          'Khóa: ${classItem.courseName}',
                          style: const TextStyle(
                            fontSize: 12,
                            color: RetroColors.textSecondary,
                          ),
                        ),
                      ],
                      if (classItem.startDate != null) ...[
                        const SizedBox(height: 2),
                        Text(
                          'Từ ${dateFormat.format(classItem.startDate!)}',
                          style: const TextStyle(
                            fontSize: 12,
                            color: RetroColors.textSecondary,
                          ),
                        ),
                      ],
                    ],
                  ),
                  trailing: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 8, vertical: 4),
                        decoration: BoxDecoration(
                          color: _getStatusColor(classItem.status)
                              .withValues(alpha: 0.15),
                          borderRadius: BorderRadius.circular(6),
                        ),
                        child: Text(
                          classItem.status ?? 'N/A',
                          style: TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.bold,
                            color: _getStatusColor(classItem.status),
                          ),
                        ),
                      ),
                    ],
                  ),
                  onTap: () {
                    // Navigate to class detail if needed
                  },
                ),
              );
            },
          );
        },
      ),
    );
  }

  Color _getStatusColor(String? status) {
    if (status == null) return RetroColors.textHint;
    switch (status.toLowerCase()) {
      case 'đang mở':
        return RetroColors.success;
      case 'đang học':
        return RetroColors.info;
      case 'đã kết thúc':
        return RetroColors.textSecondary;
      default:
        return RetroColors.textHint;
    }
  }
}
