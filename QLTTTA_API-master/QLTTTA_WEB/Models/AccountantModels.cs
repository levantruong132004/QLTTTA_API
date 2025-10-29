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
    }
}
