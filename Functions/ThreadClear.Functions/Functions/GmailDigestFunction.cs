using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services;

namespace ThreadClear.Functions.Functions
{
    public class GmailDigestFunction
    {
        private readonly ILogger<GmailDigestFunction> _logger;
        private readonly IConfiguration _configuration;
        private readonly GmailTokenService _tokenService;
        private readonly GmailService _gmailService;
        private readonly DigestEmailService _digestEmailService;

        public GmailDigestFunction(
            ILogger<GmailDigestFunction> logger,
            IConfiguration configuration,
            GmailTokenService tokenService,
            GmailService gmailService,
            DigestEmailService digestEmailService)
        {
            _logger = logger;
            _configuration = configuration;
            _tokenService = tokenService;
            _gmailService = gmailService;
            _digestEmailService = digestEmailService;
        }

        /// <summary>
        /// Runs daily at 7am UTC to generate and send digest emails
        /// CRON: 0 0 7 * * * (second minute hour day month day-of-week)
        /// </summary>
        [Function("GmailDigest")]
        public async Task Run([TimerTrigger("0 0 7 * * *")] TimerInfo timerInfo)
        {
            _logger.LogInformation("Gmail Digest function started at {Time}", DateTime.UtcNow);

            try
            {
                var users = await _tokenService.GetAllGmailUsersAsync();
                _logger.LogInformation("Found {Count} users with Gmail connected", users.Count);

                var successCount = 0;
                var errorCount = 0;

                foreach (var user in users)
                {
                    try
                    {
                        await ProcessUserDigestAsync(user);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process digest for user {UserId}", user.UserId);
                        errorCount++;
                    }
                }

                _logger.LogInformation("Gmail Digest completed. Success: {Success}, Errors: {Errors}", successCount, errorCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gmail Digest function failed");
                throw;
            }
        }

        private async Task ProcessUserDigestAsync(GmailUser user)
        {
            _logger.LogInformation("Processing digest for user {UserId} ({Email})", user.UserId, user.Email);

            // Get valid access token (refresh if needed)
            var accessToken = await _tokenService.GetValidAccessTokenAsync(user);
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Could not get valid token for user {UserId}", user.UserId);
                return;
            }

            // Fetch recent threads (last 24 hours)
            var threads = await _gmailService.ListRecentThreadsAsync(accessToken, hoursBack: 24, maxResults: 50);
            _logger.LogInformation("Fetched {Count} threads for user {UserId}", threads.Count, user.UserId);

            if (!threads.Any())
            {
                _logger.LogInformation("No threads to analyze for user {UserId}", user.UserId);
                return;
            }

            // Analyze each thread and build digest
            var digest = await BuildDigestAsync(threads, user);

            // Only send if there's something to report
            if (digest.ThreadsNeedingAttention == 0)
            {
                _logger.LogInformation("No issues found for user {UserId}, skipping email", user.UserId);
                return;
            }

            // Generate and send email
            var htmlContent = _digestEmailService.GenerateDigestHtml(digest);
            var plainContent = _digestEmailService.GenerateDigestPlainText(digest);
            await SendDigestEmailAsync(user, digest, htmlContent, plainContent);
        }

        private async Task<DigestData> BuildDigestAsync(List<GmailThread> threads, GmailUser user)
        {
            var digest = new DigestData
            {
                Date = DateTime.UtcNow,
                TotalThreadsAnalyzed = threads.Count
            };

            var analyzerUrl = _configuration["ConversationAnalyzerUrl"] 
                ?? "https://threadclear-functions.azurewebsites.net/api/analyze";

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            foreach (var thread in threads)
            {
                try
                {
                    var conversation = _gmailService.ConvertThreadToConversation(thread);
                    var analysis = await AnalyzeConversationAsync(httpClient, analyzerUrl, conversation);

                    if (analysis == null) continue;

                    var digestThread = CreateDigestThread(thread, analysis);

                    // Classify by priority based on findings
                    var priority = ClassifyPriority(analysis);
                    
                    if (priority == "high")
                        digest.HighPriorityThreads.Add(digestThread);
                    else if (priority == "medium")
                        digest.MediumPriorityThreads.Add(digestThread);
                    else
                        digest.CleanThreadCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to analyze thread {ThreadId}", thread.Id);
                }
            }

            return digest;
        }

