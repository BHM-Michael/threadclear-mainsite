using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IDigestInsightRepository
    {
        Task CreateAsync(DigestInsight insight);
        Task<List<DigestInsight>> GetPendingInsightsForUserAsync(Guid userId);
        Task<List<Guid>> GetUsersWithPendingInsightsAsync();
        Task MarkSentAsync(List<int> ids);
    }
}