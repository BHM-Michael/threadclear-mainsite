using System;

namespace ThreadClear.Functions.Models
{
    /// <summary>
    /// Represents a Microsoft Teams tenant that has installed the ThreadClear app
    /// </summary>
    public class TeamsWorkspace
    {
        public Guid Id { get; set; }
        
        // Teams identifiers
        public string TenantId { get; set; } = string.Empty;    // Microsoft 365 tenant ID
        public string? TenantName { get; set; }                  // Organization name
        
        // Service URL for sending messages back
        public string? ServiceUrl { get; set; }
        
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
