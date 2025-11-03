using System.Security.Cryptography;
using System.Text;

namespace QLTTTA_API.Services
{
    /// <summary>
    /// Tạo mật khẩu Oracle dẫn xuất từ mật khẩu UI + bí mật máy chủ (pepper).
    /// Mục tiêu: mật khẩu dùng cho Oracle khác với mật khẩu UI, người dùng không thể đăng nhập trực tiếp nếu chỉ biết mật khẩu UI.
    /// </summary>
    public static class OraclePasswordDeriver
    {
        // Tạo mật khẩu chỉ gồm chữ và số, độ dài 20-24 ký tự để an toàn với ALTER USER và client.
        public static string Derive(string username, string uiPassword, string pepper, int length = 24)
        {
            username ??= string.Empty;
            uiPassword ??= string.Empty;
            pepper ??= "LOCAL-DEV-PEPPER"; // fallback dev

            // HMAC-SHA256(pepper, username:uiPassword)
            var data = Encoding.UTF8.GetBytes(username + ":" + uiPassword);
            var key = Encoding.UTF8.GetBytes(pepper);
            using var hmac = new HMACSHA256(key);
            var mac = hmac.ComputeHash(data);

            // Chuyển sang HEX chữ hoa, lấy length ký tự đầu
            var hex = BitConverter.ToString(mac).Replace("-", string.Empty); // A-F0-9
            if (length <= 0 || length > hex.Length) length = Math.Min(24, hex.Length);
            var pwd = hex.Substring(0, length);
            return pwd; // Ví dụ: "3F2A9C..." (chỉ A-Z0-9)
        }
    }
}
