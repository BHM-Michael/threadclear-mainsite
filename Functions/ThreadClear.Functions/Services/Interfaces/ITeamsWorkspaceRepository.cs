using System;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface ITeamsWorkspaceRepository
    {
        Task<TeamsWorkspace?> GetByTenantIdAsync(string tenantId);
        Task<TeamsWorkspace> CreateAsync(TeamsWorkspace workspace);
        Task<TeamsWorkspace> UpdateAsync(TeamsWorkspace workspace);
        Task<bool> IncrementUsageAsync(string tenantId);
        Task ResetMonthlyUsageAsync();
    }
}
