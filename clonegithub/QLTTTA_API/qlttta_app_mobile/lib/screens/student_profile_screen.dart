import 'package:flutter/material.dart';
import 'package:qlttta_app_mobile/models/student.dart';
import 'package:qlttta_app_mobile/services/student_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';
import 'package:intl/intl.dart';

class StudentProfileScreen extends StatefulWidget {
  final int studentId;
  const StudentProfileScreen({super.key, required this.studentId});

  @override
  State<StudentProfileScreen> createState() => _StudentProfileScreenState();
}

class _StudentProfileScreenState extends State<StudentProfileScreen> {
  final StudentService _studentService = StudentService();
  Future<Student?>? _studentFuture;

  @override
  void initState() {
    super.initState();
    _studentFuture = _studentService.getStudentProfile(widget.studentId);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: RetroColors.vintageCream,
      appBar: AppBar(
        title: const Text('📘 CHI TIẾT HỌC VIÊN'),
      ),
      body: Container(
        decoration: const BoxDecoration(
          image: DecorationImage(
            image: NetworkImage(
                'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNDAiIGhlaWdodD0iNDAiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+PGRlZnM+PHBhdHRlcm4gaWQ9InBhdHRlcm4iIHBhdHRlcm5Vbml0cz0idXNlclNwYWNlT25Vc2UiIHdpZHRoPSI0MCIgaGVpZ2h0PSI0MCI+PHJlY3Qgd2lkdGg9IjQwIiBoZWlnaHQ9IjQwIiBmaWxsPSIjZjRlYWQ1Ii8+PHBhdGggZD0iTTAgMGgyMHYyMEgweiIgZmlsbD0iI2U4ZGNjNCIgb3BhY2l0eT0iMC4zIi8+PHBhdGggZD0iTTIwIDIwaDIwdjIwSDIweiIgZmlsbD0iI2U4ZGNjNCIgb3BhY2l0eT0iMC4zIi8+PC9wYXR0ZXJuPjwvZGVmcz48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSJ1cmwoI3BhdHRlcm4pIi8+PC9zdmc+'),
            repeat: ImageRepeat.repeat,
            opacity: 0.3,
          ),
        ),
        child: FutureBuilder<Student?>(
          future: _studentFuture,
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
                        'Error: ${snapshot.error}',
                        style: const TextStyle(fontSize: 16),
                        textAlign: TextAlign.center,
                      ),
                    ],
                  ),
                ),
              );
            } else if (!snapshot.hasData || snapshot.data == null) {
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
                      Icon(Icons.search_off,
                          size: 48, color: RetroColors.vintageGray),
                      SizedBox(height: 16),
                      Text(
                        'Profile not found.',
                        style: TextStyle(
                            fontSize: 18, fontWeight: FontWeight.bold),
                      ),
                    ],
                  ),
                ),
              );
            }

            final student = snapshot.data!;

            return SingleChildScrollView(
              padding: const EdgeInsets.all(16),
              child: Container(
                decoration: BoxDecoration(
                  color: RetroColors.vintageWhite,
                  border: Border.all(color: RetroColors.vintageBrown, width: 4),
                  boxShadow: [
                    BoxShadow(
                      color: RetroColors.vintageDarkBrown.withOpacity(0.3),
                      offset: const Offset(8, 8),
                      blurRadius: 0,
                    ),
                  ],
                ),
                child: Column(
                  children: [
                    // Header
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(16),
                      decoration: BoxDecoration(
                        gradient: LinearGradient(
                          colors: [
                            RetroColors.vintageBrown,
                            RetroColors.vintageDarkBrown
                          ],
                          begin: Alignment.topCenter,
                          end: Alignment.bottomCenter,
                        ),
                        border: const Border(
                          bottom: BorderSide(
                              color: RetroColors.vintageGold, width: 3),
                        ),
                      ),
                      child: Column(
                        children: [
                          Container(
                            width: 80,
                            height: 80,
                            decoration: BoxDecoration(
                              color: RetroColors.vintageCream,
                              border: Border.all(
                                  color: RetroColors.vintageGold, width: 3),
                            ),
                            child: Center(
                              child: Text(
                                student.hoTen.isNotEmpty
                                    ? student.hoTen[0].toUpperCase()
                                    : '?',
                                style: const TextStyle(
                                  fontSize: 40,
                                  fontWeight: FontWeight.bold,
                                  color: RetroColors.vintageBrown,
                                ),
                              ),
                            ),
                          ),
                          const SizedBox(height: 12),
                          Text(
                            student.hoTen,
                            style: const TextStyle(
                              fontSize: 24,
                              fontWeight: FontWeight.bold,
                              color: RetroColors.vintageCream,
                            ),
                            textAlign: TextAlign.center,
                          ),
                          const SizedBox(height: 8),
                          Container(
                            padding: const EdgeInsets.symmetric(
                                horizontal: 12, vertical: 6),
                            decoration: BoxDecoration(
                              color: RetroColors.vintageBrown,
                              border: Border.all(
                                  color: RetroColors.vintageGold, width: 2),
                            ),
                            child: Text(
                              'Mã: ${student.maHocVien}',
                              style: const TextStyle(
                                fontSize: 14,
                                fontWeight: FontWeight.bold,
                                color: RetroColors.vintageCream,
                                letterSpacing: 2,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                    // Body
                    Container(
                      padding: const EdgeInsets.all(16),
                      decoration: const BoxDecoration(
                        color: RetroColors.vintageOffWhite,
                      ),
                      child: Column(
                        children: [
                          _buildProfileItem(
                              Icons.person, 'Mã HỌC VIÊN', student.maHocVien),
                          _buildProfileItem(
                              Icons.badge, 'HỌ TÊN', student.hoTen),
                          _buildProfileItem(Icons.phone, 'SỐ ĐIỆN THOẠI',
                              student.soDienThoai ?? 'Không có'),
                          _buildProfileItem(
                              Icons.cake,
                              'NGÀY SINH',
                              DateFormat('dd/MM/yyyy')
                                  .format(student.ngaySinh)),
                          _buildProfileItem(Icons.home, 'ĐỊA CHỈ',
                              student.diaChi ?? 'Không có'),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            );
          },
        ),
      ),
    );
  }

  Widget _buildProfileItem(IconData icon, String label, String value) {
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      decoration: BoxDecoration(
        color: RetroColors.vintageWhite,
        border: Border.all(color: RetroColors.vintageBrown, width: 2),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 50,
            height: 50,
            decoration: const BoxDecoration(
              color: RetroColors.vintageBrown,
              border: Border(
                right:
                    BorderSide(color: RetroColors.vintageDarkBrown, width: 2),
              ),
            ),
            child: Icon(
              icon,
              color: RetroColors.vintageCream,
              size: 24,
            ),
          ),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    label,
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.bold,
                      color: RetroColors.vintageDarkBrown,
                      letterSpacing: 1,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    value,
                    style: const TextStyle(
                      fontSize: 16,
                      color: RetroColors.vintageDarkBrown,
                    ),
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
