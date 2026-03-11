using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class GraphTokenRepository : IGraphTokenRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<GraphTokenRepository> _logger;

        public GraphTokenRepository(string connectionString, ILogger<GraphTokenRepository> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task<UserGraphToken?> GetByUserIdAsync(Guid userId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, UserId, AccessToken, RefreshToken, ExpiresAt, 
                               GraphUserId, GraphUserEmail, UpdatedAt
                        FROM UserGraphTokens WHERE UserId = @UserId";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return MapToken(reader);

            return null;
        }

        public async Task<UserGraphToken?> GetByGraphUserIdAsync(string graphUserId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, UserId, AccessToken, RefreshToken, ExpiresAt,
                               GraphUserId, GraphUserEmail, UpdatedAt
                        FROM UserGraphTokens WHERE GraphUserId = @GraphUserId";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@GraphUserId", graphUserId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return MapToken(reader);

            return null;
        }

        public async Task UpsertAsync(UserGraphToken token)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                MERGE UserGraphTokens AS target
                USING (SELECT @UserId AS UserId) AS source ON target.UserId = source.UserId
                WHEN MATCHED THEN
                    UPDATE SET AccessToken = @AccessToken, RefreshToken = @RefreshToken,
                               ExpiresAt = @ExpiresAt, GraphUserId = @GraphUserId,
                               GraphUserEmail = @GraphUserEmail, UpdatedAt = GETUTCDATE()
                WHEN NOT MATCHED THEN
                    INSERT (UserId, AccessToken, RefreshToken, ExpiresAt, GraphUserId, GraphUserEmail)
                    VALUES (@UserId, @AccessToken, @RefreshToken, @ExpiresAt, @GraphUserId, @GraphUserEmail);";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", token.UserId);
            cmd.Parameters.AddWithValue("@AccessToken", token.AccessToken);
            cmd.Parameters.AddWithValue("@RefreshToken", token.RefreshToken);
            cmd.Parameters.AddWithValue("@ExpiresAt", token.ExpiresAt);
            cmd.Parameters.AddWithValue("@GraphUserId", (object?)token.GraphUserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@GraphUserEmail", (object?)token.GraphUserEmail ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Upserted Graph token for user {UserId}", token.UserId);
        }

        public async Task<string?> GetSubscriptionIdAsync(Guid userId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT SubscriptionId FROM UserGraphSubscriptions 
                        WHERE UserId = @UserId AND ExpiresAt > GETUTCDATE()";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);

            var result = await cmd.ExecuteScalarAsync();
            return result as string;
        }

        public async Task UpsertSubscriptionAsync(Guid userId, string subscriptionId, DateTime expiresAt)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                MERGE UserGraphSubscriptions AS target
                USING (SELECT @UserId AS UserId) AS source ON target.UserId = source.UserId
                WHEN MATCHED THEN
                    UPDATE SET SubscriptionId = @SubscriptionId, ExpiresAt = @ExpiresAt
                WHEN NOT MATCHED THEN
                    INSERT (UserId, SubscriptionId, ExpiresAt)
                    VALUES (@UserId, @SubscriptionId, @ExpiresAt);";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@SubscriptionId", subscriptionId);
            cmd.Parameters.AddWithValue("@ExpiresAt", expiresAt);

            await cmd.ExecuteNonQueryAsync();
        }

        private static UserGraphToken MapToken(SqlDataReader reader) => new()
        {
            Id = reader.GetInt32(0),
            UserId = reader.GetGuid(1),
            AccessToken = reader.GetString(2),
            RefreshToken = reader.GetString(3),
            ExpiresAt = reader.GetDateTime(4),
            GraphUserId = reader.IsDBNull(5) ? null : reader.GetString(5),
            GraphUserEmail = reader.IsDBNull(6) ? null : reader.GetString(6),
            UpdatedAt = reader.GetDateTime(7)
        };
    }
}