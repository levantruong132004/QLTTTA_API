import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/models/dashboard_stats.dart';
import 'package:qlttta_app_mobile/screens/login_screen.dart';
import 'package:qlttta_app_mobile/screens/my_profile_screen.dart';
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
import 'package:mobile_scanner/mobile_scanner.dart';

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
    // Tạm thời load với role mặc định, sẽ refetch sau khi roleId được lấy.
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
      // Sau khi biết role chính xác thì refetch stats để lấy đúng số liệu.
      _statsFuture = _dashboardService.getStats();
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
            Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const MyProfileScreen()),
            );
          },
        ),
        const SizedBox(height: 12),
        _QuickActionTile(
          icon: Icons.qr_code_scanner,
          title: 'Scan QR đăng nhập web',
          subtitle: 'Phê duyệt đăng nhập trên PC',
          color: RetroColors.accent,
          onTap: () {
            Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const QrScanApproveScreen()),
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
    
    // Role 2: GiangVien - QR scan and settings only (student list in dashboard)
    if (_roleId == 2) {
      actions.addAll([
        _QuickActionTile(
          icon: Icons.qr_code_scanner,
          title: 'Scan QR đăng nhập web',
          subtitle: 'Phê duyệt đăng nhập trên PC',
          color: RetroColors.accent,
          onTap: () {
            Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const QrScanApproveScreen()),
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
          icon: Icons.qr_code_scanner,
          title: 'Scan QR đăng nhập web',
          subtitle: 'Phê duyệt đăng nhập trên PC',
          color: RetroColors.accent,
          onTap: () {
            Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const QrScanApproveScreen()),
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
    
    // Role 4 & 5: NhanVienHocVu & Admin - QR scan and settings (student list in dashboard)
    actions.addAll([
      _QuickActionTile(
        icon: Icons.qr_code_scanner,
        title: 'Scan QR đăng nhập web',
        subtitle: 'Phê duyệt đăng nhập trên PC',
        color: RetroColors.accent,
        onTap: () {
          Navigator.of(context).push(
            MaterialPageRoute(builder: (_) => const QrScanApproveScreen()),
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

// ===== QR Scan & Approve Screen =====
class QrScanApproveScreen extends StatefulWidget {
  const QrScanApproveScreen({super.key});
  @override
  State<QrScanApproveScreen> createState() => _QrScanApproveScreenState();
}

class _QrScanApproveScreenState extends State<QrScanApproveScreen> {
  String? _challengeId;
  String _status = 'Đang chờ quét...';
  bool _scanned = false;
  bool _decisionMade = false;
  final AuthService _auth = AuthService();

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
          'Quét QR đăng nhập web',
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
          child: Column(
            children: [
              const SizedBox(height: 60),
              // Scanner frame
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(24),
                    child: Stack(
                      children: [
                        MobileScanner(
                          onDetect: (capture) async {
                            if (_scanned) return;
                            final barcodes = capture.barcodes;
                            for (final bc in barcodes) {
                              final raw = bc.rawValue;
                              if (raw == null) continue;
                              final id = _parseQr(raw);
                              if (id != null) {
                                setState(() { _scanned = true; _challengeId = id; _status = 'Đã quét, gửi lên để xác nhận...'; });
                                await _auth.qrScan(id);
                                if (mounted) {
                                  setState(() { _status = 'Đã quét. Chờ bạn phê duyệt.'; });
                                }
                                break;
                              }
                            }
                          },
                        ),
                        // Scanner overlay
                        if (!_scanned)
                          Container(
                            decoration: BoxDecoration(
                              border: Border.all(color: Colors.white, width: 3),
                              borderRadius: BorderRadius.circular(24),
                            ),
                          ),
                      ],
                    ),
                  ),
                ),
              ),
              // Status and buttons
              Container(
                padding: const EdgeInsets.all(24),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: const BorderRadius.vertical(top: Radius.circular(24)),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withOpacity(0.1),
                      blurRadius: 20,
                      offset: const Offset(0, -5),
                    ),
                  ],
                ),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    // Status indicator
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
                      decoration: BoxDecoration(
                        color: _scanned
                            ? (_decisionMade
                                ? (_status.contains('PHÊ DUYỆT') ? const Color(0xFF4CAF50) : const Color(0xFFF44336))
                                : const Color(0xFFFF9800))
                            : Colors.grey.shade200,
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(
                            _scanned
                                ? (_decisionMade
                                    ? (_status.contains('PHÊ DUYỆT') ? Icons.check_circle_rounded : Icons.cancel_rounded)
                                    : Icons.pending_rounded)
                                : Icons.qr_code_scanner_rounded,
                            color: _scanned && !_decisionMade ? Colors.white : (_scanned ? Colors.white : Colors.grey.shade600),
                            size: 20,
                          ),
                          const SizedBox(width: 8),
                          Text(
                            _status,
                            style: TextStyle(
                              fontWeight: FontWeight.w600,
                              fontSize: 15,
                              color: _scanned && !_decisionMade ? Colors.white : (_scanned ? Colors.white : Colors.grey.shade700),
                            ),
                          ),
                        ],
                      ),
                    ),
                    if (_challengeId != null && !_decisionMade) ...[
                      const SizedBox(height: 20),
                      Row(
                        children: [
                          Expanded(
                            child: SizedBox(
                              height: 56,
                              child: ElevatedButton(
                                onPressed: () async {
                                  if (_challengeId == null) return;
                                  final r = await _auth.qrApprove(_challengeId!, true);
                                  if (mounted) {
                                    setState(() {
                                      _decisionMade = true;
                                      _status = r['success'] == true && r['status'] == 'approved'
                                          ? 'ĐÃ PHÊ DUYỆT'
                                          : 'Phê duyệt thất bại';
                                    });
                                  }
                                },
                                style: ElevatedButton.styleFrom(
                                  backgroundColor: Colors.transparent,
                                  foregroundColor: Colors.white,
                                  padding: EdgeInsets.zero,
                                  shape: RoundedRectangleBorder(
                                    borderRadius: BorderRadius.circular(16),
                                  ),
                                  elevation: 0,
                                  shadowColor: Colors.transparent,
                                ),
                                child: Ink(
                                  decoration: BoxDecoration(
                                    gradient: const LinearGradient(
                                      colors: [Color(0xFF4CAF50), Color(0xFF66BB6A)],
                                      begin: Alignment.centerLeft,
                                      end: Alignment.centerRight,
                                    ),
                                    borderRadius: BorderRadius.circular(16),
                                    boxShadow: [
                                      BoxShadow(
                                        color: const Color(0xFF4CAF50).withOpacity(0.3),
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
                                        Icon(Icons.check_circle_rounded, size: 22),
                                        SizedBox(width: 8),
                                        Text(
                                          'Phê duyệt',
                                          style: TextStyle(
                                            fontSize: 16,
                                            fontWeight: FontWeight.w600,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                ),
                              ),
                            ),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: SizedBox(
                              height: 56,
                              child: ElevatedButton(
                                onPressed: () async {
                                  if (_challengeId == null) return;
                                  await _auth.qrApprove(_challengeId!, false);
                                  if (mounted) {
                                    setState(() {
                                      _decisionMade = true;
                                      _status = 'ĐÃ TỪ CHỐI';
                                    });
                                  }
                                },
                                style: ElevatedButton.styleFrom(
                                  backgroundColor: Colors.transparent,
                                  foregroundColor: Colors.white,
                                  padding: EdgeInsets.zero,
                                  shape: RoundedRectangleBorder(
                                    borderRadius: BorderRadius.circular(16),
                                  ),
                                  elevation: 0,
                                  shadowColor: Colors.transparent,
                                ),
                                child: Ink(
                                  decoration: BoxDecoration(
                                    gradient: const LinearGradient(
                                      colors: [Color(0xFFF44336), Color(0xFFEF5350)],
                                      begin: Alignment.centerLeft,
                                      end: Alignment.centerRight,
                                    ),
                                    borderRadius: BorderRadius.circular(16),
                                    boxShadow: [
                                      BoxShadow(
                                        color: const Color(0xFFF44336).withOpacity(0.3),
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
                                        Icon(Icons.cancel_rounded, size: 22),
                                        SizedBox(width: 8),
                                        Text(
                                          'Từ chối',
                                          style: TextStyle(
                                            fontSize: 16,
                                            fontWeight: FontWeight.w600,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
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
            ],
          ),
        ),
      ),
    );
  }

  String? _parseQr(String raw) {
    try {
      if (raw.startsWith('qlttta://')) {
        final converted = raw.replaceFirst('qlttta://', 'https://');
        final uri = Uri.parse(converted);
        return uri.queryParameters['id'];
      }
      if (raw.contains('qr-login') && raw.contains('id=')) {
        final uri = Uri.parse(raw.startsWith('http') ? raw : 'https://' + raw);
        return uri.queryParameters['id'];
      }
    } catch (_) {}
    return null;
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
          padding: const EdgeInsets.all(14),
          child: Row(
            children: [
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: color.withValues(alpha: 0.15),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Icon(icon, size: 22, color: color),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: const TextStyle(
                        fontSize: 14,
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
