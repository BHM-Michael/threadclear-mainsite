using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;
using ThreadClear.Models;

namespace ThreadClear.Functions.Services.Implementations
{
    public class DigestInsightRepository : IDigestInsightRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<DigestInsightRepository> _logger;

        public DigestInsightRepository(string connectionString, ILogger<DigestInsightRepository> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task CreateAsync(DigestInsight insight)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"INSERT INTO DigestInsights 
                            (UserId, Provider, ThreadId, Subject, HealthScore, RiskLevel,
                             UnansweredQuestions, TensionSignals, Summary, AnalyzedAt, DigestSent)
                        VALUES 
                            (@UserId, @Provider, @ThreadId, @Subject, @HealthScore, @RiskLevel,
                             @UnansweredQuestions, @TensionSignals, @Summary, @AnalyzedAt, 0)";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", insight.UserId);
            cmd.Parameters.AddWithValue("@Provider", insight.Provider);
            cmd.Parameters.AddWithValue("@ThreadId", insight.ThreadId);
            cmd.Parameters.AddWithValue("@Subject", (object?)insight.Subject ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@HealthScore", insight.HealthScore);
            cmd.Parameters.AddWithValue("@RiskLevel", insight.RiskLevel);
            cmd.Parameters.AddWithValue("@UnansweredQuestions", insight.UnansweredQuestions);
            cmd.Parameters.AddWithValue("@TensionSignals", insight.TensionSignals);
            cmd.Parameters.AddWithValue("@Summary", (object?)insight.Summary ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AnalyzedAt", insight.AnalyzedAt);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Created DigestInsight for thread {ThreadId} user {UserId}",
                insight.ThreadId, insight.UserId);
        }

        public async Task<List<DigestInsight>> GetPendingInsightsForUserAsync(Guid userId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, UserId, Provider, ThreadId, Subject, HealthScore, RiskLevel,
                               UnansweredQuestions, TensionSignals, Summary, AnalyzedAt, DigestSent, SentAt
                        FROM DigestInsights
                        WHERE UserId = @UserId AND DigestSent = 0
                        ORDER BY HealthScore ASC";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);

            var results = new List<DigestInsight>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                results.Add(MapDigestInsight(reader));

            return results;
        }

        public async Task<List<Guid>> GetUsersWithPendingInsightsAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT DISTINCT UserId FROM DigestInsights WHERE DigestSent = 0";

            using var cmd = new SqlCommand(sql, connection);

            var results = new List<Guid>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                results.Add(Guid.Parse(reader.GetString(0)));

            return results;
        }

        public async Task MarkSentAsync(List<int> ids)
        {
            if (ids == null || ids.Count == 0) return;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Build parameterized IN clause — no Dapper, do it manually
            var paramNames = new List<string>();
            using var cmd = new SqlCommand();
            cmd.Connection = connection;

            for (int i = 0; i < ids.Count; i++)
            {
                var paramName = $"@Id{i}";
                paramNames.Add(paramName);
                cmd.Parameters.AddWithValue(paramName, ids[i]);
            }

            cmd.CommandText = $@"UPDATE DigestInsights 
                                 SET DigestSent = 1, SentAt = GETUTCDATE()
                                 WHERE Id IN ({string.Join(",", paramNames)})";

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Marked {Count} DigestInsights as sent", ids.Count);
        }

        private static DigestInsight MapDigestInsight(SqlDataReader reader)
        {
            return new DigestInsight
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
                Provider = reader.GetString(reader.GetOrdinal("Provider")),
                ThreadId = reader.GetString(reader.GetOrdinal("ThreadId")),
                Subject = reader.IsDBNull(reader.GetOrdinal("Subject"))
                    ? null : reader.GetString(reader.GetOrdinal("Subject")),
                HealthScore = reader.GetInt32(reader.GetOrdinal("HealthScore")),
                RiskLevel = reader.GetString(reader.GetOrdinal("RiskLevel")),
                UnansweredQuestions = reader.GetInt32(reader.GetOrdinal("UnansweredQuestions")),
                TensionSignals = reader.GetInt32(reader.GetOrdinal("TensionSignals")),
                Summary = reader.IsDBNull(reader.GetOrdinal("Summary"))
                    ? null : reader.GetString(reader.GetOrdinal("Summary")),
                AnalyzedAt = reader.GetDateTime(reader.GetOrdinal("AnalyzedAt")),
                DigestSent = reader.GetBoolean(reader.GetOrdinal("DigestSent")),
                SentAt = reader.IsDBNull(reader.GetOrdinal("SentAt"))
                    ? null : reader.GetDateTime(reader.GetOrdinal("SentAt"))
            };
        }
    }
}