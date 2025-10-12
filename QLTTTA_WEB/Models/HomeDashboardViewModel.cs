namespace QLTTTA_WEB.Models
{
    public class HomeDashboardViewModel
    {
        public StudentProfileViewModel? Student { get; set; }
        public List<PublicCourseItem> Courses { get; set; } = new();
    }
}
