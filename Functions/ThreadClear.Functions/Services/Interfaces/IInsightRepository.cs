using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Functions;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IInsightRepository
    {
        // CRUD
        Task<StorableInsight?> GetById(Guid id);
        Task<StorableInsight> Create(StorableInsight insight);
        Task<bool> Delete(Guid id);

        // Query by organization
        Task<List<StorableInsight>> GetByOrganization(Guid organizationId, int limit = 100, int offset = 0);
        Task<List<StorableInsight>> GetByUser(Guid userId, int limit = 100, int offset = 0);

        // Filtered queries
        Task<List<StorableInsight>> GetByDateRange(Guid organizationId, DateTime start, DateTime end);
        Task<List<StorableInsight>> GetByRiskLevel(Guid organizationId, string riskLevel);
        Task<List<StorableInsight>> GetBySourceType(Guid organizationId, string sourceType);

        // Aggregations for dashboards
        Task<InsightSummary> GetSummary(Guid organizationId, DateTime? since = null);
        Task<List<InsightTrend>> GetTrends(Guid organizationId, DateTime start, DateTime end, string groupBy = "day");
        Task<List<TopicBreakdown>> GetTopicBreakdown(Guid organizationId, DateTime? since = null);

        // Cleanup
        Task<int> DeleteOlderThan(Guid organizationId, DateTime cutoff);

        //Task<Insight?> GetByOutlookConversationIdAsync(string orgId, string conversationId);
    }

    // DTOs for aggregations
    public class InsightSummary
    {
        public int TotalConversations { get; set; }
        public int HighRiskCount { get; set; }
        public int MediumRiskCount { get; set; }
        public int LowRiskCount { get; set; }
        public double AverageHealthScore { get; set; }
        public int TotalInsightEntries { get; set; }
        public Dictionary<string, int> ByCategory { get; set; } = new();
        public Dictionary<string, int> BySourceType { get; set; } = new();
    }

    public class InsightTrend
    {
        public DateTime Period { get; set; }
        public int ConversationCount { get; set; }
        public int HighRiskCount { get; set; }
        public double AverageHealthScore { get; set; }
    }

    public class TopicBreakdown
    {
        public string Topic { get; set; } = "";
        public int Count { get; set; }
        public int HighSeverityCount { get; set; }
        public Dictionary<string, int> ByCategory { get; set; } = new();
    }
}