using System.Threading.Tasks;

namespace BizSuite.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string email, string subject, string message);
    }
}