import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/models/dashboard_stats.dart';
import 'package:qlttta_app_mobile/screens/login_screen.dart';
import 'package:qlttta_app_mobile/screens/students_screen.dart';
import 'package:qlttta_app_mobile/screens/courses_screen.dart';
import 'package:qlttta_app_mobile/screens/classes_screen.dart';
import 'package:qlttta_app_mobile/screens/registrations_invoices_screen.dart';
import 'package:qlttta_app_mobile/screens/pending_payments_screen.dart';
import 'package:qlttta_app_mobile/screens/registrations_management_screen.dart';
import 'package:qlttta_app_mobile/services/auth_service.dart';
import 'package:qlttta_app_mobile/services/dashboard_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';
import 'package:shared_preferences/shared_preferences.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  String? _displayName;
  int? _roleId; // 1=HocVien, 2=GiangVien, 3=KeToan, 4=NhanVienHocVu, 5=Admin
  final DashboardService _dashboardService = DashboardService();
  late Future<DashboardStats> _statsFuture;

  @override
  void initState() {
    super.initState();
    _loadDisplayName();
    _loadRoleId();
    _statsFuture = _dashboardService.getStats();
  }

  Future<void> _loadDisplayName() async {
    final prefs = await SharedPreferences.getInstance();
    final fullName = prefs.getString('fullName');
    final username = prefs.getString('username');
    setState(() {
      _displayName = (fullName != null && fullName.isNotEmpty)
          ? fullName
          : (username ?? 'Người dùng');
    });
  }

  Future<void> _loadRoleId() async {
    final prefs = await SharedPreferences.getInstance();
    setState(() {
      _roleId = prefs.getInt('roleId') ?? 1; // Default to HocVien
    });
  }

  Future<void> _logout(BuildContext context) async {
    await AuthService().logout();
    if (!mounted) return;
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (context) => const LoginScreen()),
      (Route<dynamic> route) => false,
    );
  }

  // Build dashboard based on user role
  Widget _buildDashboardForRole(DashboardStats stats) {
    // Role 1: HocVien - Courses, classes, registrations, and invoices
    if (_roleId == 1) {
      return GridView.count(
        crossAxisCount: 2,
        mainAxisSpacing: 12,
        crossAxisSpacing: 12,
        shrinkWrap: true,
        physics: const NeverScrollableScrollPhysics(),
        childAspectRatio: 1.3,
        children: [
          _StatCard(
            icon: Icons.menu_book_rounded,
            title: 'Khóa học',
            value: stats.totalCourses.toString(),
            color: RetroColors.info,
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const CoursesScreen()),
              );
            },
          ),
          _StatCard(
            icon: Icons.class_rounded,
            title: 'Lớp học',
            value: stats.totalClasses.toString(),
            color: RetroColors.warning,
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const ClassesScreen()),
              );
            },
          ),
          _StatCard(
            icon: Icons.assignment_outlined,
            title: 'Đơn ĐK',
            value: stats.myRegistrations.toString(),
            color: RetroColors.accent,
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const RegistrationsInvoicesScreen()),
              );
            },
          ),
          _StatCard(
            icon: Icons.receipt_long_outlined,
            title: 'Hóa đơn',
            value: stats.myInvoices.toString(),
            color: RetroColors.success,
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const RegistrationsInvoicesScreen()),
              );
            },
          ),
        ],
      );
    }
    
    // Role 2: GiangVien - Courses, classes, schedule
    if (_roleId == 2) {
      return GridView.count(
        crossAxisCount: 2,
        mainAxisSpacing: 12,
        crossAxisSpacing: 12,
        shrinkWrap: true,
        physics: const NeverScrollableScrollPhysics(),
        childAspectRatio: 1.3,
        children: [
          _StatCard(
            icon: Icons.menu_book_rounded,
            title: 'Khóa học',
            value: stats.totalCourses.toString(),
            color: RetroColors.info,
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const CoursesScreen()),
              );
            },
          ),
          _StatCard(
            icon: Icons.class_rounded,
            title: 'Lớp học',
            value: stats.totalClasses.toString(),
            color: RetroColors.warning,
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const ClassesScreen()),
              );
            },
          ),
          _StatCard(
            icon: Icons.schedule_rounded,
            title: 'Lịch giảng',
            value: '-',
            color: RetroColors.accent,
            onTap: () {
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(
                  content: Text('Chức năng lịch giảng đang phát triển'),
                  duration: Duration(seconds: 1),
                ),
              );
            },
          ),
        ],
      );
    }
    
    // Role 3: KeToan - Invoice and payment management
    if (_roleId == 3) {
      return GridView.count(
        crossAxisCount: 2,
        mainAxisSpacing: 12,
        crossAxisSpacing: 12,
        shrinkWrap: true,
        physics: const NeverScrollableScrollPhysics(),
        childAspectRatio: 1.3,
        children: [
          _StatCard(
            icon: Icons.receipt_long_rounded,
            title: 'Hóa đơn',
            value: stats.totalInvoices.toString(),
            color: RetroColors.success,
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const RegistrationsManagementScreen()),
              );
            },
          ),
          _StatCard(
            icon: Icons.payment_rounded,
            title: 'Thanh toán',
            value: stats.totalPendingPayments.toString(),
            color: RetroColors.warning,
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const PendingPaymentsScreen()),
              );
            },
          ),
        ],
      );
    }
    
    // Role 4: NhanVienHocVu - Course, student, class, registration management
    if (_roleId == 4) {
      return GridView.count(
        crossAxisCount: 2,
        mainAxisSpacing: 12,
        crossAxisSpacing: 12,
        shrinkWrap: true,
        physics: const NeverScrollableScrollPhysics(),
        childAspectRatio: 1.3,
        children: [
          _StatCard(
            icon: Icons.menu_book_rounded,
            title: 'Khóa học',
            value: stats.totalCourses.toString(),
            color: RetroColors.info,
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const CoursesScreen()),
              );
            },
          ),
          _StatCard(
            icon: Icons.people_rounded,
            title: 'Học viên',
            value: stats.totalStudents.toString(),
            color: RetroColors.success,
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const StudentsScreen()),
              );
            },
          ),
          _StatCard(
            icon: Icons.class_rounded,
            title: 'Lớp học',
            value: stats.totalClasses.toString(),
            color: RetroColors.warning,
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const ClassesScreen()),
              );
            },
          ),
          _StatCard(
            icon: Icons.assignment_turned_in_rounded,
            title: 'Đăng ký',
            value: stats.totalRegistrations.toString(),
            color: RetroColors.accent,
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const RegistrationsManagementScreen()),
              );
            },
          ),
        ],
      );
    }
    
    // Role 5: Admin - Full access
    return GridView.count(
      crossAxisCount: 2,
      mainAxisSpacing: 12,
      crossAxisSpacing: 12,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      childAspectRatio: 1.3,
      children: [
        _StatCard(
          icon: Icons.menu_book_rounded,
          title: 'Khóa học',
          value: stats.totalCourses.toString(),
          color: RetroColors.info,
          onTap: () {
            Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const CoursesScreen()),
            );
          },
        ),
        _StatCard(
          icon: Icons.people_rounded,
          title: 'Học viên',
          value: stats.totalStudents.toString(),
          color: RetroColors.success,
          onTap: () {
            Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const StudentsScreen()),
            );
          },
        ),
        _StatCard(
          icon: Icons.class_rounded,
          title: 'Lớp học',
          value: stats.totalClasses.toString(),
          color: RetroColors.warning,
          onTap: () {
            Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const ClassesScreen()),
            );
          },
        ),
        _StatCard(
          icon: Icons.assignment_turned_in_rounded,
          title: 'Đăng ký',
          value: stats.totalRegistrations.toString(),
          color: RetroColors.accent,
          onTap: () {
            Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const RegistrationsManagementScreen()),
            );
          },
        ),
        _StatCard(
          icon: Icons.admin_panel_settings_rounded,
          title: 'Quản lý',
          value: '-',
          color: RetroColors.error,
          onTap: () {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                content: Text('Quản lý nhân viên & hệ thống'),
                duration: Duration(seconds: 1),
              ),
            );
          },
        ),
        _StatCard(
          icon: Icons.receipt_long_rounded,
          title: 'Hóa đơn',
          value: '-',
          color: RetroColors.primary,
          onTap: () {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                content: Text('Quản lý hóa đơn'),
                duration: Duration(seconds: 1),
              ),
            );
          },
        ),
      ],
    );
  }

  // Build quick actions based on user role
  List<Widget> _buildQuickActionsForRole() {
    List<Widget> actions = [];
    
    // Role 1: HocVien - Profile & settings only
    if (_roleId == 1) {
      actions.addAll([
        _QuickActionTile(
          icon: Icons.person_rounded,
          title: 'Hồ sơ của tôi',
          subtitle: 'Xem thông tin cá nhân',
          color: RetroColors.info,
          onTap: () {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                content: Text('Chức năng hồ sơ đang phát triển'),
                duration: Duration(seconds: 1),
              ),
            );
          },
        ),
        const SizedBox(height: 12),
        _QuickActionTile(
          icon: Icons.settings_rounded,
          title: 'Cài đặt',
          subtitle: 'Quản lý tài khoản',
          color: RetroColors.textSecondary,
          onTap: () {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                content: Text('Đang phát triển...'),
                duration: Duration(seconds: 1),
              ),
            );
          },
        ),
      ]);
      return actions;
    }
    
    // Role 2: GiangVien - Student list, schedule
    if (_roleId == 2) {
      actions.addAll([
        _QuickActionTile(
          icon: Icons.people_alt_rounded,
          title: 'Danh sách học viên',
          subtitle: 'Xem tất cả học viên',
          color: RetroColors.success,
          onTap: () {
            Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const StudentsScreen()),
            );
          },
        ),
        const SizedBox(height: 12),
        _QuickActionTile(
          icon: Icons.settings_rounded,
          title: 'Cài đặt',
          subtitle: 'Quản lý tài khoản',
          color: RetroColors.textSecondary,
          onTap: () {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                content: Text('Đang phát triển...'),
                duration: Duration(seconds: 1),
              ),
            );
          },
        ),
      ]);
      return actions;
    }
    
    // Role 3: KeToan - Only settings (invoices are in dashboard)
    if (_roleId == 3) {
      actions.addAll([
        _QuickActionTile(
          icon: Icons.settings_rounded,
          title: 'Cài đặt',
          subtitle: 'Quản lý tài khoản',
          color: RetroColors.textSecondary,
          onTap: () {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                content: Text('Đang phát triển...'),
                duration: Duration(seconds: 1),
              ),
            );
          },
        ),
      ]);
      return actions;
    }
    
    // Role 4 & 5: NhanVienHocVu & Admin - Full student access
    actions.addAll([
      _QuickActionTile(
        icon: Icons.people_alt_rounded,
        title: 'Danh sách học viên',
        subtitle: 'Xem tất cả học viên',
        color: RetroColors.success,
        onTap: () {
          Navigator.of(context).push(
            MaterialPageRoute(builder: (_) => const StudentsScreen()),
          );
        },
      ),
      const SizedBox(height: 12),
      _QuickActionTile(
        icon: Icons.settings_rounded,
        title: 'Cài đặt',
        subtitle: 'Quản lý tài khoản',
        color: RetroColors.textSecondary,
        onTap: () {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Đang phát triển...'),
              duration: Duration(seconds: 1),
            ),
          );
        },
      ),
    ]);
    
    return actions;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: RetroColors.background,
      appBar: AppBar(
        elevation: 0,
        backgroundColor: RetroColors.primary,
        title: Row(
          children: [
            const Icon(Icons.dashboard_rounded, size: 24),
            const SizedBox(width: 8),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    _displayName != null
                        ? 'Xin chào, ${_displayName!}'
                        : 'Đang tải...',
                    style: const TextStyle(
                        fontSize: 14, fontWeight: FontWeight.normal),
                    overflow: TextOverflow.ellipsis,
                  ),
                  const Text(
                    'TRANG CHỦ',
                    style: TextStyle(fontSize: 12, letterSpacing: 1.5),
                  ),
                ],
              ),
            ),
          ],
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout_rounded),
            onPressed: () => _logout(context),
            tooltip: 'Đăng xuất',
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          setState(() {
            _statsFuture = _dashboardService.getStats();
          });
        },
        child: SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // Header gradient
              Container(
                height: 120,
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                    colors: [
                      RetroColors.primary,
                      RetroColors.background,
                    ],
                  ),
                ),
                child: Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(Icons.school_rounded,
                          size: 48, color: RetroColors.textLight),
                      const SizedBox(height: 8),
                      Text(
                        _displayName != null
                            ? 'Chào mừng trở lại!'
                            : 'Đang tải...',
                        style: const TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                          color: RetroColors.textLight,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              // Stats cards
              FutureBuilder<DashboardStats>(
                future: _statsFuture,
                builder: (context, snapshot) {
                  if (snapshot.connectionState == ConnectionState.waiting) {
                    return const Padding(
                      padding: EdgeInsets.all(32.0),
                      child: Center(
                        child:
                            CircularProgressIndicator(color: RetroColors.primary),
                      ),
                    );
                  }
                  final stats =
                      snapshot.data ?? DashboardStats.empty();
                  return Padding(
                    padding: const EdgeInsets.all(16.0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text(
                          'TỔNG QUAN',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                            letterSpacing: 1.2,
                            color: RetroColors.textPrimary,
                          ),
                        ),
                        const SizedBox(height: 16),
                        _buildDashboardForRole(stats),
                        const SizedBox(height: 24),
                        const Text(
                          'THAO TÁC NHANH',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                            letterSpacing: 1.2,
                            color: RetroColors.textPrimary,
                          ),
                        ),
                        const SizedBox(height: 16),
                        // Quick actions based on role
                        ..._buildQuickActionsForRole(),
                      ],
                    ),
                  );
                },
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// Stat card widget for dashboard metrics
class _StatCard extends StatelessWidget {
  final IconData icon;
  final String title;
  final String value;
  final Color color;
  final VoidCallback onTap;

