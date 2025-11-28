import 'dart:convert';
import 'package:qlttta_app_mobile/models/dashboard_stats.dart';
import 'package:qlttta_app_mobile/services/api_service.dart';
import 'package:shared_preferences/shared_preferences.dart';

class DashboardService {
  final ApiService _apiService = ApiService();
  int? _currentRoleId;

  Future<DashboardStats> getStats() async {
    // Get role-specific stats
    final prefs = await SharedPreferences.getInstance();
    final roleId = prefs.getInt('roleId') ?? 1;
    _currentRoleId = roleId;
    
    if (roleId == 1) {
      // Student: get my registrations and invoices count
      return await _getStudentStats();
    } else if (roleId == 3) {
      // Accountant: get pending payments and accountant registrations count
      return await _getAccountantStats();
    } else if (roleId == 4) {
      // Academic staff
      return await _getGeneralStats(forceStaffStudentCount: true);
    } else {
      // Admin, Teacher, Staff: get general stats
      return await _getGeneralStats();
    }
  }

  Future<DashboardStats> _getStudentStats() async {
    try {
      int myRegistrations = 0;
      int myInvoices = 0;
      int totalCourses = 0;
      int totalClasses = 0;
      
      // Get my registrations
      final regResponse = await _apiService.get('Registrations/my');
      if (regResponse.statusCode == 200) {
        final List<dynamic> regData = jsonDecode(regResponse.body);
        myRegistrations = regData.length;
        print('Student has $myRegistrations registrations');
        
        // Count invoices - only call API for approved registrations or check all
        for (var reg in regData) {
          final regId = reg['registrationId'] ?? reg['RegistrationId'] ?? reg['REGISTRATIONID'];
          final status = reg['status'] ?? reg['Status'] ?? '';
          
          print('Registration $regId status: $status');
          
          if (regId != null) {
            try {
              final invResponse = await _apiService.get('Invoices/by-registration/$regId');
              print('Invoice API response for reg $regId: status=${invResponse.statusCode}');
              
              if (invResponse.statusCode == 200) {
                final invBody = jsonDecode(invResponse.body);
                print('Invoice body: ${invResponse.body}');
                
                // Check both lowercase and uppercase
                final success = invBody['success'] ?? invBody['Success'];
                final data = invBody['data'] ?? invBody['Data'];
                
                if (success == true && data != null) {
                  myInvoices++;
                  print('✓ Found invoice #${data['invoiceId'] ?? data['InvoiceId']} for registration $regId');
                } else {
                  print('✗ No invoice data for registration $regId (success=$success, data=$data)');
                }
              } else if (invResponse.statusCode == 404) {
                print('✗ No invoice found (404) for registration $regId');
              } else {
                print('✗ Unexpected status ${invResponse.statusCode} for registration $regId');
              }
            } catch (e) {
              print('Error checking invoice for registration $regId: $e');
            }
          }
        }
        
        print('========================================');
        print('SUMMARY: Total registrations=$myRegistrations, Total invoices=$myInvoices');
        print('========================================');
      }
      
      // Students can also see all courses and classes (to register)
      try {
        final coursesResponse = await _apiService.get('Courses');
        if (coursesResponse.statusCode == 200) {
          final coursesBody = jsonDecode(coursesResponse.body);
          if (coursesBody is List) {
            totalCourses = coursesBody.length;
          } else if (coursesBody is Map && coursesBody['data'] != null) {
            totalCourses = (coursesBody['data'] as List).length;
          }
          print('Student can see $totalCourses courses');
        }
      } catch (e) {
        print('Error fetching courses for student: $e');
      }
      
      try {
        final classesResponse = await _apiService.get('Classes');
        if (classesResponse.statusCode == 200) {
          final classesBody = jsonDecode(classesResponse.body);
          if (classesBody is List) {
            totalClasses = classesBody.length;
          } else if (classesBody is Map && classesBody['data'] != null) {
            totalClasses = (classesBody['data'] as List).length;
          }
          print('Student can see $totalClasses classes');
        }
      } catch (e) {
        print('Error fetching classes for student: $e');
      }
      
      return DashboardStats(
        totalCourses: totalCourses,
        totalStudents: 0,
        totalClasses: totalClasses,
        totalRegistrations: 0,
        myRegistrations: myRegistrations,
        myInvoices: myInvoices,
      );
    } catch (e) {
      print('Error fetching student stats: $e');
      return DashboardStats.empty();
    }
  }

  Future<DashboardStats> _getAccountantStats() async {
    try {
      int totalInvoices = 0;
      int totalPendingPayments = 0;
      
      // Get accountant registrations (these are for creating invoices)
      final regResponse = await _apiService.get('Registrations/accountant');
      if (regResponse.statusCode == 200) {
        final List<dynamic> regData = jsonDecode(regResponse.body);
        // Count how many have invoices
        for (var reg in regData) {
          final regId = reg['registrationId'] ?? reg['RegistrationId'];
          if (regId != null) {
            try {
              final invResponse = await _apiService.get('Invoices/by-registration/$regId');
              if (invResponse.statusCode == 200) {
                final invBody = jsonDecode(invResponse.body);
                if (invBody['success'] == true && invBody['data'] != null) {
                  totalInvoices++;
                }
              }
            } catch (e) {
              // Skip if invoice not found
            }
          }
        }
      }
      
      // Get pending payments count
      final payResponse = await _apiService.get('Payments/pending');
      if (payResponse.statusCode == 200) {
        final payBody = jsonDecode(payResponse.body);
        if (payBody['success'] == true && payBody['data'] != null) {
          final List<dynamic> payData = payBody['data'];
          totalPendingPayments = payData.length;
        }
      }
      
      return DashboardStats(
        totalCourses: 0,
        totalStudents: 0,
        totalClasses: 0,
        totalRegistrations: 0,
        totalInvoices: totalInvoices,
        totalPendingPayments: totalPendingPayments,
      );
    } catch (e) {
      print('Error fetching accountant stats: $e');
      return DashboardStats.empty();
    }
  }

