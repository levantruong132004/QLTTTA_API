using System.ComponentModel.DataAnnotations;

namespace QLTTTA_WEB.Models
{
    // Student ViewModels
    public class StudentViewModel
    {
        public int StudentId { get; set; }

        [Display(Name = "Họ tên")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Mã học viên")]
        public string StudentCode { get; set; } = string.Empty;

        [Display(Name = "Giới tính")]
        public string? Sex { get; set; }

        [Display(Name = "Ngày sinh")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }
    }

    public class StudentCreateViewModel
    {
        [Required(ErrorMessage = "Họ tên là bắt buộc")]
        [Display(Name = "Họ tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mã học viên là bắt buộc")]
        [Display(Name = "Mã học viên")]
        public string StudentCode { get; set; } = string.Empty;

        [Display(Name = "Giới tính")]
        public string? Sex { get; set; }

        [Required(ErrorMessage = "Ngày sinh là bắt buộc")]
        [Display(Name = "Ngày sinh")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
        [Display(Name = "Số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }
    }

    public class StudentEditViewModel : StudentCreateViewModel
    {
        public int StudentId { get; set; }
    }

    // Course ViewModels
    public class CourseViewModel
    {
        public int CourseId { get; set; }

        [Display(Name = "Mã khóa học")]
        public string CourseCode { get; set; } = string.Empty;

        [Display(Name = "Tên khóa học")]
        public string CourseName { get; set; } = string.Empty;

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Học phí chuẩn")]
        [DataType(DataType.Currency)]
        public int StandardFee { get; set; }
    }

    public class CourseCreateViewModel
    {
        [Required(ErrorMessage = "Mã khóa học là bắt buộc")]
        [Display(Name = "Mã khóa học")]
        public string CourseCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên khóa học là bắt buộc")]
        [Display(Name = "Tên khóa học")]
        public string CourseName { get; set; } = string.Empty;

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Học phí là bắt buộc")]
        [Display(Name = "Học phí chuẩn")]
        [Range(0, int.MaxValue, ErrorMessage = "Học phí phải lớn hơn 0")]
        public int StandardFee { get; set; }
    }

    public class CourseEditViewModel : CourseCreateViewModel
    {
        public int CourseId { get; set; }
    }

    // Class ViewModels
    public class ClassViewModel
    {
        public int ClassId { get; set; }

        [Display(Name = "Mã lớp")]
        public string ClassCode { get; set; } = string.Empty;

        [Display(Name = "Tên lớp")]
        public string ClassName { get; set; } = string.Empty;

        [Display(Name = "Ngày bắt đầu")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Display(Name = "Ngày kết thúc")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Sĩ số tối đa")]
        public int MaxSize { get; set; }

        [Display(Name = "Khóa học")]
        public string CourseName { get; set; } = string.Empty;

        [Display(Name = "Giáo viên")]
        public string TeacherName { get; set; } = string.Empty;

        public int CourseId { get; set; }
        public int TeacherId { get; set; }
    }

    // Teacher ViewModels
    public class TeacherViewModel
    {
        public int TeacherId { get; set; }

        [Display(Name = "Họ tên")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Mã giáo viên")]
        public string TeacherCode { get; set; } = string.Empty;

        [Display(Name = "Giới tính")]
        public string? Sex { get; set; }

        [Display(Name = "Trình độ học vấn")]
        public string? Education { get; set; }

        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    // Registration ViewModels
    public class RegistrationViewModel
    {
        public int RegistrationId { get; set; }

        [Display(Name = "Ngày đăng ký")]
        [DataType(DataType.Date)]
        public DateTime? RegistrationDate { get; set; }

        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = string.Empty;

        [Display(Name = "Ngày bắt đầu học")]
        [DataType(DataType.Date)]
        public DateTime? StudyDate { get; set; }

        [Display(Name = "Học viên")]
        public string StudentName { get; set; } = string.Empty;

        [Display(Name = "Lớp học")]
        public string ClassName { get; set; } = string.Empty;

        [Display(Name = "Nhân viên học vụ")]
        public string StaffName { get; set; } = string.Empty;

        public int StudentId { get; set; }
        public int ClassId { get; set; }
        public int StaffId { get; set; }
    }

    // Payment ViewModels
    public class PaymentViewModel
    {
        public int PaymentId { get; set; }

        [Display(Name = "Ngày thanh toán")]
        [DataType(DataType.Date)]
        public DateTime? PaymentDate { get; set; }

        [Display(Name = "Số tiền")]
        [DataType(DataType.Currency)]
        public int Amount { get; set; }

        [Display(Name = "Phương thức thanh toán")]
        public string PaymentMethod { get; set; } = string.Empty;

        [Display(Name = "Hóa đơn")]
        public string InvoiceCode { get; set; } = string.Empty;

        [Display(Name = "Kế toán")]
        public string AccountantName { get; set; } = string.Empty;

        public int InvoiceId { get; set; }
        public int AccountantId { get; set; }
    }

    // Invoice ViewModels
    public class InvoiceViewModel
    {
        public int InvoiceId { get; set; }

        [Display(Name = "Mã hóa đơn")]
        public string InvoiceCode { get; set; } = string.Empty;

        [Display(Name = "Ngày tạo")]
        [DataType(DataType.Date)]
        public DateTime? CreatedDate { get; set; }

        [Display(Name = "Ngày đến hạn")]
        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Số tiền")]
        [DataType(DataType.Currency)]
        public int Amount { get; set; }

        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = string.Empty;

        [Display(Name = "Học viên")]
        public string StudentName { get; set; } = string.Empty;

        [Display(Name = "Khóa học")]
        public string CourseName { get; set; } = string.Empty;

        public int RegistrationId { get; set; }
    }

    // Pagination ViewModels
    public class PaginatedViewModel<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalRecords { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
        public bool HasPrevious => PageNumber > 1;
        public bool HasNext => PageNumber < TotalPages;
        public string? SearchTerm { get; set; }
    }

    // Dashboard ViewModels
    public class DashboardViewModel
    {
        public int TotalStudents { get; set; }
        public int TotalCourses { get; set; }
        public int TotalClasses { get; set; }
        public int TotalTeachers { get; set; }
        public int ActiveRegistrations { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<RecentRegistrationViewModel> RecentRegistrations { get; set; } = new();
    }

    public class RecentRegistrationViewModel
    {
        public string StudentName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public DateTime? RegistrationDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}