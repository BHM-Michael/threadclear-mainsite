using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services
{
    public class DigestEmailService
    {
        private readonly ILogger<DigestEmailService> _logger;

        public DigestEmailService(ILogger<DigestEmailService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Generate the HTML email content for a daily digest
        /// </summary>
        public string GenerateDigestHtml(DigestData digest)
        {
            var highPriorityHtml = GenerateHighPrioritySection(digest.HighPriorityThreads);
            var mediumPriorityHtml = GenerateMediumPrioritySection(digest.MediumPriorityThreads);
            var summaryText = digest.CleanThreadCount > 0
                ? $"<p style='color: #22c55e;'>✓ {digest.CleanThreadCount} threads look healthy</p>"
                : "";

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>ThreadClear Daily Digest</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #1f2937; max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #7c3aed 0%, #a855f7 100%); color: white; padding: 24px; border-radius: 12px 12px 0 0; text-align: center; }}
        .header h1 {{ margin: 0; font-size: 24px; }}
        .header p {{ margin: 8px 0 0 0; opacity: 0.9; }}
        .stats {{ background: #f3f4f6; padding: 16px 24px; display: flex; justify-content: space-around; text-align: center; }}
        .stat {{ }}
        .stat-value {{ font-size: 28px; font-weight: bold; color: #7c3aed; }}
        .stat-label {{ font-size: 12px; color: #6b7280; text-transform: uppercase; }}
        .content {{ padding: 24px; background: white; border: 1px solid #e5e7eb; }}
        .section {{ margin-bottom: 24px; }}
        .section-title {{ font-size: 14px; font-weight: 600; color: #ef4444; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 12px; padding-bottom: 8px; border-bottom: 2px solid #fecaca; }}
        .section-title.medium {{ color: #f59e0b; border-bottom-color: #fde68a; }}
        .thread {{ background: #fafafa; border-radius: 8px; padding: 16px; margin-bottom: 12px; border-left: 4px solid #ef4444; }}
        .thread.medium {{ border-left-color: #f59e0b; }}
        .thread-subject {{ font-weight: 600; color: #111827; margin-bottom: 4px; }}
        .thread-participants {{ font-size: 13px; color: #6b7280; margin-bottom: 12px; }}
        .finding {{ background: white; border-radius: 6px; padding: 10px 12px; margin-bottom: 8px; font-size: 14px; }}
        .finding-icon {{ margin-right: 6px; }}
        .finding-label {{ font-weight: 500; color: #4b5563; }}
        .finding-severity {{ font-size: 11px; padding: 2px 6px; border-radius: 4px; margin-left: 8px; }}
        .severity-high {{ background: #fee2e2; color: #dc2626; }}
        .severity-medium {{ background: #fef3c7; color: #d97706; }}
        .severity-low {{ background: #dbeafe; color: #2563eb; }}
        .thread-link {{ display: inline-block; margin-top: 8px; color: #7c3aed; text-decoration: none; font-size: 13px; }}
        .thread-link:hover {{ text-decoration: underline; }}
        .footer {{ text-align: center; padding: 24px; color: #9ca3af; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>ThreadClear</h1>
        <p>Daily Digest for {digest.Date:MMMM d, yyyy}</p>
    </div>
    
    <div class='stats'>
        <div class='stat'>
            <div class='stat-value'>{digest.TotalThreadsAnalyzed}</div>
            <div class='stat-label'>Threads Scanned</div>
        </div>
        <div class='stat'>
            <div class='stat-value'>{digest.ThreadsNeedingAttention}</div>
            <div class='stat-label'>Need Attention</div>
        </div>
    </div>
    
    <div class='content'>
        {highPriorityHtml}
        {mediumPriorityHtml}
        {summaryText}
    </div>
    
    <div class='footer'>
        <p>You're receiving this because you connected Gmail to ThreadClear.</p>
        <p><a href='https://threadclear.com/settings' style='color: #7c3aed;'>Manage digest settings</a></p>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Generate plain text version for email clients that don't support HTML
        /// </summary>
        public string GenerateDigestPlainText(DigestData digest)
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("THREADCLEAR DAILY DIGEST");
            sb.AppendLine($"Date: {digest.Date:MMMM d, yyyy}");
            sb.AppendLine(new string('=', 50));
            sb.AppendLine();
            sb.AppendLine($"📬 {digest.TotalThreadsAnalyzed} threads scanned");
            sb.AppendLine($"⚠️  {digest.ThreadsNeedingAttention} need attention");
            sb.AppendLine();

            if (digest.HighPriorityThreads.Any())
            {
                sb.AppendLine("🔴 HIGH PRIORITY");
                sb.AppendLine(new string('-', 30));
                foreach (var thread in digest.HighPriorityThreads)
                {
                    AppendThreadPlainText(sb, thread);
                }
                sb.AppendLine();
            }

            if (digest.MediumPriorityThreads.Any())
            {
                sb.AppendLine("🟡 MEDIUM PRIORITY");
                sb.AppendLine(new string('-', 30));
                foreach (var thread in digest.MediumPriorityThreads)
                {
                    AppendThreadPlainText(sb, thread);
                }
            }

            if (digest.CleanThreadCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"✓ {digest.CleanThreadCount} threads look healthy");
            }

            return sb.ToString();
        }

        private string GenerateHighPrioritySection(List<DigestThread> threads)
        {
            if (!threads.Any()) return "";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<div class='section'>");
            sb.AppendLine("<div class='section-title'>🔴 High Priority</div>");

            foreach (var thread in threads)
            {
                sb.AppendLine(GenerateThreadHtml(thread, "high"));
            }

            sb.AppendLine("</div>");
            return sb.ToString();
        }

        private string GenerateMediumPrioritySection(List<DigestThread> threads)
        {
            if (!threads.Any()) return "";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<div class='section'>");
            sb.AppendLine("<div class='section-title medium'>🟡 Medium Priority</div>");

            foreach (var thread in threads)
            {
                sb.AppendLine(GenerateThreadHtml(thread, "medium"));
            }

            sb.AppendLine("</div>");
            return sb.ToString();
        }

        private string GenerateThreadHtml(DigestThread thread, string priority)
        {
            var sb = new System.Text.StringBuilder();
            var cssClass = priority == "high" ? "thread" : "thread medium";

            sb.AppendLine($"<div class='{cssClass}'>");
            sb.AppendLine($"<div class='thread-subject'>{EscapeHtml(thread.Subject)}</div>");
            sb.AppendLine($"<div class='thread-participants'>with {EscapeHtml(string.Join(", ", thread.Participants))}</div>");

            // Unanswered Questions
            foreach (var q in thread.UnansweredQuestions.Take(2))
            {
                sb.AppendLine($"<div class='finding'><span class='finding-icon'>❓</span><span class='finding-label'>Unanswered:</span> \"{EscapeHtml(q.Question ?? "")}\"</div>");
            }

            // Tension Points
            foreach (var tp in thread.TensionPoints.Take(2))
            {
                var severityClass = GetSeverityClass(tp.Severity);
                var severityBadge = !string.IsNullOrEmpty(tp.Severity) 
                    ? $"<span class='finding-severity {severityClass}'>{tp.Severity}</span>" 
                    : "";
                sb.AppendLine($"<div class='finding'><span class='finding-icon'>⚡</span><span class='finding-label'>Tension:</span> {EscapeHtml(tp.Description ?? "")}{severityBadge}</div>");
            }

            // Misalignments
            foreach (var m in thread.Misalignments.Take(2))
            {
                var severityClass = GetSeverityClass(m.Severity);
                var severityBadge = !string.IsNullOrEmpty(m.Severity)
                    ? $"<span class='finding-severity {severityClass}'>{m.Severity}</span>"
                    : "";
                sb.AppendLine($"<div class='finding'><span class='finding-icon'>🔀</span><span class='finding-label'>Misalignment:</span> {EscapeHtml(m.Description ?? "")}{severityBadge}</div>");
                if (!string.IsNullOrEmpty(m.SuggestedResolution))
                {
                    sb.AppendLine($"<div class='finding' style='margin-left: 20px; font-style: italic;'>💡 {EscapeHtml(m.SuggestedResolution)}</div>");
                }
            }

            sb.AppendLine($"<a href='{thread.GmailLink}' class='thread-link'>View in Gmail →</a>");
            sb.AppendLine("</div>");

            return sb.ToString();
        }

        private void AppendThreadPlainText(System.Text.StringBuilder sb, DigestThread thread)
        {
            sb.AppendLine();
            sb.AppendLine($"Thread: \"{thread.Subject}\"");
            sb.AppendLine($"With: {string.Join(", ", thread.Participants)}");

            foreach (var q in thread.UnansweredQuestions.Take(2))
            {
                sb.AppendLine($"  ❓ Unanswered: \"{q.Question}\"");
            }

            foreach (var tp in thread.TensionPoints.Take(2))
            {
                var severity = !string.IsNullOrEmpty(tp.Severity) ? $" [{tp.Severity}]" : "";
                sb.AppendLine($"  ⚡ Tension{severity}: {tp.Description}");
            }

            foreach (var m in thread.Misalignments.Take(2))
            {
                var severity = !string.IsNullOrEmpty(m.Severity) ? $" [{m.Severity}]" : "";
                sb.AppendLine($"  🔀 Misalignment{severity}: {m.Description}");
                if (!string.IsNullOrEmpty(m.SuggestedResolution))
                    sb.AppendLine($"     💡 {m.SuggestedResolution}");
            }

            sb.AppendLine($"  → {thread.GmailLink}");
        }

        private string GetSeverityClass(string? severity)
        {
            return severity?.ToLower() switch
            {
                "high" => "severity-high",
                "medium" => "severity-medium",
                "low" => "severity-low",
                _ => "severity-medium"
            };
        }

        private string EscapeHtml(string text)
        {
            return System.Net.WebUtility.HtmlEncode(text);
        }
    }

    #region Digest Data Models

    public class DigestData
    {
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public int TotalThreadsAnalyzed { get; set; }
        public int ThreadsNeedingAttention => HighPriorityThreads.Count + MediumPriorityThreads.Count;
        public int CleanThreadCount { get; set; }
        public List<DigestThread> HighPriorityThreads { get; set; } = new();
        public List<DigestThread> MediumPriorityThreads { get; set; } = new();
    }

    public class DigestThread
    {
        public string ThreadId { get; set; } = "";
        public string Subject { get; set; } = "";
        public List<string> Participants { get; set; } = new();
        
        // Uses your existing models from ThreadClear.Functions.Models
        public List<UnansweredQuestion> UnansweredQuestions { get; set; } = new();
        public List<TensionPoint> TensionPoints { get; set; } = new();
        public List<Misalignment> Misalignments { get; set; } = new();
        
        public string GmailLink => $"https://mail.google.com/mail/u/0/#inbox/{ThreadId}";
    }

    #endregion
}
