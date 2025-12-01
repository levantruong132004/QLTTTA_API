namespace QLTTTA_WEB.Models
{
    // Dùng cho trang Quản trị - Lớp học (khớp với API /api/classes)
    public class AdminClassItem
    {
        public int ClassId { get; set; }
        public string ClassCode { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int MaxSize { get; set; }
        public int CourseId { get; set; }
        public int TeacherId { get; set; }
        public string? Status { get; set; }
        public int ApprovedCount { get; set; }
    }

    // Dùng cho trang Quản trị - Lịch học (khớp với API /api/schedules/by-class/{id})
    public class ScheduleItem
    {
        public int ScheduleId { get; set; }
        public int DayOfWeek { get; set; }
        public string StartTime { get; set; } = string.Empty; // HH:mm
        public string EndTime { get; set; } = string.Empty;   // HH:mm
    }

    // Dùng cho trang Quản trị - Duyệt đăng ký (khớp với API /api/registrations)
    public class AdminRegistrationItem
    {
        public int RegistrationId { get; set; }
        public string? RegistrationCode { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public int StudentId { get; set; }
        public int ClassId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ClassName { get; set; }
        public string? CourseName { get; set; }
    }

    public class StudentMyRegistrationItem
    {
        public int RegistrationId { get; set; }
        public int StudentId { get; set; }
        public int ClassId { get; set; }
        public string? ClassName { get; set; }
        public string? CourseName { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? RegistrationDate { get; set; }
    }

    // Dùng để hiển thị danh sách học viên đã duyệt trong một lớp
    public class RosterStudentItem
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? StudentCode { get; set; }
        public string? Sex { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
    }


}
