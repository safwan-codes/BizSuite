namespace BizSuite.Repositories
{
    public class UserLoginResult
    {
        public int    UserId    { get; set; }
        public int    CompanyId { get; set; }
        public string RoleName  { get; set; } = string.Empty;
        public string FullName  { get; set; } = string.Empty;
        public string Email     { get; set; } = string.Empty;
        public string Company   { get; set; } = string.Empty;
        public string Phone     { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public byte[]? ProfileImageData { get; internal set; }
        public string? ProfileImageMimeType { get; internal set; }
    }

    public interface IUserRepository
    {
        UserLoginResult? ValidateLogin(string email, string password);
        UserLoginResult? RegisterTenant(string companyName, string fullName, string email, string password);
        bool ChangePassword(int userId, string oldPassword, string newPassword);
        void UpdateProfile(int userId, string fullName, string email, string phone, string department, byte[]? imageData, string? mimeType);
        UserLoginResult? GetProfile(string email);
        bool SaveResetCode(string email, string code);
        bool VerifyResetCode(string email, string code);
        void ResetPassword(string email, string passwordHash);
    }
}
