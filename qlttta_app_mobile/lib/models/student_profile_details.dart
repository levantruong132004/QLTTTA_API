class StudentProfileDetails {
  final String fullName;
  final String studentCode;
  final String? gender;
  final DateTime? dateOfBirth;
  final String? phoneNumber;
  final String? address;
  final String? email;

  const StudentProfileDetails({
    required this.fullName,
    required this.studentCode,
    this.gender,
    this.dateOfBirth,
    this.phoneNumber,
    this.address,
    this.email,
  });

  factory StudentProfileDetails.fromJson(Map<String, dynamic> json) {
    DateTime? dob;
    final rawDob = json['ngaySinh'] ?? json['NgaySinh'];
    if (rawDob is String && rawDob.isNotEmpty) {
      dob = DateTime.tryParse(rawDob);
    } else if (rawDob is DateTime) {
      dob = rawDob;
    }

    return StudentProfileDetails(
      fullName: (json['hoTen'] ?? json['HoTen'] ?? '').toString(),
      studentCode: (json['maHocVien'] ?? json['MaHocVien'] ?? '').toString(),
      gender: (json['gioiTinh'] ?? json['GioiTinh'])?.toString(),
      dateOfBirth: dob,
      phoneNumber: (json['soDienThoai'] ?? json['SoDienThoai'])?.toString(),
      address: (json['diaChi'] ?? json['DiaChi'])?.toString(),
      email: (json['email'] ?? json['Email'])?.toString(),
    );
  }
}
