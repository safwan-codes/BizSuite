namespace BizSuite.Models.Auth
{
    public class UserLoginResult
    {
        public int UserId { get; set; }
        public int CompanyId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;

        public byte[]? ProfileImageData { get; set; }
        public string? ProfileImageMimeType { get; set; }
    }
}