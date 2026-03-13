using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Services;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IGmailService
    {
        Task<List<GmailThread>> ListRecentThreadsAsync(string accessToken, int hoursBack = 24, int maxResults = 50);
        Task<GmailThread?> GetThreadAsync(string accessToken, string threadId);
        Task<GmailThread?> GetThreadByMessageIdAsync(string accessToken, string messageId);
        Task<GmailEmailMetadata> GetEmailMetadataAsync(string accessToken, string messageId);
        string ConvertThreadToConversation(GmailThread thread);
        Task<string> RefreshAccessTokenAsync(string refreshToken, string clientId, string clientSecret);
        Task<GmailWatchResult> SetupWatchAsync(string accessToken, string pubSubTopic);
        Task<GmailHistoryResult> GetNewMessageIdsAsync(string accessToken, string startHistoryId);
    }
}