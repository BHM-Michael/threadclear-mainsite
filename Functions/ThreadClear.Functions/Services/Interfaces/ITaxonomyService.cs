using System;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface ITaxonomyService
    {
        // Get merged taxonomy for an organization
        Task<TaxonomyData> GetTaxonomyForOrganization(Guid organizationId);
        
        // Get industry template
        Task<TaxonomyData> GetIndustryTemplate(string industryType);
        
        // Save organization customizations
        Task<TaxonomyConfiguration> SaveOrganizationTaxonomy(Guid organizationId, TaxonomyData taxonomy, Guid updatedBy);
        
        // Topic management
        Task<TaxonomyData> AddCustomTopic(Guid organizationId, TopicDefinition topic, Guid updatedBy);
        Task<TaxonomyData> RemoveCustomTopic(Guid organizationId, string topicKey, Guid updatedBy);
        
        // Role management
        Task<TaxonomyData> AddCustomRole(Guid organizationId, RoleDefinition role, Guid updatedBy);
        Task<TaxonomyData> RemoveCustomRole(Guid organizationId, string roleKey, Guid updatedBy);
    }
}
