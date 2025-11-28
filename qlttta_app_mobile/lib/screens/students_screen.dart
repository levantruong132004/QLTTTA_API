import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/models/student.dart';
import 'package:qlttta_app_mobile/screens/student_profile_screen.dart';
import 'package:qlttta_app_mobile/services/student_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';

class StudentsScreen extends StatefulWidget {
  const StudentsScreen({super.key});

  @override
  State<StudentsScreen> createState() => _StudentsScreenState();
}

class _StudentsScreenState extends State<StudentsScreen> {
  final StudentService _studentService = StudentService();
  late Future<void> _loadFuture;
  final List<Student> _students = [];
  int _totalStudents = 0;
  int _pageNumber = 1;
  final int _pageSize = 50;
  // _isLoadingMore no longer needed; removed.
  bool _hasMore = true;
  String _search = '';

  @override
  void initState() {
    super.initState();
    _loadFuture = _initialLoad();
  }

  Future<void> _initialLoad() async {
    try {
      // Ưu tiên endpoint staff/students để đồng bộ như web
      final staffList = await _studentService.getStaffStudents();
      setState(() {
        _students.clear();
        _students.addAll(staffList);
        _totalStudents = staffList.length;
        _hasMore = false; // đã tải hết
      });
    } catch (_) {
      // Fallback: dùng endpoint Students phân trang
      _totalStudents = await _studentService.getStudentsCount();
      await _loadPage(reset: true);
    }
  }

  Future<void> _loadPage({bool reset = false}) async {
    if (reset) {
      _students.clear();
      _pageNumber = 1;
      _hasMore = true;
    }
    if (!_hasMore) return;
    final (list, total) = await _studentService.getStudents(pageNumber: _pageNumber, pageSize: _pageSize, search: _search.isEmpty ? null : _search);
    if (reset && _totalStudents == 0) _totalStudents = total; // fallback nếu count endpoint lỗi
    setState(() {
      _students.addAll(list);
      _pageNumber++;
      _hasMore = _students.length < (_totalStudents == 0 ? total : _totalStudents);
    });
  }

  Future<void> _onRefresh() async {
    _totalStudents = await _studentService.getStudentsCount();
    await _loadPage(reset: true);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        elevation: 0,
        backgroundColor: Colors.transparent,
        title: Row(
          children: [
            Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: Colors.white.withOpacity(0.2),
                borderRadius: BorderRadius.circular(12),
              ),
              child: const Icon(Icons.people_rounded, size: 24),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Text(
                    'Danh sách học viên',
                    style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
                  ),
                  Text(
                    _totalStudents > 0 ? '$_totalStudents học viên' : 'Đang tải...',
                    style: const TextStyle(fontSize: 12, fontWeight: FontWeight.normal),
                  ),
                ],
              ),
            ),
          ],
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
        child: FutureBuilder<void>(
          future: _loadFuture,
          builder: (context, snapshot) {
            if (snapshot.connectionState == ConnectionState.waiting) {
              return const Center(
                child: CircularProgressIndicator(
                  color: RetroColors.vintageBrown,
                ),
              );
            } else if (snapshot.hasError) {
              return Center(
                child: Container(
                  margin: const EdgeInsets.all(20),
                  padding: const EdgeInsets.all(20),
                  decoration: BoxDecoration(
                    color: RetroColors.vintageWhite,
                    border: Border.all(
                        color: RetroColors.vintageBurgundy, width: 3),
                  ),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(Icons.error_outline,
                          size: 48, color: RetroColors.vintageBurgundy),
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
            } else if (_students.isEmpty) {
              return Center(
                child: Container(
                  margin: const EdgeInsets.all(20),
                  padding: const EdgeInsets.all(20),
                  decoration: BoxDecoration(
                    color: RetroColors.vintageWhite,
                    border:
                        Border.all(color: RetroColors.vintageBrown, width: 3),
                  ),
                  child: const Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(Icons.school,
                          size: 48, color: RetroColors.vintageGray),
                      SizedBox(height: 16),
                      Text(
                        'Không có học viên nào.',
                        style: TextStyle(
                            fontSize: 18, fontWeight: FontWeight.bold),
                      ),
                    ],
                  ),
                ),
              );
            }

            return RefreshIndicator(
              onRefresh: _onRefresh,
              color: Colors.white,
              child: ListView.builder(
              padding: EdgeInsets.fromLTRB(16, MediaQuery.of(context).padding.top + 80, 16, 16),
              itemCount: _students.length + (_hasMore ? 1 : 0),
              itemBuilder: (context, index) {
                if (index >= _students.length) {
                  _loadPage();
                  return const Padding(
                    padding: EdgeInsets.symmetric(vertical: 24),
                    child: Center(child: CircularProgressIndicator(color: Colors.white)),
                  );
                }
                final student = _students[index];
                return AnimatedOpacity(
                  opacity: 1.0,
                  duration: Duration(milliseconds: 300 + (index % 5) * 50),
                  child: Container(
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
                    child: Material(
                      color: Colors.transparent,
                      child: InkWell(
                        borderRadius: BorderRadius.circular(16),
                        onTap: () {
                          Navigator.of(context).push(
                            MaterialPageRoute(
                              builder: (context) => StudentProfileScreen(
                                  studentId: student.studentId),
                            ),
                          );
                        },
                        child: Padding(
                          padding: const EdgeInsets.all(16),
                          child: Row(
                            children: [
                              Hero(
                                tag: 'student_avatar_${student.studentId}',
                                child: Container(
                                  width: 56,
                                  height: 56,
                                  decoration: BoxDecoration(
                                    gradient: LinearGradient(
                                      begin: Alignment.topLeft,
                                      end: Alignment.bottomRight,
                                      colors: [
                                        const Color(0xFF8B4513),
                                        const Color(0xFFD2691E),
                                      ],
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
                                  child: Center(
                                    child: Text(
                                      student.hoTen.isNotEmpty
                                          ? student.hoTen[0].toUpperCase()
                                          : '?',
                                      style: const TextStyle(
                                        fontSize: 24,
                                        fontWeight: FontWeight.bold,
                                        color: Colors.white,
                                      ),
                                    ),
                                  ),
                                ),
                              ),
                              const SizedBox(width: 16),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      student.hoTen,
                                      style: const TextStyle(
                                        fontSize: 16,
                                        fontWeight: FontWeight.w600,
                                        color: Color(0xFF1a1a1a),
                                      ),
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                    const SizedBox(height: 6),
                                    Container(
                                      padding: const EdgeInsets.symmetric(
                                          horizontal: 10, vertical: 4),
                                      decoration: BoxDecoration(
                                        color: const Color(0xFF8B4513).withOpacity(0.1),
                                        borderRadius: BorderRadius.circular(8),
                                      ),
                                      child: Text(
                                        student.maHocVien,
                                        style: const TextStyle(
                                          fontSize: 12,
                                          fontWeight: FontWeight.w600,
                                          color: Color(0xFF8B4513),
                                          letterSpacing: 0.5,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              Container(
                                padding: const EdgeInsets.all(10),
                                decoration: BoxDecoration(
                                  color: const Color(0xFF8B4513).withOpacity(0.1),
                                  borderRadius: BorderRadius.circular(12),
                                ),
                                child: const Icon(
                                  Icons.arrow_forward_ios_rounded,
                                  color: Color(0xFF8B4513),
                                  size: 18,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                );
              },
            ));
          },
        ),
      ),
    );
  }
}
