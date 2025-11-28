namespace QLTTTA_API.Models.DTOs
{
    public class StaffListItemDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? FullName { get; set; }
        public string? Sex { get; set; }
        public string? Phone { get; set; }
    }

    public class StaffCreateDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        // 3=KeToan, 4=NhanVienHocVu
        public int RoleId { get; set; } = 4;
        public string? FullName { get; set; }
        public string? EmployeeCode { get; set; }
        public string? Sex { get; set; }
        public string? Phone { get; set; }
    }

    public class StaffUpdateDto
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        // 3=KeToan, 4=NhanVienHocVu
        public int RoleId { get; set; }
        public string? FullName { get; set; }
        public string? Sex { get; set; }
        public string? Phone { get; set; }
    }
}
