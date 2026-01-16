using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IUsageService
    {
        /// <summary>
        /// Increment analysis count for a user (and optionally their org)
        /// </summary>
        Task IncrementAnalysisCount(Guid userId, Guid? organizationId);

        /// <summary>
        /// Increment Gmail threads analyzed count
        /// </summary>
        Task IncrementGmailThreads(Guid userId, Guid? organizationId, int count = 1);

        /// <summary>
        /// Increment spell check runs count
        /// </summary>
        Task IncrementSpellChecks(Guid userId, Guid? organizationId, int count = 1);

        /// <summary>
        /// Track AI token usage
        /// </summary>
        Task IncrementTokenUsage(Guid userId, Guid? organizationId, int tokens);

        /// <summary>
        /// Get usage summary for a specific user
        /// </summary>
        Task<UsageSummary> GetUserUsage(Guid userId, DateTime? from = null, DateTime? to = null);

        /// <summary>
        /// Get usage summary for an organization (aggregate of all members)
        /// </summary>
        Task<UsageSummary> GetOrganizationUsage(Guid organizationId, DateTime? from = null, DateTime? to = null);

        /// <summary>
        /// Get daily usage breakdown for a user
        /// </summary>
        Task<List<DailyUsage>> GetUserDailyUsage(Guid userId, int days = 30);

        /// <summary>
        /// Get daily usage breakdown for an organization
        /// </summary>
        Task<List<DailyUsage>> GetOrganizationDailyUsage(Guid organizationId, int days = 30);

        /// <summary>
        /// Check if user has exceeded their plan limits
        /// </summary>
        Task<UsageLimitCheck> CheckUserLimits(Guid userId);
    }

    public class UsageSummary
    {
        public int TotalAnalyses { get; set; }
        public int GmailThreadsAnalyzed { get; set; }
        public int SpellChecksRun { get; set; }
        public int AITokensUsed { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
    }

    public class DailyUsage
    {
        public DateTime Date { get; set; }
        public int AnalysisCount { get; set; }
        public int GmailThreadsAnalyzed { get; set; }
        public int SpellChecksRun { get; set; }
        public int AITokensUsed { get; set; }
    }

    public class UsageLimitCheck
    {
        public bool IsWithinLimits { get; set; }
        public int AnalysesUsed { get; set; }
        public int AnalysesLimit { get; set; }
        public int AnalysesRemaining => Math.Max(0, AnalysesLimit - AnalysesUsed);
        public string? LimitMessage { get; set; }
        public DateTime ResetDate { get; set; }
    }
}