class Student {
  final int studentId;
  final String maHocVien;
  final String hoTen;
  final String? soDienThoai;
  final DateTime? ngaySinh; // có thể null từ API
  final String? diaChi;

  Student({
    required this.studentId,
    required this.maHocVien,
    required this.hoTen,
    this.soDienThoai,
    this.ngaySinh,
    this.diaChi,
  });

  factory Student.fromJson(Map<String, dynamic> json) {
    DateTime? dob;
    final rawDob = json['dateOfBirth'];
    if (rawDob is String && rawDob.isNotEmpty) {
      try { dob = DateTime.parse(rawDob); } catch (_) { dob = null; }
    }
    return Student(
      studentId: json['studentId'],
      maHocVien: json['studentCode'] ?? '',
      hoTen: json['fullName'] ?? '',
      soDienThoai: json['phoneNumber'],
      ngaySinh: dob,
      diaChi: json['address'],
    );
  }
}
