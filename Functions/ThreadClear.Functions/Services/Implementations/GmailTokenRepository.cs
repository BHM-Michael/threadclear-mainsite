using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class GmailTokenRepository : IGmailTokenRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<GmailTokenRepository> _logger;

        public GmailTokenRepository(string connectionString, ILogger<GmailTokenRepository> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task<UserGmailToken?> GetByUserIdAsync(Guid userId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, UserId, AccessToken, RefreshToken, ExpiresAt,
                               GmailUserId, GmailUserEmail, UpdatedAt
                        FROM UserGmailTokens WHERE UserId = @UserId";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return MapToken(reader);

            return null;
        }

        public async Task<UserGmailToken?> GetByEmailAsync(string email)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, UserId, AccessToken, RefreshToken, ExpiresAt,
                               GmailUserId, GmailUserEmail, UpdatedAt
                        FROM UserGmailTokens WHERE GmailUserEmail = @Email";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Email", email);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return MapToken(reader);

            return null;
        }

        public async Task UpsertAsync(UserGmailToken token)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                MERGE UserGmailTokens AS target
                USING (SELECT @UserId AS UserId) AS source ON target.UserId = source.UserId
                WHEN MATCHED THEN
                    UPDATE SET AccessToken = @AccessToken, RefreshToken = @RefreshToken,
                               ExpiresAt = @ExpiresAt, GmailUserId = @GmailUserId,
                               GmailUserEmail = @GmailUserEmail, UpdatedAt = GETUTCDATE()
                WHEN NOT MATCHED THEN
                    INSERT (UserId, AccessToken, RefreshToken, ExpiresAt, GmailUserId, GmailUserEmail)
                    VALUES (@UserId, @AccessToken, @RefreshToken, @ExpiresAt, @GmailUserId, @GmailUserEmail);";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", token.UserId);
            cmd.Parameters.AddWithValue("@AccessToken", token.AccessToken);
            cmd.Parameters.AddWithValue("@RefreshToken", token.RefreshToken);
            cmd.Parameters.AddWithValue("@ExpiresAt", token.ExpiresAt);
            cmd.Parameters.AddWithValue("@GmailUserId", (object?)token.GmailUserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@GmailUserEmail", (object?)token.GmailUserEmail ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Upserted Gmail token for user {UserId}", token.UserId);
        }

        public async Task<string?> GetHistoryIdAsync(Guid userId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT HistoryId FROM UserGmailWatches WHERE UserId = @UserId";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);

            var result = await cmd.ExecuteScalarAsync();
            return result as string;
        }

        public async Task UpsertWatchAsync(Guid userId, string historyId, DateTime expiresAt)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                MERGE UserGmailWatches AS target
                USING (SELECT @UserId AS UserId) AS source ON target.UserId = source.UserId
                WHEN MATCHED THEN
                    UPDATE SET HistoryId = @HistoryId, ExpiresAt = @ExpiresAt,
                               UpdatedAt = GETUTCDATE()
                WHEN NOT MATCHED THEN
                    INSERT (UserId, HistoryId, ExpiresAt)
                    VALUES (@UserId, @HistoryId, @ExpiresAt);";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@HistoryId", historyId);
            cmd.Parameters.AddWithValue("@ExpiresAt", expiresAt);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<Guid>> GetUsersWithExpiringWatchesAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Return users whose watch expires within 24 hours
            var sql = @"SELECT UserId FROM UserGmailWatches
                        WHERE ExpiresAt < DATEADD(HOUR, 24, GETUTCDATE())";

            using var cmd = new SqlCommand(sql, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            var userIds = new List<Guid>();
            while (await reader.ReadAsync())
                userIds.Add(reader.GetGuid(0));

            return userIds;
        }

        private static UserGmailToken MapToken(SqlDataReader reader) => new()
        {
            Id = reader.GetInt32(0),
            UserId = reader.GetGuid(1),
            AccessToken = reader.GetString(2),
            RefreshToken = reader.GetString(3),
            ExpiresAt = reader.GetDateTime(4),
            GmailUserId = reader.IsDBNull(5) ? null : reader.GetString(5),
            GmailUserEmail = reader.IsDBNull(6) ? null : reader.GetString(6),
            UpdatedAt = reader.GetDateTime(7)
        };
    }
}