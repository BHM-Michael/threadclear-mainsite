using System;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IGmailTokenRepository
    {
        Task<UserGmailToken?> GetByUserIdAsync(Guid userId);
        Task<UserGmailToken?> GetByEmailAsync(string email);
        Task UpsertAsync(UserGmailToken token);
        Task<string?> GetHistoryIdAsync(Guid userId);
        Task UpsertWatchAsync(Guid userId, string historyId, DateTime expiresAt);
        Task<List<Guid>> GetUsersWithExpiringWatchesAsync();
    }
}