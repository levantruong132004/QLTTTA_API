class OpenClassItem {
  final int classId;
  final String classCode;
  final String className;
  final int maxSize;
  final String courseName;
  final String? scheduleText;

  OpenClassItem({
    required this.classId,
    required this.classCode,
    required this.className,
    required this.maxSize,
    required this.courseName,
    this.scheduleText,
  });

  factory OpenClassItem.fromJson(Map<String, dynamic> json) {
    return OpenClassItem(
      classId: json['classId'] ?? json['ClassId'] ?? 0,
      classCode: json['classCode'] ?? json['ClassCode'] ?? '',
      className: json['className'] ?? json['ClassName'] ?? '',
      maxSize: json['maxSize'] ?? json['MaxSize'] ?? 0,
      courseName: json['courseName'] ?? json['CourseName'] ?? '',
      scheduleText: json['scheduleText'] ?? json['ScheduleText'],
    );
  }
}
