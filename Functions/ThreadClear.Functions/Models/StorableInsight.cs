using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ThreadClear.Functions.Models
{
    public class StorableInsight
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OrganizationId { get; set; }
        public Guid? UserId { get; set; }
        public string? TeamOrChannel { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string SourceType { get; set; } = "unknown";
        public int ParticipantCount { get; set; }
        public int MessageCount { get; set; }
        public string OverallRisk { get; set; } = "Low";
        public int HealthScore { get; set; }
        public string InsightsJson { get; set; } = "[]";

        // Parsed insights
        private List<InsightEntry>? _insights;
        public List<InsightEntry> Insights
        {
            get
            {
                if (_insights == null && !string.IsNullOrEmpty(InsightsJson))
                {
                    _insights = JsonSerializer.Deserialize<List<InsightEntry>>(InsightsJson);
                }
                return _insights ?? new List<InsightEntry>();
            }
            set
            {
                _insights = value;
                InsightsJson = JsonSerializer.Serialize(value);
            }
        }
    }

    public class InsightEntry
    {
        public string Category { get; set; } = "";
        public string Value { get; set; } = "";
        public string Role { get; set; } = "unknown";
        public string Topic { get; set; } = "general";
        public string Severity { get; set; } = "low";
    }
}