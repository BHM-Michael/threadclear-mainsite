using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IAnalysisResultService
    {
        Task<Guid> SaveAsync(AnalysisRecord result);
        Task<List<AnalysisRecord>> GetByUserAsync(Guid userId, int limit = 50);
        Task<List<AnalysisRecord>> GetByOrganizationAsync(Guid organizationId, int limit = 100);
        Task<AnalysisRecord?> GetByIdAsync(Guid id);
    }
}