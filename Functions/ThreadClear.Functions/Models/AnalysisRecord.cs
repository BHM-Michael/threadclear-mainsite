using System;
using System.Collections.Generic;

namespace ThreadClear.Functions.Models
{
    public class AnalysisRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public string Source { get; set; } = "web";  // web, chrome, gmail, slack, teams, outlook
        public string? ChannelLabel { get; set; }
        public int HealthScore { get; set; }
        public string RiskLevel { get; set; } = "Low";  // Low, Medium, High
        public int ParticipantCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<AnalysisFindingRecord> Findings { get; set; } = new();
    }

    public class AnalysisFindingRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AnalysisId { get; set; }
        public string FindingType { get; set; } = "";  // unanswered_question, tension, misalignment, suggested_action
        public string? Category { get; set; }  // Frustration, Urgency, Repeated Request, etc.
        public string? Severity { get; set; }  // Low, Medium, High
    }
}