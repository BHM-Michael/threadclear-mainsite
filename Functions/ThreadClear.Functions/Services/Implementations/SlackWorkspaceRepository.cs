using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class SlackWorkspaceRepository : ISlackWorkspaceRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<SlackWorkspaceRepository> _logger;

        public SlackWorkspaceRepository(IConfiguration configuration, ILogger<SlackWorkspaceRepository> logger)
        {
            _connectionString = configuration["SqlConnectionString"]
                ?? throw new InvalidOperationException("Connection string not found");
            _logger = logger;
        }

        public async Task<SlackWorkspace?> GetByTeamIdAsync(string teamId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT Id, TeamId, TeamName, AccessToken, TokenType, Scope,
                       InstalledByUserId, InstalledByUserName, OrganizationId,
                       Tier, MonthlyAnalysisCount, MonthlyAnalysisLimit,
                       LastAnalysisAt, UsageResetDate, CreatedAt, UpdatedAt, IsActive
                FROM SlackWorkspaces
                WHERE TeamId = @TeamId AND IsActive = 1";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TeamId", teamId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapFromReader(reader);
            }

            return null;
        }

        public async Task<SlackWorkspace> CreateAsync(SlackWorkspace workspace)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            workspace.Id = Guid.NewGuid();
            workspace.CreatedAt = DateTime.UtcNow;
            workspace.UpdatedAt = DateTime.UtcNow;
            workspace.UsageResetDate = GetNextMonthStart();

            var sql = @"
                INSERT INTO SlackWorkspaces 
                (Id, TeamId, TeamName, AccessToken, TokenType, Scope,
                 InstalledByUserId, InstalledByUserName, OrganizationId,
                 Tier, MonthlyAnalysisCount, MonthlyAnalysisLimit,
                 LastAnalysisAt, UsageResetDate, CreatedAt, UpdatedAt, IsActive)
                VALUES 
                (@Id, @TeamId, @TeamName, @AccessToken, @TokenType, @Scope,
                 @InstalledByUserId, @InstalledByUserName, @OrganizationId,
                 @Tier, @MonthlyAnalysisCount, @MonthlyAnalysisLimit,
                 @LastAnalysisAt, @UsageResetDate, @CreatedAt, @UpdatedAt, @IsActive)";

            using var command = new SqlCommand(sql, connection);
            AddParameters(command, workspace);

            await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Created Slack workspace {TeamId} ({TeamName})", workspace.TeamId, workspace.TeamName);

            return workspace;
        }

        public async Task<SlackWorkspace> UpdateAsync(SlackWorkspace workspace)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            workspace.UpdatedAt = DateTime.UtcNow;

            var sql = @"
                UPDATE SlackWorkspaces SET
                    TeamName = @TeamName,
                    AccessToken = @AccessToken,
                    TokenType = @TokenType,
                    Scope = @Scope,
                    InstalledByUserId = @InstalledByUserId,
                    InstalledByUserName = @InstalledByUserName,
                    OrganizationId = @OrganizationId,
                    Tier = @Tier,
                    MonthlyAnalysisCount = @MonthlyAnalysisCount,
                    MonthlyAnalysisLimit = @MonthlyAnalysisLimit,
                    LastAnalysisAt = @LastAnalysisAt,
                    UsageResetDate = @UsageResetDate,
                    UpdatedAt = @UpdatedAt,
                    IsActive = @IsActive
                WHERE Id = @Id";

            using var command = new SqlCommand(sql, connection);
            AddParameters(command, workspace);

            await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Updated Slack workspace {TeamId}", workspace.TeamId);

            return workspace;
        }

        public async Task<bool> IncrementUsageAsync(string teamId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // First check if we need to reset the monthly count
            var checkSql = @"
                UPDATE SlackWorkspaces 
                SET MonthlyAnalysisCount = 0, 
                    UsageResetDate = @NextMonth
                WHERE TeamId = @TeamId 
                  AND UsageResetDate < GETUTCDATE()";

            using (var checkCommand = new SqlCommand(checkSql, connection))
            {
                checkCommand.Parameters.AddWithValue("@TeamId", teamId);
                checkCommand.Parameters.AddWithValue("@NextMonth", GetNextMonthStart());
                await checkCommand.ExecuteNonQueryAsync();
            }

            // Now increment the counter
            var sql = @"
                UPDATE SlackWorkspaces 
                SET MonthlyAnalysisCount = MonthlyAnalysisCount + 1,
                    LastAnalysisAt = GETUTCDATE(),
                    UpdatedAt = GETUTCDATE()
                WHERE TeamId = @TeamId AND IsActive = 1";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TeamId", teamId);

            var rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }

        public async Task ResetMonthlyUsageAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                UPDATE SlackWorkspaces 
                SET MonthlyAnalysisCount = 0,
                    UsageResetDate = @NextMonth,
                    UpdatedAt = GETUTCDATE()
                WHERE UsageResetDate < GETUTCDATE()";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@NextMonth", GetNextMonthStart());

            var rows = await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Reset monthly usage for {Count} workspaces", rows);
        }

        private static DateTime GetNextMonthStart()
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, 1).AddMonths(1);
        }

        private static SlackWorkspace MapFromReader(SqlDataReader reader)
        {
            return new SlackWorkspace
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                TeamId = reader.GetString(reader.GetOrdinal("TeamId")),
                TeamName = reader.IsDBNull(reader.GetOrdinal("TeamName")) ? null : reader.GetString(reader.GetOrdinal("TeamName")),
                AccessToken = reader.GetString(reader.GetOrdinal("AccessToken")),
                TokenType = reader.GetString(reader.GetOrdinal("TokenType")),
                Scope = reader.IsDBNull(reader.GetOrdinal("Scope")) ? null : reader.GetString(reader.GetOrdinal("Scope")),
                InstalledByUserId = reader.IsDBNull(reader.GetOrdinal("InstalledByUserId")) ? null : reader.GetString(reader.GetOrdinal("InstalledByUserId")),
                InstalledByUserName = reader.IsDBNull(reader.GetOrdinal("InstalledByUserName")) ? null : reader.GetString(reader.GetOrdinal("InstalledByUserName")),
                OrganizationId = reader.IsDBNull(reader.GetOrdinal("OrganizationId")) ? null : reader.GetGuid(reader.GetOrdinal("OrganizationId")),
                Tier = reader.GetString(reader.GetOrdinal("Tier")),
                MonthlyAnalysisCount = reader.GetInt32(reader.GetOrdinal("MonthlyAnalysisCount")),
                MonthlyAnalysisLimit = reader.GetInt32(reader.GetOrdinal("MonthlyAnalysisLimit")),
                LastAnalysisAt = reader.IsDBNull(reader.GetOrdinal("LastAnalysisAt")) ? null : reader.GetDateTime(reader.GetOrdinal("LastAnalysisAt")),
                UsageResetDate = reader.IsDBNull(reader.GetOrdinal("UsageResetDate")) ? null : reader.GetDateTime(reader.GetOrdinal("UsageResetDate")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
            };
        }

        private static void AddParameters(SqlCommand command, SlackWorkspace workspace)
        {
            command.Parameters.AddWithValue("@Id", workspace.Id);
            command.Parameters.AddWithValue("@TeamId", workspace.TeamId);
            command.Parameters.AddWithValue("@TeamName", (object?)workspace.TeamName ?? DBNull.Value);
            command.Parameters.AddWithValue("@AccessToken", workspace.AccessToken);
            command.Parameters.AddWithValue("@TokenType", workspace.TokenType);
            command.Parameters.AddWithValue("@Scope", (object?)workspace.Scope ?? DBNull.Value);
            command.Parameters.AddWithValue("@InstalledByUserId", (object?)workspace.InstalledByUserId ?? DBNull.Value);
            command.Parameters.AddWithValue("@InstalledByUserName", (object?)workspace.InstalledByUserName ?? DBNull.Value);
            command.Parameters.AddWithValue("@OrganizationId", (object?)workspace.OrganizationId ?? DBNull.Value);
            command.Parameters.AddWithValue("@Tier", workspace.Tier);
            command.Parameters.AddWithValue("@MonthlyAnalysisCount", workspace.MonthlyAnalysisCount);
            command.Parameters.AddWithValue("@MonthlyAnalysisLimit", workspace.MonthlyAnalysisLimit);
            command.Parameters.AddWithValue("@LastAnalysisAt", (object?)workspace.LastAnalysisAt ?? DBNull.Value);
            command.Parameters.AddWithValue("@UsageResetDate", (object?)workspace.UsageResetDate ?? DBNull.Value);
            command.Parameters.AddWithValue("@CreatedAt", workspace.CreatedAt);
            command.Parameters.AddWithValue("@UpdatedAt", workspace.UpdatedAt);
            command.Parameters.AddWithValue("@IsActive", workspace.IsActive);
        }
    }
}