        private async Task<AnalysisResult?> AnalyzeConversationAsync(HttpClient client, string url, string conversation)
        {
            var request = new { conversationText = conversation };
            var content = new StringContent(
                JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Analysis API returned {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        private DigestThread CreateDigestThread(GmailThread gmailThread, AnalysisResult analysis)
        {
            var firstMessage = gmailThread.Messages?.FirstOrDefault();
            var subject = GetHeader(firstMessage?.Payload, "Subject") ?? "(No Subject)";
            
            var participants = gmailThread.Messages?
                .Select(m => GetHeader(m.Payload, "From"))
                .Where(f => !string.IsNullOrEmpty(f))
                .Select(f => ExtractName(f!))
                .Distinct()
                .Take(3)
                .ToList() ?? new List<string>();

            return new DigestThread
            {
                ThreadId = gmailThread.Id,
                Subject = subject,
                Participants = participants,
                UnansweredQuestions = analysis.Capsule?.Analysis?.UnansweredQuestions ?? new List<UnansweredQuestion>(),
                TensionPoints = analysis.Capsule?.Analysis?.TensionPoints ?? new List<TensionPoint>(),
                Misalignments = analysis.Capsule?.Analysis?.Misalignments ?? new List<Misalignment>()
            };
        }

        private string ClassifyPriority(AnalysisResult analysis)
        {
            var a = analysis.Capsule?.Analysis;

            var hasHighSeverity =
                (a?.TensionPoints?.Any(t => t.Severity?.ToLower() == "high") ?? false) ||
                (a?.Misalignments?.Any(m => m.Severity?.ToLower() == "high") ?? false);

            var multipleUnanswered = (a?.UnansweredQuestions?.Count ?? 0) >= 2;

            if (hasHighSeverity || multipleUnanswered)
                return "high";

            var hasFindings =
                (a?.UnansweredQuestions?.Any() ?? false) ||
                (a?.TensionPoints?.Any() ?? false) ||
                (a?.Misalignments?.Any() ?? false);

            if (hasFindings)
                return "medium";

            return "low";
        }

        private string? GetHeader(GmailPayload? payload, string name)
        {
            return payload?.Headers?.FirstOrDefault(h =>
                h.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        private string ExtractName(string from)
        {
            var match = System.Text.RegularExpressions.Regex.Match(from, @"^([^<]+)<");
            return match.Success ? match.Groups[1].Value.Trim().Trim('"') : from;
        }

        private async Task SendDigestEmailAsync(GmailUser user, DigestData digest, string htmlContent, string plainContent)
        {
            var smtpHost = _configuration["SmtpHost"] ?? "smtp.sendgrid.net";
            var smtpPort = int.Parse(_configuration["SmtpPort"] ?? "587");
            var smtpUser = _configuration["SmtpUser"] ?? "apikey";
            var smtpPassword = _configuration["SmtpPassword"] ?? _configuration["SendGridApiKey"];
            var fromEmail = _configuration["DigestFromEmail"] ?? "digest@threadclear.com";
            var fromName = _configuration["DigestFromName"] ?? "ThreadClear";

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPassword),
                EnableSsl = true
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = $"ThreadClear Daily Digest: {digest.ThreadsNeedingAttention} threads need attention",
                IsBodyHtml = true,
                Body = htmlContent
            };

            message.To.Add(new MailAddress(user.Email, user.DisplayName));

            var plainView = AlternateView.CreateAlternateViewFromString(plainContent, System.Text.Encoding.UTF8, "text/plain");
            var htmlView = AlternateView.CreateAlternateViewFromString(htmlContent, System.Text.Encoding.UTF8, "text/html");

            message.AlternateViews.Add(plainView);
            message.AlternateViews.Add(htmlView);

            await client.SendMailAsync(message);
            _logger.LogInformation("Digest email sent to {Email}", user.Email);
        }
    }

    /// <summary>
    /// Response model from ConversationAnalyzer - matches your existing models
    /// </summary>
    //public class AnalysisResult
    //{
    //    public List<UnansweredQuestion>? UnansweredQuestions { get; set; }
    //    public List<TensionPoint>? TensionPoints { get; set; }
    //    public List<Misalignment>? Misalignments { get; set; }
    //    public int? HealthScore { get; set; }
    //    public string? Summary { get; set; }
    //}

    public class AnalysisResult
    {
        public bool Success { get; set; }
        public CapsuleData? Capsule { get; set; }
        public string? ParsingMode { get; set; }
        public string? User { get; set; }
    }

    public class CapsuleData
    {
        public ConversationAnalysisData? Analysis { get; set; }
        public string? Summary { get; set; }
    }

    public class ConversationAnalysisData
    {
        public List<UnansweredQuestion>? UnansweredQuestions { get; set; }
        public List<TensionPoint>? TensionPoints { get; set; }
        public List<Misalignment>? Misalignments { get; set; }
        public ConversationHealthData? ConversationHealth { get; set; }
    }

    public class ConversationHealthData
    {
        public double? HealthScore { get; set; }
        public string? RiskLevel { get; set; }
    }
}
