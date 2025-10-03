using System.ComponentModel.DataAnnotations;

namespace QLTTTA_API.Models.DTOs
{
    // Student DTOs
    public class StudentCreateDto
    {
        [Required(ErrorMessage = "Họ tên là bắt buộc")]
        public string FullName { get; set; } = string.Empty;

        // StudentCode được sinh tự động bởi trigger nếu không truyền
        public string? StudentCode { get; set; }

        public string? Sex { get; set; }

        [Required(ErrorMessage = "Ngày sinh là bắt buộc")]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Address { get; set; }
    }

    public class StudentUpdateDto : StudentCreateDto
    {
        public int StudentId { get; set; }
    }

    // Course DTOs
    public class CourseCreateDto
    {
        [Required(ErrorMessage = "Mã khóa học là bắt buộc")]
        public string CourseCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên khóa học là bắt buộc")]
        public string CourseName { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required(ErrorMessage = "Học phí là bắt buộc")]
        public int StandardFee { get; set; }
    }

    public class CourseUpdateDto : CourseCreateDto
    {
        public int CourseId { get; set; }
    }

    // Class DTOs
    public class ClassCreateDto
    {
        [Required(ErrorMessage = "Mã lớp là bắt buộc")]
        public string ClassCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên lớp là bắt buộc")]
        public string ClassName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ngày bắt đầu là bắt buộc")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Ngày kết thúc là bắt buộc")]
        public DateTime EndDate { get; set; }

        [Required(ErrorMessage = "Sĩ số tối đa là bắt buộc")]
        public int MaxSize { get; set; }

        [Required(ErrorMessage = "Khóa học là bắt buộc")]
        public int CourseId { get; set; }

        [Required(ErrorMessage = "Giáo viên là bắt buộc")]
        public int TeacherId { get; set; }
    }

    public class ClassUpdateDto : ClassCreateDto
    {
        public int ClassId { get; set; }
    }

    // Registration DTOs
    public class RegistrationCreateDto
    {
        [Required(ErrorMessage = "Học viên là bắt buộc")]
        public int StudentId { get; set; }

        [Required(ErrorMessage = "Lớp học là bắt buộc")]
        public int ClassId { get; set; }

        public DateTime? StudyDate { get; set; }
        public string Status { get; set; } = "Đang chờ";
    }

    public class RegistrationUpdateDto : RegistrationCreateDto
    {
        public int RegistrationId { get; set; }
        public DateTime? RegistrationDate { get; set; }
    }

    // Teacher DTOs
    public class TeacherCreateDto
    {
        [Required(ErrorMessage = "Họ tên là bắt buộc")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mã giáo viên là bắt buộc")]
        public string TeacherCode { get; set; } = string.Empty;

        public string? Sex { get; set; }
        public string? Education { get; set; }

        [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class TeacherUpdateDto : TeacherCreateDto
    {
        public int TeacherId { get; set; }
    }

    // Invoice DTOs
    public class InvoiceCreateDto
    {
        [Required(ErrorMessage = "Mã hóa đơn là bắt buộc")]
        public string InvoiceCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ngày đến hạn là bắt buộc")]
        public DateTime DueDate { get; set; }

        [Required(ErrorMessage = "Số tiền là bắt buộc")]
        public int Amount { get; set; }

        [Required(ErrorMessage = "Đăng ký học là bắt buộc")]
        public int RegistrationId { get; set; }

        public string Status { get; set; } = "Chưa thanh toán";
    }

    public class InvoiceUpdateDto : InvoiceCreateDto
    {
        public int InvoiceId { get; set; }
        public DateTime? CreatedDate { get; set; }
    }

    // Payment DTOs
    public class PaymentCreateDto
    {
        [Required(ErrorMessage = "Số tiền là bắt buộc")]
        public int Amount { get; set; }

        [Required(ErrorMessage = "Phương thức thanh toán là bắt buộc")]
        public string PaymentMethod { get; set; } = string.Empty;

        [Required(ErrorMessage = "Hóa đơn là bắt buộc")]
        public int InvoiceId { get; set; }

        [Required(ErrorMessage = "Kế toán là bắt buộc")]
        public int AccountantId { get; set; }
    }

    public class PaymentUpdateDto : PaymentCreateDto
    {
        public int PaymentId { get; set; }
        public DateTime? PaymentDate { get; set; }
    }

    // Schedule DTOs
    public class ScheduleCreateDto
    {
        [Required(ErrorMessage = "Lớp học là bắt buộc")]
        public int ClassId { get; set; }

        [Required(ErrorMessage = "Thứ là bắt buộc")]
        [Range(2, 8, ErrorMessage = "Thứ phải từ 2 đến 8")]
        public int DayOfWeek { get; set; }

        [Required(ErrorMessage = "Giờ bắt đầu là bắt buộc")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "Giờ kết thúc là bắt buộc")]
        public TimeSpan EndTime { get; set; }
    }

    public class ScheduleUpdateDto : ScheduleCreateDto
    {
        public int ScheduleId { get; set; }
    }

    // Response DTOs
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public object? Errors { get; set; }
    }

    public class PaginatedResponse<T>
    {
        public List<T> Data { get; set; } = new();
        public int TotalRecords { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
    }
}