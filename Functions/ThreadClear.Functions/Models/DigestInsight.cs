using System;

namespace ThreadClear.Models
{
    public class DigestInsight
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public string Provider { get; set; }
        public string ThreadId { get; set; }
        public string Subject { get; set; }
        public int HealthScore { get; set; }
        public string RiskLevel { get; set; }      // "Low" | "Medium" | "High"
        public int UnansweredQuestions { get; set; }
        public int TensionSignals { get; set; }
        public string Summary { get; set; }
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    }
}