using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class InsightRepository : IInsightRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<InsightRepository> _logger;

        public InsightRepository(string connectionString, ILogger<InsightRepository> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task<StorableInsight?> GetById(Guid id)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, OrganizationId, UserId, TeamOrChannel, Timestamp, SourceType,
                               ParticipantCount, MessageCount, OverallRisk, HealthScore, Insights
                        FROM StorableInsights 
                        WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapInsight(reader);
            }
            return null;
        }

        public async Task<StorableInsight> Create(StorableInsight insight)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"INSERT INTO StorableInsights 
                        (Id, OrganizationId, UserId, TeamOrChannel, Timestamp, SourceType, 
                         ParticipantCount, MessageCount, OverallRisk, HealthScore, Insights)
                        VALUES (@Id, @OrganizationId, @UserId, @TeamOrChannel, @Timestamp, @SourceType,
                                @ParticipantCount, @MessageCount, @OverallRisk, @HealthScore, @Insights)";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", insight.Id);
            cmd.Parameters.AddWithValue("@OrganizationId", insight.OrganizationId);
            cmd.Parameters.AddWithValue("@UserId", (object?)insight.UserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TeamOrChannel", (object?)insight.TeamOrChannel ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Timestamp", insight.Timestamp);
            cmd.Parameters.AddWithValue("@SourceType", insight.SourceType);
            cmd.Parameters.AddWithValue("@ParticipantCount", insight.ParticipantCount);
            cmd.Parameters.AddWithValue("@MessageCount", insight.MessageCount);
            cmd.Parameters.AddWithValue("@OverallRisk", insight.OverallRisk);
            cmd.Parameters.AddWithValue("@HealthScore", insight.HealthScore);
            cmd.Parameters.AddWithValue("@Insights", insight.InsightsJson);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Created insight {Id} for org {OrgId}", insight.Id, insight.OrganizationId);

            return insight;
        }

        public async Task<bool> Delete(Guid id)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "DELETE FROM StorableInsights WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", id);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        public async Task<List<StorableInsight>> GetByOrganization(Guid organizationId, int limit = 100, int offset = 0)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, OrganizationId, UserId, TeamOrChannel, Timestamp, SourceType,
                               ParticipantCount, MessageCount, OverallRisk, HealthScore, Insights
                        FROM StorableInsights 
                        WHERE OrganizationId = @OrganizationId
                        ORDER BY Timestamp DESC
                        OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@OrganizationId", organizationId);
            cmd.Parameters.AddWithValue("@Offset", offset);
            cmd.Parameters.AddWithValue("@Limit", limit);

            var insights = new List<StorableInsight>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                insights.Add(MapInsight(reader));
            }
            return insights;
        }

        public async Task<List<StorableInsight>> GetByUser(Guid userId, int limit = 100, int offset = 0)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, OrganizationId, UserId, TeamOrChannel, Timestamp, SourceType,
                               ParticipantCount, MessageCount, OverallRisk, HealthScore, Insights
                        FROM StorableInsights 
                        WHERE UserId = @UserId
                        ORDER BY Timestamp DESC
                        OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Offset", offset);
            cmd.Parameters.AddWithValue("@Limit", limit);

            var insights = new List<StorableInsight>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                insights.Add(MapInsight(reader));
            }
            return insights;
        }

        public async Task<List<StorableInsight>> GetByDateRange(Guid organizationId, DateTime start, DateTime end)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, OrganizationId, UserId, TeamOrChannel, Timestamp, SourceType,
                               ParticipantCount, MessageCount, OverallRisk, HealthScore, Insights
                        FROM StorableInsights 
                        WHERE OrganizationId = @OrganizationId 
                          AND Timestamp >= @Start AND Timestamp <= @End
                        ORDER BY Timestamp DESC";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@OrganizationId", organizationId);
            cmd.Parameters.AddWithValue("@Start", start);
            cmd.Parameters.AddWithValue("@End", end);

            var insights = new List<StorableInsight>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                insights.Add(MapInsight(reader));
            }
            return insights;
        }

        public async Task<List<StorableInsight>> GetByRiskLevel(Guid organizationId, string riskLevel)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, OrganizationId, UserId, TeamOrChannel, Timestamp, SourceType,
                               ParticipantCount, MessageCount, OverallRisk, HealthScore, Insights
                        FROM StorableInsights 
                        WHERE OrganizationId = @OrganizationId AND OverallRisk = @RiskLevel
                        ORDER BY Timestamp DESC";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@OrganizationId", organizationId);
            cmd.Parameters.AddWithValue("@RiskLevel", riskLevel);

            var insights = new List<StorableInsight>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                insights.Add(MapInsight(reader));
            }
            return insights;
        }

        public async Task<List<StorableInsight>> GetBySourceType(Guid organizationId, string sourceType)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, OrganizationId, UserId, TeamOrChannel, Timestamp, SourceType,
                               ParticipantCount, MessageCount, OverallRisk, HealthScore, Insights
                        FROM StorableInsights 
                        WHERE OrganizationId = @OrganizationId AND SourceType = @SourceType
                        ORDER BY Timestamp DESC";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@OrganizationId", organizationId);
            cmd.Parameters.AddWithValue("@SourceType", sourceType);

            var insights = new List<StorableInsight>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                insights.Add(MapInsight(reader));
            }
            return insights;
        }

        public async Task<InsightSummary> GetSummary(Guid organizationId, DateTime? since = null)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var whereClause = "WHERE OrganizationId = @OrganizationId";
            if (since.HasValue)
            {
                whereClause += " AND Timestamp >= @Since";
            }

            var sql = $@"SELECT 
                COUNT(*) as TotalConversations,
                ISNULL(SUM(CASE WHEN OverallRisk = 'High' THEN 1 ELSE 0 END), 0) as HighRiskCount,
                ISNULL(SUM(CASE WHEN OverallRisk = 'Medium' THEN 1 ELSE 0 END), 0) as MediumRiskCount,
                ISNULL(SUM(CASE WHEN OverallRisk = 'Low' THEN 1 ELSE 0 END), 0) as LowRiskCount,
                ISNULL(AVG(CAST(HealthScore as FLOAT)), 0) as AverageHealthScore
            FROM StorableInsights
            {whereClause}";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@OrganizationId", organizationId);
            if (since.HasValue)
            {
                cmd.Parameters.AddWithValue("@Since", since.Value);
            }

            var summary = new InsightSummary();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                summary.TotalConversations = reader.GetInt32(0);
                summary.HighRiskCount = reader.GetInt32(1);
                summary.MediumRiskCount = reader.GetInt32(2);
                summary.LowRiskCount = reader.GetInt32(3);
                summary.AverageHealthScore = reader.IsDBNull(4) ? 0 : reader.GetDouble(4);
            }
            await reader.CloseAsync();

            // Get source type breakdown
            var sourceTypeSql = $@"SELECT SourceType, COUNT(*) as Count
                                   FROM StorableInsights 
                                   {whereClause}
                                   GROUP BY SourceType";

            using var sourceCmd = new SqlCommand(sourceTypeSql, connection);
            sourceCmd.Parameters.AddWithValue("@OrganizationId", organizationId);
            if (since.HasValue)
            {
                sourceCmd.Parameters.AddWithValue("@Since", since.Value);
            }

            using var sourceReader = await sourceCmd.ExecuteReaderAsync();
            while (await sourceReader.ReadAsync())
            {
                summary.BySourceType[sourceReader.GetString(0)] = sourceReader.GetInt32(1);
            }

            return summary;
        }

        public async Task<List<InsightTrend>> GetTrends(Guid organizationId, DateTime start, DateTime end, string groupBy = "day")
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var dateFormat = groupBy.ToLower() switch
            {
                "hour" => "DATEADD(HOUR, DATEDIFF(HOUR, 0, Timestamp), 0)",
                "week" => "DATEADD(WEEK, DATEDIFF(WEEK, 0, Timestamp), 0)",
                "month" => "DATEADD(MONTH, DATEDIFF(MONTH, 0, Timestamp), 0)",
                _ => "CAST(Timestamp AS DATE)" // day
            };

            var sql = $@"SELECT 
                            {dateFormat} as Period,
                            COUNT(*) as ConversationCount,
                            SUM(CASE WHEN OverallRisk = 'High' THEN 1 ELSE 0 END) as HighRiskCount,
                            AVG(CAST(HealthScore as FLOAT)) as AverageHealthScore
                        FROM StorableInsights 
                        WHERE OrganizationId = @OrganizationId 
                          AND Timestamp >= @Start AND Timestamp <= @End
                        GROUP BY {dateFormat}
                        ORDER BY Period";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@OrganizationId", organizationId);
            cmd.Parameters.AddWithValue("@Start", start);
            cmd.Parameters.AddWithValue("@End", end);

            var trends = new List<InsightTrend>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                trends.Add(new InsightTrend
                {
                    Period = reader.GetDateTime(0),
                    ConversationCount = reader.GetInt32(1),
                    HighRiskCount = reader.GetInt32(2),
                    AverageHealthScore = reader.IsDBNull(3) ? 0 : reader.GetDouble(3)
                });
            }
            return trends;
        }

        public async Task<List<TopicBreakdown>> GetTopicBreakdown(Guid organizationId, DateTime? since = null)
        {
            // This requires parsing JSON in SQL or doing it in memory
            // For simplicity, we'll fetch insights and aggregate in memory
            var insights = since.HasValue
                ? await GetByDateRange(organizationId, since.Value, DateTime.UtcNow)
                : await GetByOrganization(organizationId, 1000);

            var topicStats = new Dictionary<string, TopicBreakdown>();

            foreach (var insight in insights)
            {
                foreach (var entry in insight.Insights)
                {
                    if (!topicStats.ContainsKey(entry.Topic))
                    {
                        topicStats[entry.Topic] = new TopicBreakdown
                        {
                            Topic = entry.Topic,
                            Count = 0,
                            HighSeverityCount = 0,
                            ByCategory = new Dictionary<string, int>()
                        };
                    }

                    var stats = topicStats[entry.Topic];
                    stats.Count++;

                    if (entry.Severity.Equals("high", StringComparison.OrdinalIgnoreCase))
                    {
                        stats.HighSeverityCount++;
                    }

                    if (!stats.ByCategory.ContainsKey(entry.Category))
                    {
                        stats.ByCategory[entry.Category] = 0;
                    }
                    stats.ByCategory[entry.Category]++;
                }
            }

            return topicStats.Values.OrderByDescending(t => t.Count).ToList();
        }

        public async Task<int> DeleteOlderThan(Guid organizationId, DateTime cutoff)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "DELETE FROM StorableInsights WHERE OrganizationId = @OrganizationId AND Timestamp < @Cutoff";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@OrganizationId", organizationId);
            cmd.Parameters.AddWithValue("@Cutoff", cutoff);

            var rows = await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Deleted {Count} insights older than {Cutoff} for org {OrgId}", rows, cutoff, organizationId);

            return rows;
        }

        private StorableInsight MapInsight(SqlDataReader reader)
        {
            return new StorableInsight
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                OrganizationId = reader.GetGuid(reader.GetOrdinal("OrganizationId")),
                UserId = reader.IsDBNull(reader.GetOrdinal("UserId")) ? null : reader.GetGuid(reader.GetOrdinal("UserId")),
                TeamOrChannel = reader.IsDBNull(reader.GetOrdinal("TeamOrChannel")) ? null : reader.GetString(reader.GetOrdinal("TeamOrChannel")),
                Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp")),
                SourceType = reader.GetString(reader.GetOrdinal("SourceType")),
                ParticipantCount = reader.GetInt32(reader.GetOrdinal("ParticipantCount")),
                MessageCount = reader.GetInt32(reader.GetOrdinal("MessageCount")),
                OverallRisk = reader.GetString(reader.GetOrdinal("OverallRisk")),
                HealthScore = reader.GetInt32(reader.GetOrdinal("HealthScore")),
                InsightsJson = reader.GetString(reader.GetOrdinal("Insights"))
            };
        }
    }
}