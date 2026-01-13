using System;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface ISlackWorkspaceRepository
    {
        Task<SlackWorkspace?> GetByTeamIdAsync(string teamId);
        Task<SlackWorkspace> CreateAsync(SlackWorkspace workspace);
        Task<SlackWorkspace> UpdateAsync(SlackWorkspace workspace);
        Task<bool> IncrementUsageAsync(string teamId);
        Task ResetMonthlyUsageAsync();
    }
}
