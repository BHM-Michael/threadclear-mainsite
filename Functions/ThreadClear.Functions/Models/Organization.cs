using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ThreadClear.Functions.Models
{
    public class Organization
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string? Slug { get; set; }
        public string IndustryType { get; set; } = "default";
        public string Plan { get; set; } = "free";
        public string? SettingsJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Parsed settings (not stored directly)
        private OrganizationSettings? _settings;
        public OrganizationSettings Settings
        {
            get
            {
                if (_settings == null && !string.IsNullOrEmpty(SettingsJson))
                {
                    _settings = JsonSerializer.Deserialize<OrganizationSettings>(SettingsJson);
                }
                return _settings ?? new OrganizationSettings();
            }
            set
            {
                _settings = value;
                SettingsJson = JsonSerializer.Serialize(value);
            }
        }
    }

    public class OrganizationSettings
    {
        public bool AllowMemberInvites { get; set; } = false;
        public bool RequireApproval { get; set; } = true;
        public bool StoreInsights { get; set; } = true;
        public int InsightRetentionDays { get; set; } = 90;
        public List<string> AllowedDomains { get; set; } = new();
        public AnalysisDefaults AnalysisDefaults { get; set; } = new();
    }

    public class AnalysisDefaults
    {
        public string DefaultParsingMode { get; set; } = "auto";
        public bool EnableUnansweredQuestions { get; set; } = true;
        public bool EnableTensionPoints { get; set; } = true;
        public bool EnableMisalignments { get; set; } = true;
        public bool EnableConversationHealth { get; set; } = true;
        public bool EnableSuggestedActions { get; set; } = true;
    }
}