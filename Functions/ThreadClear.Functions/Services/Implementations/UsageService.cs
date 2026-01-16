using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class UsageService : IUsageService
    {
        private readonly string _connectionString;
        private readonly ILogger<UsageService> _logger;

        // Default limits for free tier
        private const int FREE_TIER_MONTHLY_ANALYSES = 10;

        public UsageService(string connectionString, ILogger<UsageService> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task IncrementAnalysisCount(Guid userId, Guid? organizationId)
        {
            await IncrementCounter(userId, organizationId, "AnalysisCount", 1);
        }

        public async Task IncrementGmailThreads(Guid userId, Guid? organizationId, int count = 1)
        {
            await IncrementCounter(userId, organizationId, "GmailThreadsAnalyzed", count);
        }

        public async Task IncrementSpellChecks(Guid userId, Guid? organizationId, int count = 1)
        {
            await IncrementCounter(userId, organizationId, "SpellChecksRun", count);
        }

        public async Task IncrementTokenUsage(Guid userId, Guid? organizationId, int tokens)
        {
            await IncrementCounter(userId, organizationId, "AITokensUsed", tokens);
        }

        private async Task IncrementCounter(Guid userId, Guid? organizationId, string column, int amount)
        {
            var today = DateTime.UtcNow.Date;

            // Upsert: insert if not exists, update if exists
            var sql = $@"
                MERGE UsageTracking AS target
                USING (SELECT @UserId AS UserId, @Period AS Period) AS source
                ON target.UserId = source.UserId AND target.Period = source.Period
                WHEN MATCHED THEN
                    UPDATE SET 
                        {column} = {column} + @Amount,
                        UpdatedAt = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (Id, UserId, OrganizationId, Period, {column}, CreatedAt, UpdatedAt)
                    VALUES (NEWID(), @UserId, @OrganizationId, @Period, @Amount, SYSUTCDATETIME(), SYSUTCDATETIME());
            ";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@UserId", userId);
                command.Parameters.AddWithValue("@OrganizationId", (object?)organizationId ?? DBNull.Value);
                command.Parameters.AddWithValue("@Period", today);
                command.Parameters.AddWithValue("@Amount", amount);

                await command.ExecuteNonQueryAsync();

                _logger.LogDebug("Incremented {Column} by {Amount} for user {UserId}", column, amount, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to increment {Column} for user {UserId}", column, userId);
                // Don't throw - usage tracking failure shouldn't block the main operation
            }
        }

        public async Task<UsageSummary> GetUserUsage(Guid userId, DateTime? from = null, DateTime? to = null)
        {
            var periodStart = from ?? DateTime.UtcNow.AddDays(-30);
            var periodEnd = to ?? DateTime.UtcNow;

            var sql = @"
                SELECT 
                    ISNULL(SUM(AnalysisCount), 0) AS TotalAnalyses,
                    ISNULL(SUM(GmailThreadsAnalyzed), 0) AS GmailThreadsAnalyzed,
                    ISNULL(SUM(SpellChecksRun), 0) AS SpellChecksRun,
                    ISNULL(SUM(AITokensUsed), 0) AS AITokensUsed
                FROM UsageTracking
                WHERE UserId = @UserId
                  AND Period >= @PeriodStart
                  AND Period <= @PeriodEnd
            ";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@PeriodStart", periodStart.Date);
            command.Parameters.AddWithValue("@PeriodEnd", periodEnd.Date);

            using var reader = await command.ExecuteReaderAsync();

            var summary = new UsageSummary
            {
                PeriodStart = periodStart,
                PeriodEnd = periodEnd
            };

            if (await reader.ReadAsync())
            {
                summary.TotalAnalyses = reader.GetInt32(0);
                summary.GmailThreadsAnalyzed = reader.GetInt32(1);
                summary.SpellChecksRun = reader.GetInt32(2);
                summary.AITokensUsed = reader.GetInt32(3);
            }

            return summary;
        }

        public async Task<UsageSummary> GetOrganizationUsage(Guid organizationId, DateTime? from = null, DateTime? to = null)
        {
            var periodStart = from ?? DateTime.UtcNow.AddDays(-30);
            var periodEnd = to ?? DateTime.UtcNow;

            var sql = @"
                SELECT 
                    ISNULL(SUM(AnalysisCount), 0) AS TotalAnalyses,
                    ISNULL(SUM(GmailThreadsAnalyzed), 0) AS GmailThreadsAnalyzed,
                    ISNULL(SUM(SpellChecksRun), 0) AS SpellChecksRun,
                    ISNULL(SUM(AITokensUsed), 0) AS AITokensUsed
                FROM UsageTracking
                WHERE OrganizationId = @OrganizationId
                  AND Period >= @PeriodStart
                  AND Period <= @PeriodEnd
            ";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@OrganizationId", organizationId);
            command.Parameters.AddWithValue("@PeriodStart", periodStart.Date);
            command.Parameters.AddWithValue("@PeriodEnd", periodEnd.Date);

            using var reader = await command.ExecuteReaderAsync();

            var summary = new UsageSummary
            {
                PeriodStart = periodStart,
                PeriodEnd = periodEnd
            };

            if (await reader.ReadAsync())
            {
                summary.TotalAnalyses = reader.GetInt32(0);
                summary.GmailThreadsAnalyzed = reader.GetInt32(1);
                summary.SpellChecksRun = reader.GetInt32(2);
                summary.AITokensUsed = reader.GetInt32(3);
            }

            return summary;
        }

        public async Task<List<DailyUsage>> GetUserDailyUsage(Guid userId, int days = 30)
        {
            var since = DateTime.UtcNow.AddDays(-days).Date;

            var sql = @"
                SELECT 
                    Period,
                    AnalysisCount,
                    GmailThreadsAnalyzed,
                    SpellChecksRun,
                    AITokensUsed
                FROM UsageTracking
                WHERE UserId = @UserId
                  AND Period >= @Since
                ORDER BY Period ASC
            ";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Since", since);

            using var reader = await command.ExecuteReaderAsync();

            var results = new List<DailyUsage>();
            while (await reader.ReadAsync())
            {
                results.Add(new DailyUsage
                {
                    Date = reader.GetDateTime(0),
                    AnalysisCount = reader.GetInt32(1),
                    GmailThreadsAnalyzed = reader.GetInt32(2),
                    SpellChecksRun = reader.GetInt32(3),
                    AITokensUsed = reader.GetInt32(4)
                });
            }

            return results;
        }

        public async Task<List<DailyUsage>> GetOrganizationDailyUsage(Guid organizationId, int days = 30)
        {
            var since = DateTime.UtcNow.AddDays(-days).Date;

            var sql = @"
                SELECT 
                    Period,
                    SUM(AnalysisCount) AS AnalysisCount,
                    SUM(GmailThreadsAnalyzed) AS GmailThreadsAnalyzed,
                    SUM(SpellChecksRun) AS SpellChecksRun,
                    SUM(AITokensUsed) AS AITokensUsed
                FROM UsageTracking
                WHERE OrganizationId = @OrganizationId
                  AND Period >= @Since
                GROUP BY Period
                ORDER BY Period ASC
            ";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@OrganizationId", organizationId);
            command.Parameters.AddWithValue("@Since", since);

            using var reader = await command.ExecuteReaderAsync();

            var results = new List<DailyUsage>();
            while (await reader.ReadAsync())
            {
                results.Add(new DailyUsage
                {
                    Date = reader.GetDateTime(0),
                    AnalysisCount = reader.GetInt32(1),
                    GmailThreadsAnalyzed = reader.GetInt32(2),
                    SpellChecksRun = reader.GetInt32(3),
                    AITokensUsed = reader.GetInt32(4)
                });
            }

            return results;
        }

        public async Task<UsageLimitCheck> CheckUserLimits(Guid userId)
        {
            // Get current month's usage
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var nextMonth = monthStart.AddMonths(1);

            var usage = await GetUserUsage(userId, monthStart, DateTime.UtcNow);

            // TODO: Get user's plan limits from subscription table
            // For now, use free tier limits
            var limit = FREE_TIER_MONTHLY_ANALYSES;

            var check = new UsageLimitCheck
            {
                AnalysesUsed = usage.TotalAnalyses,
                AnalysesLimit = limit,
                IsWithinLimits = usage.TotalAnalyses < limit,
                ResetDate = nextMonth
            };

            if (!check.IsWithinLimits)
            {
                check.LimitMessage = $"You've used all {limit} analyses this month. Upgrade for unlimited access.";
            }

            return check;
        }
    }
}