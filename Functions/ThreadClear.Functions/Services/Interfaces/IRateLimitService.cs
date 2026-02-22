using System.Threading.Tasks;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IRateLimitService
    {
        bool IsAllowed(string ipAddress, int maxRequests = 3);
        int GetRemainingRequests(string ipAddress, int maxRequests = 3);
        Task LogPublicScanAsync(string ipAddress, string sourceType, int textLength,
            int participantCount, int messageCount, int analysisTimeMs);
        Task PingAsync();
    }
}