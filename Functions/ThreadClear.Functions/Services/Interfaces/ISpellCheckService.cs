using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Functions;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface ISpellCheckService
    {
        Task<SpellCheckResult> CheckTextAsync(string text);
        Task<List<MessageSpellCheckResult>> CheckMessagesAsync(List<MessageToCheck> messages);
    }

    public class SpellCheckResult
    {
        public string OriginalText { get; set; } = string.Empty;
        public List<SpellCheckIssue> Issues { get; set; } = new List<SpellCheckIssue>();
        public int TotalIssues => Issues.Count;
    }

    public class SpellCheckIssue
    {
        public string Word { get; set; } = string.Empty;
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public string Type { get; set; } = "spelling"; // "spelling" | "grammar" | "style"
        public string Message { get; set; } = string.Empty;
        public List<string> Suggestions { get; set; } = new List<string>();
        public string Severity { get; set; } = "warning"; // "error" | "warning" | "info"
    }

    public class MessageToCheck
    {
        public string MessageId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class MessageSpellCheckResult
    {
        public string MessageId { get; set; } = string.Empty;
        public List<SpellCheckIssue> Issues { get; set; } = new List<SpellCheckIssue>();
    }
}