using System;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IGraphService
    {
        Task<string> RefreshAccessTokenAsync(string refreshToken);
        Task<string> GetEmailBodyAsync(string accessToken, string messageId);
        Task<GraphEmailMetadata> GetEmailMetadataAsync(string accessToken, string messageId);
        Task<string> CreateSubscriptionAsync(string accessToken, Guid userId);
        Task RenewSubscriptionAsync(string accessToken, string subscriptionId);
    }

    public class GraphEmailMetadata
    {
        public string MessageId { get; set; } = "";
        public string Subject { get; set; } = "";
        public string BodyText { get; set; } = "";
        public string FromEmail { get; set; } = "";
        public string GraphUserId { get; set; } = "";
        public DateTime ReceivedAt { get; set; }
        public int MessageCount { get; set; } = 1;
    }
}