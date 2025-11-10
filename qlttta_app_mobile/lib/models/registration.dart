class Registration {
  final int registrationId;
  final int studentId;
  final String? studentName;
  final int courseId;
  final String? courseName;
  final int? classId;
  final String? className;
  final DateTime registrationDate;
  final String status; // 'pending', 'approved', 'rejected'
  final String? notes;

  Registration({
    required this.registrationId,
    required this.studentId,
    this.studentName,
    required this.courseId,
    this.courseName,
    this.classId,
    this.className,
    required this.registrationDate,
    required this.status,
    this.notes,
  });

  factory Registration.fromJson(Map<String, dynamic> json) {
    return Registration(
      registrationId: json['registrationId'] ?? json['RegistrationId'] ?? 0,
      studentId: json['studentId'] ?? json['StudentId'] ?? 0,
      studentName: json['studentName'] ?? json['StudentName'],
      courseId: json['courseId'] ?? json['CourseId'] ?? 0,
      courseName: json['courseName'] ?? json['CourseName'],
      classId: json['classId'] ?? json['ClassId'],
      className: json['className'] ?? json['ClassName'],
      registrationDate: json['registrationDate'] != null
          ? DateTime.parse(json['registrationDate'])
          : (json['RegistrationDate'] != null
              ? DateTime.parse(json['RegistrationDate'])
              : DateTime.now()),
      status: json['status'] ?? json['Status'] ?? 'pending',
      notes: json['notes'] ?? json['Notes'],
    );
  }

  String getStatusText() {
    switch (status.toLowerCase()) {
      case 'approved':
      case 'đã duyệt':
        return 'Đã duyệt';
      case 'pending':
      case 'chờ duyệt':
        return 'Chờ duyệt';
      case 'rejected':
      case 'từ chối':
        return 'Từ chối';
      default:
        return status;
    }
  }
}
