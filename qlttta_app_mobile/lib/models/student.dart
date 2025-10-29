class Student {
  final int studentId;
  final String maHocVien;
  final String hoTen;
  final String? soDienThoai;
  final DateTime ngaySinh;
  final String? diaChi;

  Student({
    required this.studentId,
    required this.maHocVien,
    required this.hoTen,
    this.soDienThoai,
    required this.ngaySinh,
    this.diaChi,
  });

  factory Student.fromJson(Map<String, dynamic> json) {
    return Student(
      studentId: json['studentId'],
      maHocVien: json['studentCode'] ?? '',
      hoTen: json['fullName'] ?? '',
      soDienThoai: json['phoneNumber'],
      ngaySinh: DateTime.parse(json['dateOfBirth']),
      diaChi: json['address'],
    );
  }
}
