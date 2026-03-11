using System;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IGraphTokenRepository
    {
        Task<UserGraphToken?> GetByUserIdAsync(Guid userId);
        Task<UserGraphToken?> GetByGraphUserIdAsync(string graphUserId);
        Task UpsertAsync(UserGraphToken token);
        Task<string?> GetSubscriptionIdAsync(Guid userId);
        Task UpsertSubscriptionAsync(Guid userId, string subscriptionId, DateTime expiresAt);
    }
}