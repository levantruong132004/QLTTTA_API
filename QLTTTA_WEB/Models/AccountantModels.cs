namespace QLTTTA_WEB.Models
{
    public class AccountantRegItem
    {
        public int RegistrationId { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public string? Status { get; set; }
        public int StudentId { get; set; }
        public string? StudentName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public int ClassId { get; set; }
        public string? ClassName { get; set; }
        public string? CourseName { get; set; }
        public int StandardFee { get; set; }
        public int? InvoiceId { get; set; } // Thêm để kiểm tra có hóa đơn hay không
        public string? InvoiceCode { get; set; } // Mã hóa đơn
        public bool IsSigned { get; set; } // Đã ký số chưa
    }

    public class AccountantHomeViewModel
    {
        public int? SelectedCourseId { get; set; }
        public int? SelectedClassId { get; set; }
        public List<SimpleCourseViewModel> Courses { get; set; } = new();
        public List<AdminClassItem> Classes { get; set; } = new();
        public List<AccountantRegItem> Registrations { get; set; } = new();
    }

    // New page model for accountant invoice page
    public class AccountantInvoicePageViewModel
    {
        public AccountantRegItem? Registration { get; set; }
        public InvoiceViewModel? Invoice { get; set; } // reuse existing model in ViewModels.cs
        public DateTime? SuggestedDueDate { get; set; }
        public int SuggestedAmount { get; set; }
        public DigitalSignatureViewModel? Signature { get; set; }
    }

    // Models cho chữ ký số
    public class DigitalSignatureViewModel
    {
        public bool IsSigned { get; set; }
        public string? SignedBy { get; set; }
        public DateTime? SignedDate { get; set; }
        public bool IsValid { get; set; }
        public string? Algorithm { get; set; }
    }

    public class CreateAndSignInvoiceViewModel
    {
        public int RegistrationId { get; set; }
        public DateTime DueDate { get; set; }
        public int Amount { get; set; }
        public string PrivateKeyPath { get; set; } = "";
        public int AccountantId { get; set; }
    }

    public class GenerateKeyPairViewModel
    {
        public string? PublicKey { get; set; }
        public string? PrivateKey { get; set; }
        public string? Message { get; set; }
    }

    public class SignInvoiceViewModel
    {
        public int InvoiceId { get; set; }
        public string PrivateKeyPath { get; set; } = "";
        public int AccountantId { get; set; }
    }
}
