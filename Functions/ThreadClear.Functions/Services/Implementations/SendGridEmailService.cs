using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class SendGridEmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SendGridEmailService> _logger;
        private readonly HttpClient _httpClient;

        public SendGridEmailService(IConfiguration config, ILogger<SendGridEmailService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("sendgrid");
        }

        public async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            var apiKey = _config["SendGridApiKey"];
            var fromEmail = _config["SendGridFromEmail"] ?? "digest@threadclear.com";
            var fromName = _config["SendGridFromName"] ?? "ThreadClear";

            var payload = new
            {
                personalizations = new[]
                {
                    new { to = new[] { new { email = toEmail } } }
                },
                from = new { email = fromEmail, name = fromName },
                subject = subject,
                content = new[]
                {
                    new { type = "text/html", value = htmlBody }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://api.sendgrid.com/v3/mail/send");
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Digest email sent to {Email} — Subject: {Subject}",
                    toEmail, subject);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("SendGrid failed for {Email} — Status: {Status}, Error: {Error}",
                    toEmail, response.StatusCode, error);
                throw new Exception($"SendGrid send failed: {response.StatusCode}");
            }
        }
    }
}