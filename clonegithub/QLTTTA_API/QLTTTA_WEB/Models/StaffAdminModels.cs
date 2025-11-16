namespace QLTTTA_WEB.Models
{
    public class StaffAdminItem
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

    public class StaffCreateModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        // 3 = Kế toán, 4 = Nhân viên học vụ
        public int RoleId { get; set; } = 4;
        public string? FullName { get; set; }
        public string? EmployeeCode { get; set; }
        public string? Sex { get; set; }
        public string? Phone { get; set; }
    }

    public class StaffUpdateModel
    {
        public int UserId { get; set; }
        public string? Email { get; set; }
        public int RoleId { get; set; }
        public string? FullName { get; set; }
        public string? Sex { get; set; }
        public string? Phone { get; set; }
    }
}