  const _StatCard({
    required this.icon,
    required this.title,
    required this.value,
    required this.color,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Container(
          decoration: BoxDecoration(
            color: RetroColors.surface,
            borderRadius: BorderRadius.circular(16),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.08),
                blurRadius: 8,
                offset: const Offset(0, 2),
              ),
            ],
          ),
          padding: const EdgeInsets.all(16),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: color.withValues(alpha: 0.15),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Icon(icon, size: 28, color: color),
              ),
              const Spacer(),
              Text(
                value,
                style: TextStyle(
                  fontSize: 28,
                  fontWeight: FontWeight.bold,
                  color: color,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                title.toUpperCase(),
                style: const TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w600,
                  letterSpacing: 0.5,
                  color: RetroColors.textSecondary,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// Quick action tile for common actions
class _QuickActionTile extends StatelessWidget {
  final IconData icon;
  final String title;
  final String subtitle;
  final Color color;
  final VoidCallback onTap;

  const _QuickActionTile({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.color,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Container(
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
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: color.withValues(alpha: 0.15),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Icon(icon, size: 24, color: color),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.bold,
                        color: RetroColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      subtitle,
                      style: const TextStyle(
                        fontSize: 12,
                        color: RetroColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              const Icon(
                Icons.arrow_forward_ios_rounded,
                size: 16,
                color: RetroColors.textHint,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
