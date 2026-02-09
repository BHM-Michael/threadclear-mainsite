using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class AnalysisResultService : IAnalysisResultService
    {
        private readonly string _connectionString;

        public AnalysisResultService(IConfiguration configuration)
        {
            _connectionString = configuration["SqlConnectionString"]
                ?? throw new InvalidOperationException("SqlConnectionString not configured");
        }

        public async Task<Guid> SaveAsync(AnalysisRecord result)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var resultSql = @"
                    INSERT INTO AnalysisResults (Id, UserId, OrganizationId, Source, ChannelLabel, HealthScore, RiskLevel, ParticipantCount, CreatedAt)
                    VALUES (@Id, @UserId, @OrganizationId, @Source, @ChannelLabel, @HealthScore, @RiskLevel, @ParticipantCount, @CreatedAt)";

                using (var cmd = new SqlCommand(resultSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", result.Id);
                    cmd.Parameters.AddWithValue("@UserId", result.UserId);
                    cmd.Parameters.AddWithValue("@OrganizationId", (object?)result.OrganizationId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Source", result.Source);
                    cmd.Parameters.AddWithValue("@ChannelLabel", (object?)result.ChannelLabel ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@HealthScore", result.HealthScore);
                    cmd.Parameters.AddWithValue("@RiskLevel", result.RiskLevel);
                    cmd.Parameters.AddWithValue("@ParticipantCount", result.ParticipantCount);
                    cmd.Parameters.AddWithValue("@CreatedAt", result.CreatedAt);
                    await cmd.ExecuteNonQueryAsync();
                }

                foreach (var finding in result.Findings)
                {
                    var findingSql = @"
                        INSERT INTO AnalysisFindings (Id, AnalysisId, FindingType, Category, Severity)
                        VALUES (@Id, @AnalysisId, @FindingType, @Category, @Severity)";

                    using var cmd = new SqlCommand(findingSql, connection, transaction);
                    cmd.Parameters.AddWithValue("@Id", finding.Id);
                    cmd.Parameters.AddWithValue("@AnalysisId", result.Id);
                    cmd.Parameters.AddWithValue("@FindingType", finding.FindingType);
                    cmd.Parameters.AddWithValue("@Category", (object?)finding.Category ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Severity", (object?)finding.Severity ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return result.Id;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<AnalysisRecord>> GetByUserAsync(Guid userId, int limit = 50)
        {
            var results = new List<AnalysisRecord>();
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT TOP (@Limit) Id, UserId, OrganizationId, Source, ChannelLabel, HealthScore, RiskLevel, ParticipantCount, CreatedAt
                FROM AnalysisResults
                WHERE UserId = @UserId
                ORDER BY CreatedAt DESC";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Limit", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapResult(reader));
            }

            foreach (var result in results)
            {
                result.Findings = await GetFindingsAsync(connection, result.Id);
            }

            return results;
        }

        public async Task<List<AnalysisRecord>> GetByOrganizationAsync(Guid organizationId, int limit = 100)
        {
            var results = new List<AnalysisRecord>();
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT TOP (@Limit) Id, UserId, OrganizationId, Source, ChannelLabel, HealthScore, RiskLevel, ParticipantCount, CreatedAt
                FROM AnalysisResults
                WHERE OrganizationId = @OrganizationId
                ORDER BY CreatedAt DESC";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@OrganizationId", organizationId);
            cmd.Parameters.AddWithValue("@Limit", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapResult(reader));
            }

            foreach (var result in results)
            {
                result.Findings = await GetFindingsAsync(connection, result.Id);
            }

            return results;
        }

        public async Task<AnalysisRecord?> GetByIdAsync(Guid id)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT Id, UserId, OrganizationId, Source, ChannelLabel, HealthScore, RiskLevel, ParticipantCount, CreatedAt
                FROM AnalysisResults
                WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var result = MapResult(reader);
                result.Findings = await GetFindingsAsync(connection, result.Id);
                return result;
            }

            return null;
        }

        private async Task<List<AnalysisFindingRecord>> GetFindingsAsync(SqlConnection connection, Guid analysisId)
        {
            var findings = new List<AnalysisFindingRecord>();
            var sql = "SELECT Id, AnalysisId, FindingType, Category, Severity FROM AnalysisFindings WHERE AnalysisId = @AnalysisId";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@AnalysisId", analysisId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                findings.Add(new AnalysisFindingRecord
                {
                    Id = reader.GetGuid(0),
                    AnalysisId = reader.GetGuid(1),
                    FindingType = reader.GetString(2),
                    Category = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Severity = reader.IsDBNull(4) ? null : reader.GetString(4)
                });
            }

            return findings;
        }

        private AnalysisRecord MapResult(SqlDataReader reader)
        {
            return new AnalysisRecord
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                OrganizationId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                Source = reader.GetString(3),
                ChannelLabel = reader.IsDBNull(4) ? null : reader.GetString(4),
                HealthScore = reader.GetInt32(5),
                RiskLevel = reader.GetString(6),
                ParticipantCount = reader.GetInt32(7),
                CreatedAt = reader.GetDateTime(8)
            };
        }
    }
}