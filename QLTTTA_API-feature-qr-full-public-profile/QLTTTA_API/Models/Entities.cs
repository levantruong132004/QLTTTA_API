using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QLTTTA_API.Models
{
    [Table("HOC_VIEN", Schema = "QLTT_ADMIN")]
    // Học viên
    public class Student
    {
        [Key]
        [Column("ID_HOC_VIEN")]
        public int StudentId { get; set; }

        [Column("HO_TEN")]
        public string? FullName { get; set; }

        [Column("MA_HOC_VIEN")]
        public string? StudentCode { get; set; }

        [Column("GIOI_TINH")]
        public string? Sex { get; set; }

        [Column("NGAY_SINH")]
        public DateTime? DateOfBirth { get; set; }

        [Column("SO_DIEN_THOAI")]
        public string? PhoneNumber { get; set; }

        [Column("DIA_CHI")]
        public string? Address { get; set; }
    }

    // Khóa học
    public class Course
    {
        public int CourseId { get; set; }
        public string? CourseCode { get; set; }
        public string? CourseName { get; set; }
        public string? Description { get; set; }
        public int StandardFee { get; set; }
    }

    // Lớp học
    public class Class
    {
        public int ClassId { get; set; }
        public string? ClassCode { get; set; }
        public string? ClassName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int MaxSize { get; set; }
        public int CourseId { get; set; }
        public int TeacherId { get; set; }
    }

    // Giáo viên
    public class Teacher
    {
        public int TeacherId { get; set; }
        public string? FullName { get; set; }
        public string? TeacherCode { get; set; }
        public string? Sex { get; set; }
        public string? Education { get; set; }
        public string? PhoneNumber { get; set; }
    }

    // Đăng ký học
    public class Registration
    {
        public int RegistrationId { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public string? Status { get; set; }
        public DateTime? StudyDate { get; set; }
        public int StudentId { get; set; }
        public int ClassId { get; set; }
        public int StaffId { get; set; }
    }

    // Nhân viên học vụ
    public class AcademicStaff
    {
        public int StaffId { get; set; }
        public string? FullName { get; set; }
        public string? StaffCode { get; set; }
        public string? Sex { get; set; }
        public string? PhoneNumber { get; set; }
    }

    // Kế toán
    public class Accountant
    {
        public int AccountantId { get; set; }
        public string? FullName { get; set; }
        public string? StaffCode { get; set; }
        public string? Sex { get; set; }
        public string? PhoneNumber { get; set; }
    }

    // Hóa đơn
    public class Invoice
    {
        public int InvoiceId { get; set; }
        public string? InvoiceCode { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int Amount { get; set; }
        public string? Status { get; set; }
        public int RegistrationId { get; set; }
    }

    // Thanh toán
    public class Payment
    {
        public int PaymentId { get; set; }
        public DateTime? PaymentDate { get; set; }
        public int Amount { get; set; }
        public string? PaymentMethod { get; set; }
        public int InvoiceId { get; set; }
        public int AccountantId { get; set; }
    }

    // Lịch học
    public class Schedule
    {
        public int ScheduleId { get; set; }
        public int ClassId { get; set; }
        public int DayOfWeek { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
    }

    // Tài khoản
    public class Account
    {
        public int UserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public int RoleId { get; set; }
    }

    // Vai trò
    public class Role
    {
        public int RoleId { get; set; }
        public string? RoleName { get; set; }
    }
}