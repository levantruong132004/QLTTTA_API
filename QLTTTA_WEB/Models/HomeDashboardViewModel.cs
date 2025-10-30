namespace QLTTTA_WEB.Models
{
    public class HomeDashboardViewModel
    {
        public StudentProfileViewModel? Student { get; set; }
        public List<PublicCourseItem> Courses { get; set; } = new();
        // Staff-specific
        public int? SelectedCourseId { get; set; }
        public List<SimpleCourseViewModel> StaffCourses { get; set; } = new();
        public List<AdminRegistrationItem>? StaffRegistrations { get; set; } // reserved
        public List<AdminClassItem> StaffClasses { get; set; } = new();
    }
}
