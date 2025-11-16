class DashboardStats {
  final int totalCourses;
  final int totalStudents;
  final int totalClasses;
  final int totalRegistrations;
  final int totalInvoices; // For accountant
  final int totalPendingPayments; // For accountant
  final int myRegistrations; // For student
  final int myInvoices; // For student

  DashboardStats({
    required this.totalCourses,
    required this.totalStudents,
    required this.totalClasses,
    required this.totalRegistrations,
    this.totalInvoices = 0,
    this.totalPendingPayments = 0,
    this.myRegistrations = 0,
    this.myInvoices = 0,
  });

  factory DashboardStats.fromJson(Map<String, dynamic> json) {
    return DashboardStats(
      totalCourses: json['totalCourses'] ?? 0,
      totalStudents: json['totalStudents'] ?? 0,
      totalClasses: json['totalClasses'] ?? 0,
      totalRegistrations: json['totalRegistrations'] ?? 0,
      totalInvoices: json['totalInvoices'] ?? 0,
      totalPendingPayments: json['totalPendingPayments'] ?? 0,
      myRegistrations: json['myRegistrations'] ?? 0,
      myInvoices: json['myInvoices'] ?? 0,
    );
  }

  // Default empty stats
  factory DashboardStats.empty() {
    return DashboardStats(
      totalCourses: 0,
      totalStudents: 0,
      totalClasses: 0,
      totalRegistrations: 0,
      totalInvoices: 0,
      totalPendingPayments: 0,
      myRegistrations: 0,
      myInvoices: 0,
    );
  }
}

class Course {
  final int courseId;
  final String courseCode;
  final String courseName;
  final String? description;
  final int standardFee;

  Course({
    required this.courseId,
    required this.courseCode,
    required this.courseName,
    this.description,
    required this.standardFee,
  });

  factory Course.fromJson(Map<String, dynamic> json) {
    return Course(
      courseId: json['courseId'] ?? 0,
      courseCode: json['courseCode'] ?? '',
      courseName: json['courseName'] ?? '',
      description: json['description'],
      standardFee: json['standardFee'] ?? 0,
    );
  }
}

class Class {
  final int classId;
  final String classCode;
  final String className;
  final String? courseName;
  final DateTime? startDate;
  final DateTime? endDate;
  final int maxSize;
  final String? status;

  Class({
    required this.classId,
    required this.classCode,
    required this.className,
    this.courseName,
    this.startDate,
    this.endDate,
    required this.maxSize,
    this.status,
  });

  factory Class.fromJson(Map<String, dynamic> json) {
    return Class(
      classId: json['classId'] ?? 0,
      classCode: json['classCode'] ?? '',
      className: json['className'] ?? '',
      courseName: json['courseName'],
      startDate: json['startDate'] != null
          ? DateTime.parse(json['startDate'])
          : null,
      endDate:
          json['endDate'] != null ? DateTime.parse(json['endDate']) : null,
      maxSize: json['maxSize'] ?? 0,
      status: json['status'],
    );
  }
}
