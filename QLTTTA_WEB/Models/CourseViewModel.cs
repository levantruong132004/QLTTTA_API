namespace QLTTTA_WEB.Models
{
    public class SimpleCourseViewModel
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    // Models cho học viên xem hóa đơn và chữ ký số
    public class StudentRegistrationViewModel
    {
        public int RegistrationId { get; set; }
        public string? RegistrationCode { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public string? Status { get; set; }
        public string? CourseName { get; set; }
        public string? ClassName { get; set; }
        public DateTime? StudyDate { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool HasInvoice { get; set; }
        public string? InvoiceCode { get; set; }
        public string? InvoiceStatus { get; set; }
        public bool HasDigitalSignature { get; set; }
        public string? StudentName { get; set; }
        public DateTime? StudyStartDate { get; set; }
        public DateTime? StudyEndDate { get; set; }
    }

    public class StudentInvoiceViewModel
    {
        public int InvoiceId { get; set; }
        public string InvoiceCode { get; set; } = "";
        public DateTime? CreatedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int Amount { get; set; }
        public string Status { get; set; } = "";
        public int RegistrationId { get; set; }
        public string? SignatureBase64 { get; set; } // Chữ ký số (data ký RSA, không phải ảnh)
        public string? SignatureImageBase64 { get; set; } // Ảnh chữ ký tay đã scan
    }

    public class StudentInvoiceDetailViewModel
    {
        public StudentRegistrationViewModel? Registration { get; set; }
        public StudentInvoiceViewModel? Invoice { get; set; }
        public DigitalSignatureViewModel? Signature { get; set; }
    }

    public class SignatureVerificationResult
    {
        public bool Success { get; set; }
        public bool IsValid { get; set; }
        public string? SignedBy { get; set; }
        public DateTime? SignedDate { get; set; }
        public string? Message { get; set; }
        public string? InvoiceData { get; set; }
    }

    // DTO cho API response từ backend
    public class StudentRegistrationDetailDto
    {
        public int RegistrationId { get; set; }
        public string? RegistrationCode { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public string? Status { get; set; }
        public string? CourseName { get; set; }
        public string? ClassName { get; set; }
        public DateTime? StudyDate { get; set; } // Chỉ sử dụng StudyDate
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
