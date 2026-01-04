using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class InsightService : IInsightService
    {
        private readonly IInsightRepository _insightRepository;
        private readonly ITaxonomyService _taxonomyService;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ILogger<InsightService> _logger;

        public InsightService(
            IInsightRepository insightRepository,
            ITaxonomyService taxonomyService,
            IOrganizationRepository organizationRepository,
            ILogger<InsightService> logger)
        {
            _insightRepository = insightRepository;
            _taxonomyService = taxonomyService;
            _organizationRepository = organizationRepository;
            _logger = logger;
        }

        public async Task<StorableInsight> StoreInsight(Guid organizationId, Guid? userId, ThreadCapsule capsule)
        {
            // Get organization's taxonomy for proper categorization
            var taxonomy = await _taxonomyService.GetTaxonomyForOrganization(organizationId);

            // Transform the capsule into a storable insight
            var transformer = new InsightTransformer(taxonomy);
            var insight = transformer.Transform(capsule, organizationId, userId);

            // Store it
            await _insightRepository.Create(insight);

            _logger.LogInformation("Stored insight {InsightId} for org {OrgId} with {Count} entries",
                insight.Id, organizationId, insight.Insights.Count);

            return insight;
        }

        public async Task<List<StorableInsight>> GetRecentInsights(Guid organizationId, int limit = 50)
        {
            return await _insightRepository.GetByOrganization(organizationId, limit);
        }

        public async Task<List<StorableInsight>> GetUserInsights(Guid userId, int limit = 50)
        {
            return await _insightRepository.GetByUser(userId, limit);
        }

        public async Task<StorableInsight?> GetInsight(Guid insightId)
        {
            return await _insightRepository.GetById(insightId);
        }

        public async Task<InsightSummary> GetDashboardSummary(Guid organizationId, int days = 30)
        {
            var since = DateTime.UtcNow.AddDays(-days);
            return await _insightRepository.GetSummary(organizationId, since);
        }

        public async Task<List<InsightTrend>> GetTrendData(Guid organizationId, int days = 30, string groupBy = "day")
        {
            var start = DateTime.UtcNow.AddDays(-days);
            var end = DateTime.UtcNow;
            return await _insightRepository.GetTrends(organizationId, start, end, groupBy);
        }

        public async Task<List<TopicBreakdown>> GetTopicAnalysis(Guid organizationId, int days = 30)
        {
            var since = DateTime.UtcNow.AddDays(-days);
            return await _insightRepository.GetTopicBreakdown(organizationId, since);
        }

        public async Task<int> CleanupOldInsights(Guid organizationId)
        {
            // Get organization's retention policy
            var org = await _organizationRepository.GetById(organizationId);
            var retentionDays = org?.Settings?.InsightRetentionDays ?? 90;

            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            var deleted = await _insightRepository.DeleteOlderThan(organizationId, cutoff);

            if (deleted > 0)
            {
                _logger.LogInformation("Cleaned up {Count} old insights for org {OrgId}", deleted, organizationId);
            }

            return deleted;
        }

        public async Task<List<InsightTrend>> GetInsightTrends(Guid organizationId, int days, string groupBy)
        {
            return await GetTrendData(organizationId, days, groupBy);
        }

        public async Task<List<TopicBreakdown>> GetTopicBreakdown(Guid organizationId, int days)
        {
            return await GetTopicAnalysis(organizationId, days);
        }

        public async Task<StorableInsight?> GetInsightById(Guid insightId)
        {
            return await GetInsight(insightId);
        }
    }
}
