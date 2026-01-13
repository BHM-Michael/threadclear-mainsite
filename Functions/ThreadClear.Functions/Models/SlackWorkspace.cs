using System;

namespace ThreadClear.Functions.Models
{
    /// <summary>
    /// Represents a Slack workspace that has installed the ThreadClear app
    /// </summary>
    public class SlackWorkspace
    {
        public Guid Id { get; set; }
        
        // Slack identifiers
        public string TeamId { get; set; } = string.Empty;      // Slack workspace ID (T12345678)
        public string? TeamName { get; set; }                    // Workspace name
        
        // OAuth tokens
        public string AccessToken { get; set; } = string.Empty;  // Bot token (xoxb-...)
        public string TokenType { get; set; } = "bot";
        public string? Scope { get; set; }                       // Granted scopes
        
        // Installing user info
        public string? InstalledByUserId { get; set; }           // Slack user ID who installed
        public string? InstalledByUserName { get; set; }
        
        // Link to ThreadClear organization (for paid features)
        public Guid? OrganizationId { get; set; }
        
        // Subscription tier
        public string Tier { get; set; } = "free";               // 'free', 'pro', 'enterprise'
        
        // Usage tracking
        public int MonthlyAnalysisCount { get; set; } = 0;       // Analyses this month
        public int MonthlyAnalysisLimit { get; set; } = 20;      // Limit based on tier
        public DateTime? LastAnalysisAt { get; set; }
        public DateTime? UsageResetDate { get; set; }            // When to reset monthly count
        
        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Check if workspace has exceeded their monthly limit
        /// </summary>
        public bool HasExceededLimit()
        {
            // Pro and Enterprise have no limit
            if (Tier == "pro" || Tier == "enterprise")
                return false;
                
            return MonthlyAnalysisCount >= MonthlyAnalysisLimit;
        }
        
        /// <summary>
        /// Get remaining analyses for this month
        /// </summary>
        public int GetRemainingAnalyses()
        {
            if (Tier == "pro" || Tier == "enterprise")
                return int.MaxValue;
                
            return Math.Max(0, MonthlyAnalysisLimit - MonthlyAnalysisCount);
        }
    }
}
