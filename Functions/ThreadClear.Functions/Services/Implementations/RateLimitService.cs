using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class RateLimitService : IRateLimitService
    {
        private readonly ILogger<RateLimitService> _logger;
        private readonly string _connectionString;

        public RateLimitService(string connectionString, ILogger<RateLimitService> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public bool IsAllowed(string ipAddress, int maxRequests = 3)
        {
            try
            {
                var ipHash = HashIp(ipAddress);
                var windowStart = DateTime.UtcNow.Date;

                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var sql = @"SELECT COUNT(*) FROM PublicScanLog 
                    WHERE ClientIpHash = @IpHash 
                    AND CreatedAt >= @WindowStart";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@IpHash", ipHash);
                cmd.Parameters.AddWithValue("@WindowStart", windowStart);

                var count = (int)cmd.ExecuteScalar();
                return count < maxRequests;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rate limit check failed — allowing request");
                return true;
            }
        }

        public int GetRemainingRequests(string ipAddress, int maxRequests = 3)
        {
            try
            {
                var ipHash = HashIp(ipAddress);
                var windowStart = DateTime.UtcNow.Date;

                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var sql = @"SELECT COUNT(*) FROM PublicScanLog 
                    WHERE ClientIpHash = @IpHash 
                    AND CreatedAt >= @WindowStart";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@IpHash", ipHash);
                cmd.Parameters.AddWithValue("@WindowStart", windowStart);

                var count = (int)cmd.ExecuteScalar();
                return Math.Max(0, maxRequests - count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetRemainingRequests failed");
                return 0;
            }
        }

        public async Task LogPublicScanAsync(string ipAddress, string sourceType, int textLength,
            int participantCount, int messageCount, int analysisTimeMs)
        {
            try
            {
                var ipHash = HashIp(ipAddress);

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var sql = @"INSERT INTO PublicScanLog 
                    (ClientIpHash, SourceType, TextLength, ParticipantCount, MessageCount, AnalysisTimeMs)
                    VALUES (@IpHash, @SourceType, @TextLength, @ParticipantCount, @MessageCount, @AnalysisTimeMs)";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@IpHash", ipHash);
                cmd.Parameters.AddWithValue("@SourceType", sourceType);
                cmd.Parameters.AddWithValue("@TextLength", textLength);
                cmd.Parameters.AddWithValue("@ParticipantCount", participantCount);
                cmd.Parameters.AddWithValue("@MessageCount", messageCount);
                cmd.Parameters.AddWithValue("@AnalysisTimeMs", analysisTimeMs);

                await cmd.ExecuteNonQueryAsync();
                _logger.LogInformation("Logged public scan: {Length} chars, {Participants} participants, {Ms}ms",
                    textLength, participantCount, analysisTimeMs);
            }
            catch (Exception ex)
            {
                // Don't fail the analysis if logging fails
                _logger.LogError(ex, "Failed to log public scan");
            }
        }

        public async Task PingAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync();
        }

        private string HashIp(string ip)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(ip));
            return Convert.ToHexString(bytes).ToLower();
        }
    }
}