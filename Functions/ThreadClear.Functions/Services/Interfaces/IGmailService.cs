using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IGmailService
    {
        Task<string> RefreshAccessTokenAsync(string refreshToken);
        Task<GmailEmailMetadata> GetEmailMetadataAsync(string accessToken, string messageId);
        Task<GmailHistoryResult> GetNewMessageIdsAsync(string accessToken, string startHistoryId);
        Task<GmailWatchResult> SetupWatchAsync(string accessToken, string pubSubTopic);
    }

    public class GmailEmailMetadata
    {
        public string MessageId { get; set; } = "";
        public string Subject { get; set; } = "";
        public string BodyText { get; set; } = "";
        public string FromEmail { get; set; } = "";
        public DateTime ReceivedAt { get; set; }
    }

    public class GmailHistoryResult
    {
        public List<string> NewMessageIds { get; set; } = new();
        public string LatestHistoryId { get; set; } = "";
    }

    public class GmailWatchResult
    {
        public string HistoryId { get; set; } = "";
        public DateTime Expiration { get; set; }
    }
}