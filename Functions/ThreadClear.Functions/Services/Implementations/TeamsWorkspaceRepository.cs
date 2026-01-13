using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class TeamsWorkspaceRepository : ITeamsWorkspaceRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<TeamsWorkspaceRepository> _logger;

        public TeamsWorkspaceRepository(IConfiguration configuration, ILogger<TeamsWorkspaceRepository> logger)
        {
            _connectionString = configuration["SqlConnectionString"]
                ?? throw new InvalidOperationException("Connection string not found");
            _logger = logger;
        }

        public async Task<TeamsWorkspace?> GetByTenantIdAsync(string tenantId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT Id, TenantId, TenantName, ServiceUrl, OrganizationId,
                       Tier, MonthlyAnalysisCount, MonthlyAnalysisLimit,
                       LastAnalysisAt, UsageResetDate, CreatedAt, UpdatedAt, IsActive
                FROM TeamsWorkspaces
                WHERE TenantId = @TenantId AND IsActive = 1";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TenantId", tenantId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapFromReader(reader);
            }

            return null;
        }

        public async Task<TeamsWorkspace> CreateAsync(TeamsWorkspace workspace)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            workspace.Id = Guid.NewGuid();
            workspace.CreatedAt = DateTime.UtcNow;
            workspace.UpdatedAt = DateTime.UtcNow;
            workspace.UsageResetDate = GetNextMonthStart();

            var sql = @"
                INSERT INTO TeamsWorkspaces 
                (Id, TenantId, TenantName, ServiceUrl, OrganizationId,
                 Tier, MonthlyAnalysisCount, MonthlyAnalysisLimit,
                 LastAnalysisAt, UsageResetDate, CreatedAt, UpdatedAt, IsActive)
                VALUES 
                (@Id, @TenantId, @TenantName, @ServiceUrl, @OrganizationId,
                 @Tier, @MonthlyAnalysisCount, @MonthlyAnalysisLimit,
                 @LastAnalysisAt, @UsageResetDate, @CreatedAt, @UpdatedAt, @IsActive)";

            using var command = new SqlCommand(sql, connection);
            AddParameters(command, workspace);

            await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Created Teams workspace {TenantId} ({TenantName})", workspace.TenantId, workspace.TenantName);

            return workspace;
        }

        public async Task<TeamsWorkspace> UpdateAsync(TeamsWorkspace workspace)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            workspace.UpdatedAt = DateTime.UtcNow;

            var sql = @"
                UPDATE TeamsWorkspaces SET
                    TenantName = @TenantName,
                    ServiceUrl = @ServiceUrl,
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
            _logger.LogInformation("Updated Teams workspace {TenantId}", workspace.TenantId);

            return workspace;
        }

        public async Task<bool> IncrementUsageAsync(string tenantId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // First check if we need to reset the monthly count
            var checkSql = @"
                UPDATE TeamsWorkspaces 
                SET MonthlyAnalysisCount = 0, 
                    UsageResetDate = @NextMonth
                WHERE TenantId = @TenantId 
                  AND UsageResetDate < GETUTCDATE()";

            using (var checkCommand = new SqlCommand(checkSql, connection))
            {
                checkCommand.Parameters.AddWithValue("@TenantId", tenantId);
                checkCommand.Parameters.AddWithValue("@NextMonth", GetNextMonthStart());
                await checkCommand.ExecuteNonQueryAsync();
            }

            // Now increment the counter
            var sql = @"
                UPDATE TeamsWorkspaces 
                SET MonthlyAnalysisCount = MonthlyAnalysisCount + 1,
                    LastAnalysisAt = GETUTCDATE(),
                    UpdatedAt = GETUTCDATE()
                WHERE TenantId = @TenantId AND IsActive = 1";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TenantId", tenantId);

            var rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }

        public async Task ResetMonthlyUsageAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                UPDATE TeamsWorkspaces 
                SET MonthlyAnalysisCount = 0,
                    UsageResetDate = @NextMonth,
                    UpdatedAt = GETUTCDATE()
                WHERE UsageResetDate < GETUTCDATE()";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@NextMonth", GetNextMonthStart());

            var rows = await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Reset monthly usage for {Count} Teams workspaces", rows);
        }

        private static DateTime GetNextMonthStart()
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, 1).AddMonths(1);
        }

        private static TeamsWorkspace MapFromReader(SqlDataReader reader)
        {
            return new TeamsWorkspace
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                TenantId = reader.GetString(reader.GetOrdinal("TenantId")),
                TenantName = reader.IsDBNull(reader.GetOrdinal("TenantName")) ? null : reader.GetString(reader.GetOrdinal("TenantName")),
                ServiceUrl = reader.IsDBNull(reader.GetOrdinal("ServiceUrl")) ? null : reader.GetString(reader.GetOrdinal("ServiceUrl")),
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

        private static void AddParameters(SqlCommand command, TeamsWorkspace workspace)
        {
            command.Parameters.AddWithValue("@Id", workspace.Id);
            command.Parameters.AddWithValue("@TenantId", workspace.TenantId);
            command.Parameters.AddWithValue("@TenantName", (object?)workspace.TenantName ?? DBNull.Value);
            command.Parameters.AddWithValue("@ServiceUrl", (object?)workspace.ServiceUrl ?? DBNull.Value);
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
