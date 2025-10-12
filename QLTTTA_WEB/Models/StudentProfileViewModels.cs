namespace QLTTTA_WEB.Models
{
    public class StudentProfileViewModel
    {
        public string? HoTen { get; set; }
        public string? MaHocVien { get; set; }
        public string? GioiTinh { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string? SoDienThoai { get; set; }
        public string? DiaChi { get; set; }
        public string? Email { get; set; }
    }

    public class StudentProfileUpdateRequest
    {
        public string? HoTen { get; set; }
        public string? GioiTinh { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string? SoDienThoai { get; set; }
        public string? DiaChi { get; set; }
        public string? Email { get; set; }
    }

    public class PublicCourseItem
    {
        public string? CourseCode { get; set; }
        public string? CourseName { get; set; }
        public string? Description { get; set; }
        public int StandardFee { get; set; }
    }
}
