import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/models/dashboard_stats.dart';
import 'package:qlttta_app_mobile/services/registration_service.dart';
import 'package:qlttta_app_mobile/models/open_class.dart';
import 'class_selection_screen.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';
import 'package:intl/intl.dart';

class CourseDetailScreen extends StatefulWidget {
  final Course course;

  const CourseDetailScreen({super.key, required this.course});

  @override
  State<CourseDetailScreen> createState() => _CourseDetailScreenState();
}

class _CourseDetailScreenState extends State<CourseDetailScreen> {
  final RegistrationService _registrationService = RegistrationService();
  // Removed course-level registration (Plan A) -> no need for _isRegistering flag
  List<OpenClassItem> _openClasses = [];
  bool _loadingClasses = true;

  @override
  void initState() {
    super.initState();
    _loadOpenClasses();
  }

  Future<void> _loadOpenClasses() async {
    try {
      final classes = await _registrationService.getOpenClasses(widget.course.courseCode);
      if (!mounted) return;
      setState(() {
        _openClasses = classes;
        _loadingClasses = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() => _loadingClasses = false);
    }
  }

  // Removed legacy course-level registration method as Plan A requires class selection only.

  @override
  Widget build(BuildContext context) {
    final currencyFormat = NumberFormat.currency(locale: 'vi_VN', symbol: '₫');

    return Scaffold(
      backgroundColor: RetroColors.background,
      appBar: AppBar(
        title: const Text('CHI TIẾT KHÓA HỌC'),
        backgroundColor: RetroColors.info,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Course Header Card
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                color: RetroColors.surface,
                borderRadius: BorderRadius.circular(12),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.06),
                    blurRadius: 8,
                    offset: const Offset(0, 2),
                  ),
                ],
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Icon
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: RetroColors.info.withValues(alpha: 0.15),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: const Icon(
                      Icons.menu_book_rounded,
                      color: RetroColors.info,
                      size: 40,
                    ),
                  ),
                  const SizedBox(height: 16),
                  
                  // Course Name
                  Text(
                    widget.course.courseName,
                    style: const TextStyle(
                      fontSize: 24,
                      fontWeight: FontWeight.bold,
                      color: RetroColors.textPrimary,
                    ),
                  ),
                  const SizedBox(height: 8),
                  
                  // Course Code
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                    decoration: BoxDecoration(
                      color: RetroColors.accent.withValues(alpha: 0.15),
                      borderRadius: BorderRadius.circular(6),
                    ),
                    child: Text(
                      'Mã: ${widget.course.courseCode}',
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w600,
                        color: RetroColors.primary,
                      ),
                    ),
                  ),
                ],
              ),
            ),
            
            const SizedBox(height: 20),
            
            // Description Card
            if (widget.course.description != null && widget.course.description!.isNotEmpty)
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(16),
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
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Row(
                      children: [
                        Icon(Icons.description_outlined, 
                          color: RetroColors.textSecondary, size: 20),
                        SizedBox(width: 8),
                        Text(
                          'Mô tả',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                            color: RetroColors.textPrimary,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 12),
                    Text(
                      widget.course.description!,
                      style: const TextStyle(
                        fontSize: 14,
                        height: 1.5,
                        color: RetroColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
            
            const SizedBox(height: 20),
            
            // Fee Card
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: RetroColors.surface,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: RetroColors.success, width: 2),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.06),
                    blurRadius: 6,
                    offset: const Offset(0, 2),
                  ),
                ],
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Row(
                    children: [
                      Icon(Icons.payments_outlined, 
                        color: RetroColors.success, size: 28),
                      SizedBox(width: 12),
                      Text(
                        'Học phí chuẩn',
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w600,
                          color: RetroColors.textPrimary,
                        ),
                      ),
                    ],
                  ),
                  Text(
                    currencyFormat.format(widget.course.standardFee),
                    style: const TextStyle(
                      fontSize: 20,
                      fontWeight: FontWeight.bold,
                      color: RetroColors.success,
                    ),
                  ),
                ],
              ),
            ),
            
            const SizedBox(height: 32),
            
            // Register Button
            Builder(
              builder: (_) {
                if (_loadingClasses) {
                  return const Padding(
                    padding: EdgeInsets.symmetric(vertical: 12),
                    child: SizedBox(
                      height: 24,
                      width: 24,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                  );
                }

                if (_openClasses.isEmpty) {
                  return Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: RetroColors.warning.withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: RetroColors.warning.withValues(alpha: 0.3)),
                    ),
                    child: const Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Icon(Icons.info_outline, color: RetroColors.warning),
                        SizedBox(width: 12),
                        Expanded(
                          child: Text(
                            'Hiện chưa có lớp mở tuyển sinh cho khóa này. Vui lòng quay lại sau hoặc liên hệ nhân viên học vụ.',
                            style: TextStyle(color: RetroColors.textSecondary),
                          ),
                        ),
                      ],
                    ),
                  );
                }

                // There are open classes -> show registration button for class
                return SizedBox(
                  width: double.infinity,
                  height: 50,
                  child: ElevatedButton(
                    onPressed: () async {
                      final result = await Navigator.of(context).push<bool>(
                        MaterialPageRoute(
                          builder: (_) => ClassSelectionScreen(
                            courseCode: widget.course.courseCode,
                            courseName: widget.course.courseName,
                          ),
                        ),
                      );
                      if (result == true && mounted) {
                        ScaffoldMessenger.of(context).showSnackBar(
                          const SnackBar(
                            content: Text('Đã gửi yêu cầu đăng ký lớp'),
                            backgroundColor: RetroColors.success,
                          ),
                        );
                        Navigator.of(context).pop();
                      }
                    },
                    style: ElevatedButton.styleFrom(
                      backgroundColor: RetroColors.info,
                      foregroundColor: Colors.white,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                    ),
                    child: const Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.class_, size: 22),
                        SizedBox(width: 10),
                        Text(
                          'ĐĂNG KÝ LỚP MỞ',
                          style: TextStyle(
                            fontSize: 15,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
            
            const SizedBox(height: 16),
            
            // Info Note
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: RetroColors.warning.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(8),
                border: Border.all(
                  color: RetroColors.warning.withValues(alpha: 0.3),
                ),
              ),
              child: const Row(
                children: [
                  Icon(Icons.info_outline, 
                    color: RetroColors.warning, size: 20),
                  SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      'Đơn đăng ký sẽ được nhân viên học vụ xét duyệt',
                      style: TextStyle(
                        fontSize: 13,
                        color: RetroColors.textSecondary,
                      ),
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
}