  Future<DashboardStats> _getGeneralStats({bool forceStaffStudentCount = false}) async {
    try {
      int totalCourses = 0;
      int totalStudents = 0;
      int totalClasses = 0;
      int totalRegistrations = 0;
      
      // Courses endpoint returns array directly (not wrapped in {data: ...})
      try {
        final coursesResponse = await _apiService.get('Courses');
        if (coursesResponse.statusCode == 200) {
          final dynamic courseData = jsonDecode(coursesResponse.body);
          if (courseData is List) {
            totalCourses = courseData.length;
          }
        }
      } catch (e) {
        print('Error fetching courses: $e');
      }
      
      // Students count endpoint (chính xác và nhẹ)
      try {
        final countResponse = await _apiService.get('Students/count');
        if (countResponse.statusCode == 200) {
          final body = jsonDecode(countResponse.body);
          final count = body['Count'] ?? body['count'];
          if (count is int) {
            totalStudents = count;
          } else if (count is String) {
            totalStudents = int.tryParse(count) ?? 0;
          }
        } else {
          // Fallback: lấy trang đầu nếu count thất bại
          final studentsResponse = await _apiService.get('Students?pageNumber=1&pageSize=100');
          if (studentsResponse.statusCode == 200) {
            final body = jsonDecode(studentsResponse.body);
            if (body is Map) {
              final dataField = body['data'] ?? body['Data'];
              if (dataField is List) totalStudents = dataField.length; // chỉ trang đầu
              final totalRecords = body['TotalRecords'] ?? body['totalRecords'];
              if (totalRecords is int) totalStudents = totalRecords; // ưu tiên totalRecords nếu có
            } else if (body is List) {
              totalStudents = body.length;
            }
          }
        }
      } catch (e) {
        print('Error fetching students count: $e');
      }
      
      // Classes endpoint
      try {
        final classesResponse = await _apiService.get('Classes');
        if (classesResponse.statusCode == 200) {
          final body = jsonDecode(classesResponse.body);
          if (body is Map && body['data'] != null) {
            final List<dynamic> classData = body['data'];
            totalClasses = classData.length;
          } else if (body is List) {
            totalClasses = body.length;
          }
        }
      } catch (e) {
        print('Error fetching classes: $e');
      }
      
      // Registrations endpoint returns array directly
      try {
        final registrationsResponse = await _apiService.get('Registrations');
        if (registrationsResponse.statusCode == 200) {
          final dynamic regData = jsonDecode(registrationsResponse.body);
          if (regData is List) {
            totalRegistrations = regData.length;
          }
        }
      } catch (e) {
        print('Error fetching registrations: $e');
      }

      if (forceStaffStudentCount || (_currentRoleId == 4 && totalStudents == 0)) {
        final staffCount = await _countStudentsViaStaffEndpoint();
        if (staffCount > 0) {
          totalStudents = staffCount;
        }
      }
      
      return DashboardStats(
        totalCourses: totalCourses,
        totalStudents: totalStudents,
        totalClasses: totalClasses,
        totalRegistrations: totalRegistrations,
      );
    } catch (e) {
      print('Error fetching general stats: $e');
      return DashboardStats.empty();
    }
  }

  Future<int> _countStudentsViaStaffEndpoint() async {
    try {
      final response = await _apiService.get('staff/students');
      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        final data = body is Map ? (body['data'] ?? body['Data']) : null;
        if (data is List) return data.length;
        if (body is List) return body.length;
      }
    } catch (e) {
      print('Error counting staff students: $e');
    }
    return 0;
  }

  Future<List<Course>> getCourses() async {
    try {
      final response = await _apiService.get('Courses');
      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        // Backend returns array directly, not wrapped
        final List<dynamic> courseData;
        if (body is List) {
          courseData = body;
        } else if (body is Map && body['data'] != null) {
          courseData = body['data'];
        } else {
          courseData = [];
        }
        return courseData.map((json) => Course.fromJson(json)).toList();
      }
      return [];
    } catch (e) {
      print('Error fetching courses: $e');
      return [];
    }
  }

  Future<List<Class>> getClasses() async {
    try {
      final response = await _apiService.get('Classes');
      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        // Backend returns array directly, not wrapped
        final List<dynamic> classData;
        if (body is List) {
          classData = body;
        } else if (body is Map && body['data'] != null) {
          classData = body['data'];
        } else {
          classData = [];
        }
        return classData.map((json) => Class.fromJson(json)).toList();
      }
      return [];
    } catch (e) {
      print('Error fetching classes: $e');
      return [];
    }
  }
}
