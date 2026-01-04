using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface ITaxonomyRepository
    {
        // Get taxonomy configurations
        Task<TaxonomyConfiguration?> GetById(Guid id);
        Task<TaxonomyConfiguration?> GetByOrganizationId(Guid organizationId);
        Task<TaxonomyConfiguration?> GetByIndustryType(string industryType);
        Task<TaxonomyConfiguration?> GetSystemDefault();

        // Save taxonomy
        Task<TaxonomyConfiguration> Create(TaxonomyConfiguration config);
        Task<TaxonomyConfiguration> Update(TaxonomyConfiguration config);
        Task<bool> Delete(Guid id);

        // List operations
        Task<List<TaxonomyConfiguration>> GetAllIndustryTemplates();
    }
}