using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThreadClear.Functions.Services.Interfaces;
using ThreadClear.Models;
using ThreadClear.Functions.Services;

namespace ThreadClear.Functions
{
    public class DigestSenderFunction
    {
        private readonly IDigestInsightRepository _digestRepo;
        private readonly IUserService _userService;
        private readonly IEmailService _emailService;
        private readonly ILogger<DigestSenderFunction> _logger;

        public DigestSenderFunction(
            IDigestInsightRepository digestRepo,
            IUserService userService,
            IEmailService emailService,
            ILogger<DigestSenderFunction> logger)
        {
            _digestRepo = digestRepo;
            _userService = userService;
            _emailService = emailService;
            _logger = logger;
        }

        [Function("DigestSender")]
        public async Task Run([TimerTrigger("0 */30 * * * *")] TimerInfo timer)
        {
            _logger.LogInformation("DigestSender fired at {Time}", DateTime.UtcNow);

            var pendingUserIds = await _digestRepo.GetUsersWithPendingInsightsAsync();

            if (!pendingUserIds.Any())
            {
                _logger.LogInformation("No pending digest insights found");
                return;
            }

            _logger.LogInformation("Sending digests to {Count} users", pendingUserIds.Count);

            foreach (var userId in pendingUserIds)
            {
                try
                {
                    await SendDigestForUser(userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Digest send failed for user {UserId}", userId);
                }
            }
        }

        private async Task SendDigestForUser(Guid userId)
        {
            var user = await _userService.GetUserById(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found, skipping digest", userId);
                return;
            }

            var insights = await _digestRepo.GetPendingInsightsForUserAsync(userId);
            if (!insights.Any()) return;

            var highRisk = insights.Where(i => i.RiskLevel == "High").ToList();
            var mediumRisk = insights.Where(i => i.RiskLevel == "Medium").ToList();
            var lowRisk = insights.Where(i => i.RiskLevel == "Low").ToList();

            var avgHealth = (int)insights.Average(i => i.HealthScore);
            var totalUnanswered = insights.Sum(i => i.UnansweredQuestions);
            var totalTension = insights.Sum(i => i.TensionSignals);

            var subject = BuildSubject(highRisk.Count, mediumRisk.Count, avgHealth);
            var body = BuildEmailBody(insights, highRisk, mediumRisk, lowRisk,
                                         avgHealth, totalUnanswered, totalTension);

            await _emailService.SendAsync(user.Email, subject, body);

            var provider = insights.FirstOrDefault()?.Provider ?? "unknown";
            await _digestRepo.LogAuditAsync(userId, insights.Count, provider);
            await _digestRepo.DeleteAsync(insights.Select(i => i.Id).ToList());

            _logger.LogInformation(
                "Digest sent to {Email} — {Count} threads, avg health {Health}",
                user.Email, insights.Count, avgHealth);
        }

        private string BuildSubject(int highCount, int medCount, int avgHealth)
        {
            if (highCount > 0)
                return $"⚠️ ThreadClear: {highCount} high-risk thread{(highCount > 1 ? "s" : "")} need attention";
            if (medCount > 0)
                return $"ThreadClear Digest: {medCount} thread{(medCount > 1 ? "s" : "")} to review";
            return $"ThreadClear Digest: Inbox health {avgHealth}/100 ✓";
        }

        private string BuildEmailBody(
            List<DigestInsight> all,
            List<DigestInsight> high,
            List<DigestInsight> medium,
            List<DigestInsight> low,
            int avgHealth, int unanswered, int tension)
        {
            var lines = new List<string>
            {
                "<h2>Your ThreadClear Digest</h2>",
                $"<p><strong>Inbox health:</strong> {avgHealth}/100 &nbsp;|&nbsp; " +
                $"<strong>Threads analyzed:</strong> {all.Count} &nbsp;|&nbsp; " +
                $"<strong>Unanswered questions:</strong> {unanswered} &nbsp;|&nbsp; " +
                $"<strong>Tension signals:</strong> {tension}</p>",
                "<hr/>"
            };

            if (high.Any())
            {
                lines.Add("<h3 style='color:#c0392b'>⚠️ High Risk</h3>");
                foreach (var i in high) lines.Add(FormatInsightRow(i));
            }
            if (medium.Any())
            {
                lines.Add("<h3 style='color:#e67e22'>⚡ Medium Risk</h3>");
                foreach (var i in medium) lines.Add(FormatInsightRow(i));
            }
            if (low.Any())
            {
                lines.Add("<h3 style='color:#27ae60'>✓ Low Risk</h3>");
                foreach (var i in low) lines.Add(FormatInsightRow(i));
            }

            lines.Add("<hr/><p style='font-size:12px;color:#999'>ThreadClear · " +
                      "<a href='https://threadclear.com/preferences'>Manage digest preferences</a></p>");

            return string.Join("\n", lines);
        }

        private string FormatInsightRow(DigestInsight i)
        {
            return $"<div style='margin-bottom:12px;padding:10px;border-left:4px solid #ccc'>" +
                   $"<strong>{System.Net.WebUtility.HtmlEncode(i.Subject ?? "No Subject")}</strong><br/>" +
                   $"Health: {i.HealthScore}/100 &nbsp;·&nbsp; " +
                   $"{i.UnansweredQuestions} unanswered &nbsp;·&nbsp; " +
                   $"{i.TensionSignals} tension signals<br/>" +
                   $"<span style='color:#666;font-size:13px'>{i.Summary}</span>" +
                   "</div>";
        }
    }
}