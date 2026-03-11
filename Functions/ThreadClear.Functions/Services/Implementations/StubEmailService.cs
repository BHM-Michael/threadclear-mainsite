using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class StubEmailService : IEmailService
    {
        private readonly ILogger<StubEmailService> _logger;

        public StubEmailService(ILogger<StubEmailService> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            _logger.LogInformation(
                "[StubEmailService] Would send to {Email} — Subject: {Subject}",
                toEmail, subject);
            return Task.CompletedTask;
        }
    }
}