class Schedule {
  final int scheduleId;
  final int classId;
  final String? className;
  final String? courseName;
  final String dayOfWeek; // 'Monday', 'Tuesday', etc.
  final String startTime; // HH:mm format
  final String endTime;
  final String? room;

  Schedule({
    required this.scheduleId,
    required this.classId,
    this.className,
    this.courseName,
    required this.dayOfWeek,
    required this.startTime,
    required this.endTime,
    this.room,
  });

  factory Schedule.fromJson(Map<String, dynamic> json) {
    return Schedule(
      scheduleId: json['scheduleId'] ?? json['ScheduleId'] ?? 0,
      classId: json['classId'] ?? json['ClassId'] ?? 0,
      className: json['className'] ?? json['ClassName'],
      courseName: json['courseName'] ?? json['CourseName'],
      dayOfWeek: json['dayOfWeek'] ?? json['DayOfWeek'] ?? '',
      startTime: json['startTime'] ?? json['StartTime'] ?? '',
      endTime: json['endTime'] ?? json['EndTime'] ?? '',
      room: json['room'] ?? json['Room'],
    );
  }

  String getDayOfWeekVN() {
    switch (dayOfWeek.toLowerCase()) {
      case 'monday':
      case 'mon':
        return 'Thứ 2';
      case 'tuesday':
      case 'tue':
        return 'Thứ 3';
      case 'wednesday':
      case 'wed':
        return 'Thứ 4';
      case 'thursday':
      case 'thu':
        return 'Thứ 5';
      case 'friday':
      case 'fri':
        return 'Thứ 6';
      case 'saturday':
      case 'sat':
        return 'Thứ 7';
      case 'sunday':
      case 'sun':
        return 'Chủ nhật';
      default:
        return dayOfWeek;
    }
  }
}
