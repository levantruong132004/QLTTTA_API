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
  late Future<List<Student>> _studentsFuture;

  @override
  void initState() {
    super.initState();
    _studentsFuture = _studentService.getStudents();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: RetroColors.vintageCream,
      appBar: AppBar(
        title: const Text('📋 DANH SÁCH HỌC VIÊN'),
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
        child: FutureBuilder<List<Student>>(
          future: _studentsFuture,
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
            } else if (!snapshot.hasData || snapshot.data!.isEmpty) {
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

            final students = snapshot.data!;
            return ListView.builder(
              padding: const EdgeInsets.all(16),
              itemCount: students.length,
              itemBuilder: (context, index) {
                final student = students[index];
                return Container(
                  margin: const EdgeInsets.only(bottom: 12),
                  decoration: BoxDecoration(
                    color: RetroColors.vintageWhite,
                    border:
                        Border.all(color: RetroColors.vintageBrown, width: 3),
                    boxShadow: [
                      BoxShadow(
                        color: RetroColors.vintageDarkBrown.withOpacity(0.3),
                        offset: const Offset(4, 4),
                        blurRadius: 0,
                      ),
                    ],
                  ),
                  child: ListTile(
                    contentPadding:
                        const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                    leading: Container(
                      width: 50,
                      height: 50,
                      decoration: BoxDecoration(
                        color: RetroColors.vintageBrown,
                        border: Border.all(
                            color: RetroColors.vintageDarkBrown, width: 2),
                      ),
                      child: Center(
                        child: Text(
                          student.hoTen.isNotEmpty
                              ? student.hoTen[0].toUpperCase()
                              : '?',
                          style: const TextStyle(
                            fontSize: 24,
                            fontWeight: FontWeight.bold,
                            color: RetroColors.vintageCream,
                          ),
                        ),
                      ),
                    ),
                    title: Text(
                      student.hoTen,
                      style: const TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.bold,
                        color: RetroColors.vintageDarkBrown,
                      ),
                    ),
                    subtitle: Container(
                      margin: const EdgeInsets.only(top: 4),
                      padding: const EdgeInsets.symmetric(
                          horizontal: 8, vertical: 4),
                      decoration: BoxDecoration(
                        color: RetroColors.vintageBrown,
                        border: Border.all(
                            color: RetroColors.vintageDarkBrown, width: 2),
                      ),
                      child: Text(
                        student.maHocVien,
                        style: const TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.bold,
                          color: RetroColors.vintageCream,
                          letterSpacing: 1,
                        ),
                      ),
                    ),
                    trailing: Container(
                      padding: const EdgeInsets.all(8),
                      decoration: BoxDecoration(
                        color: RetroColors.vintageOlive,
                        border: Border.all(
                            color: RetroColors.vintageDarkGray, width: 2),
                      ),
                      child: const Icon(
                        Icons.arrow_forward,
                        color: RetroColors.vintageWhite,
                        size: 20,
                      ),
                    ),
                    onTap: () {
                      Navigator.of(context).push(
                        MaterialPageRoute(
                          builder: (context) => StudentProfileScreen(
                              studentId: student.studentId),
                        ),
                      );
                    },
                  ),
                );
              },
            );
          },
        ),
      ),
    );
  }
}
