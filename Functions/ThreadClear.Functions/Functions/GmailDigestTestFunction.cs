using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services;

namespace ThreadClear.Functions.Functions
{
    public class GmailDigestTestFunction
    {
        private readonly ILogger<GmailDigestTestFunction> _logger;
        private readonly IConfiguration _configuration;
        private readonly GmailTokenService _tokenService;
        private readonly GmailService _gmailService;
        private readonly DigestEmailService _digestEmailService;

        public GmailDigestTestFunction(
            ILogger<GmailDigestTestFunction> logger,
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
        /// Preview digest HTML without sending email
        /// GET /api/gmail/digest/preview/{userId}
        /// </summary>
        [Function("GmailDigestPreview")]
        public async Task<HttpResponseData> Preview(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "gmail/digest/preview/{userId}")] HttpRequestData req,
            string userId)
        {
            _logger.LogInformation("Digest preview requested for user {UserId}", userId);

            if (!Guid.TryParse(userId, out var userGuid))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid userId format");
                return badRequest;
            }

            try
            {
                var digest = await BuildDigestForUserAsync(userGuid);
                
                if (digest == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("User not found or Gmail not connected");
                    return notFound;
                }

                var htmlContent = _digestEmailService.GenerateDigestHtml(digest);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await response.WriteStringAsync(htmlContent);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate preview for user {UserId}", userId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Error: {ex.Message}");
                return error;
            }
        }

        /// <summary>
        /// Send test digest email
        /// POST /api/gmail/digest/send/{userId}
        /// </summary>
        [Function("GmailDigestSend")]
        public async Task<HttpResponseData> Send(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "gmail/digest/send/{userId}")] HttpRequestData req,
            string userId)
        {
            _logger.LogInformation("Digest send requested for user {UserId}", userId);

            if (!Guid.TryParse(userId, out var userGuid))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid userId format");
                return badRequest;
            }

            try
            {
                var users = await _tokenService.GetAllGmailUsersAsync();
                var user = users.FirstOrDefault(u => u.UserId == userGuid);

                if (user == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("User not found or Gmail not connected");
                    return notFound;
                }

                var digest = await BuildDigestForUserAsync(userGuid);
                
                if (digest == null)
                {
                    var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await error.WriteStringAsync("Failed to build digest");
                    return error;
                }

                var htmlContent = _digestEmailService.GenerateDigestHtml(digest);
                var plainContent = _digestEmailService.GenerateDigestPlainText(digest);

                await SendDigestEmailAsync(user, digest, htmlContent, plainContent);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    sentTo = user.Email,
                    threadsAnalyzed = digest.TotalThreadsAnalyzed,
                    threadsNeedingAttention = digest.ThreadsNeedingAttention
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send digest for user {UserId}", userId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Error: {ex.Message}");
                return error;
            }
        }

        /// <summary>
        /// Get digest data as JSON (for debugging)
        /// GET /api/gmail/digest/data/{userId}
        /// </summary>
        [Function("GmailDigestData")]
        public async Task<HttpResponseData> GetData(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "gmail/digest/data/{userId}")] HttpRequestData req,
            string userId)
        {
            _logger.LogInformation("Digest data requested for user {UserId}", userId);

            if (!Guid.TryParse(userId, out var userGuid))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid userId format");
                return badRequest;
            }

            try
            {
                var digest = await BuildDigestForUserAsync(userGuid);

                if (digest == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("User not found or Gmail not connected");
                    return notFound;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(digest);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get digest data for user {UserId}", userId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Error: {ex.Message}");
                return error;
            }
        }

        private async Task<DigestData?> BuildDigestForUserAsync(Guid userId)
        {
            var users = await _tokenService.GetAllGmailUsersAsync();
            var user = users.FirstOrDefault(u => u.UserId == userId);

            if (user == null)
                return null;

            var accessToken = await _tokenService.GetValidAccessTokenAsync(user);
            if (string.IsNullOrEmpty(accessToken))
                return null;

            var threads = await _gmailService.ListRecentThreadsAsync(accessToken, hoursBack: 24, maxResults: 20);

            var digest = new DigestData
            {
                Date = DateTime.UtcNow,
                TotalThreadsAnalyzed = threads.Count
            };

            var analyzerUrl = _configuration["ConversationAnalyzerUrl"]
                ?? "https://threadclear-functions.azurewebsites.net/api/analyze";

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            foreach (var thread in threads)
            {
                try
                {
                    var conversation = _gmailService.ConvertThreadToConversation(thread);
                    var analysis = await AnalyzeConversationAsync(httpClient, analyzerUrl, conversation);

                    if (analysis == null) continue;

                    var digestThread = CreateDigestThread(thread, analysis);
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
                    _logger.LogWarning(ex, "Failed to analyze thread {ThreadId}: {Message}", thread.Id, ex.Message);
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
                return null;

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
        }
    }
}
