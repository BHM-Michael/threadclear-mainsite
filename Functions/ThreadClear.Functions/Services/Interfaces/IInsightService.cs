using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IInsightService
    {
        // Store insight from analysis
        Task<StorableInsight> StoreInsight(Guid organizationId, Guid? userId, ThreadCapsule capsule);
        
        // Query insights
        Task<List<StorableInsight>> GetRecentInsights(Guid organizationId, int limit = 50);
        Task<List<StorableInsight>> GetUserInsights(Guid userId, int limit = 50);
        Task<StorableInsight?> GetInsight(Guid insightId);
        
        // Dashboard data
        Task<InsightSummary> GetDashboardSummary(Guid organizationId, int days = 30);
        Task<List<InsightTrend>> GetTrendData(Guid organizationId, int days = 30, string groupBy = "day");
        Task<List<TopicBreakdown>> GetTopicAnalysis(Guid organizationId, int days = 30);
        
        // Cleanup
        Task<int> CleanupOldInsights(Guid organizationId);

        Task<List<InsightTrend>> GetInsightTrends(Guid organizationId, int days, string groupBy);
        Task<List<TopicBreakdown>> GetTopicBreakdown(Guid organizationId, int days);
        Task<StorableInsight?> GetInsightById(Guid insightId);
    }
}
