namespace QLTTTA_WEB.Models
{
    public class StaffStudentListItem
    {
        public int StudentId { get; set; }
        public string? StudentCode { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; }
    }

    public class StaffRegistrationItemVM
    {
        public int RegistrationId { get; set; }
        public string? RegistrationCode { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public string? Status { get; set; }
        public int ClassId { get; set; }
        public string? ClassName { get; set; }
        public string? CourseName { get; set; }
    }

    public class StaffInvoiceItemVM
    {
        public int InvoiceId { get; set; }
        public string? InvoiceCode { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int Amount { get; set; }
        public string? Status { get; set; }
        public int RegistrationId { get; set; }
        public string? ClassName { get; set; }
        public string? CourseName { get; set; }
    }

    public class StaffStudentDetailViewModel
    {
        public StaffStudentListItem Student { get; set; } = new();
        public List<StaffRegistrationItemVM> Registrations { get; set; } = new();
        public List<StaffInvoiceItemVM> Invoices { get; set; } = new();
    }
}

