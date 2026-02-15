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
        private readonly ConcurrentDictionary<string, List<DateTime>> _requestLog = new();

        public RateLimitService(string connectionString, ILogger<RateLimitService> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public bool IsAllowed(string ipAddress, int maxRequests = 3)
        {
            var now = DateTime.UtcNow;
            var windowStart = now.Date;

            var requests = _requestLog.AddOrUpdate(
                ipAddress,
                _ => new List<DateTime> { now },
                (_, existing) =>
                {
                    existing.RemoveAll(t => t < windowStart);

                    if (existing.Count < maxRequests)
                    {
                        existing.Add(now);
                    }

                    return existing;
                });

            var allowed = requests.Count(t => t >= windowStart) <= maxRequests;

            if (!allowed)
            {
                _logger.LogWarning("Rate limit exceeded for IP {IP} - {Count}/{Max} requests today",
                    MaskIp(ipAddress), requests.Count, maxRequests);
            }

            return allowed;
        }

        public int GetRemainingRequests(string ipAddress, int maxRequests = 3)
        {
            var windowStart = DateTime.UtcNow.Date;

            if (!_requestLog.TryGetValue(ipAddress, out var requests))
                return maxRequests;

            var todayCount = requests.Count(t => t >= windowStart);
            return Math.Max(0, maxRequests - todayCount);
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

        private string HashIp(string ip)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(ip));
            return Convert.ToHexString(bytes).ToLower();
        }

        private string MaskIp(string ip)
        {
            var parts = ip.Split('.');
            if (parts.Length == 4)
                return $"{parts[0]}.{parts[1]}.xxx.xxx";
            return "masked";
        }
    }
}