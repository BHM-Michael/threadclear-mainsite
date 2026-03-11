using System.Threading.Tasks;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendAsync(string toEmail, string subject, string htmlBody);
    }
}