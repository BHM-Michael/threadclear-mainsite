using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Data;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class TaxonomyService : ITaxonomyService
    {
        private readonly ITaxonomyRepository _taxonomyRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ILogger<TaxonomyService> _logger;

        public TaxonomyService(
            ITaxonomyRepository taxonomyRepository,
            IOrganizationRepository organizationRepository,
            ILogger<TaxonomyService> logger)
        {
            _taxonomyRepository = taxonomyRepository;
            _organizationRepository = organizationRepository;
            _logger = logger;
        }

        public async Task<TaxonomyData> GetTaxonomyForOrganization(Guid organizationId)
        {
            // 1. Get the organization to know its industry type
            var org = await _organizationRepository.GetById(organizationId);
            var industryType = org?.IndustryType ?? "default";

            // 2. Start with industry template (or default)
            var baseTaxonomy = IndustryTemplates.GetTemplate(industryType);

            // 3. Get organization-specific overrides if any
            var orgConfig = await _taxonomyRepository.GetByOrganizationId(organizationId);

            // 4. Merge organization overrides into base
            if (orgConfig?.Taxonomy != null)
            {
                MergeTaxonomy(baseTaxonomy, orgConfig.Taxonomy);
            }

            return baseTaxonomy;
        }

        public Task<TaxonomyData> GetIndustryTemplate(string industryType)
        {
            return Task.FromResult(IndustryTemplates.GetTemplate(industryType));
        }

        public async Task<TaxonomyConfiguration> SaveOrganizationTaxonomy(Guid organizationId, TaxonomyData taxonomy, Guid updatedBy)
        {
            var existingConfig = await _taxonomyRepository.GetByOrganizationId(organizationId);

            if (existingConfig != null)
            {
                existingConfig.Taxonomy = taxonomy;
                existingConfig.UpdatedBy = updatedBy;
                return await _taxonomyRepository.Update(existingConfig);
            }
            else
            {
                var org = await _organizationRepository.GetById(organizationId);
                var newConfig = new TaxonomyConfiguration
                {
                    Id = Guid.NewGuid(),
                    Scope = "Organization",
                    OrganizationId = organizationId,
                    Name = $"{org?.Name ?? "Organization"} Custom Taxonomy",
                    Taxonomy = taxonomy,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = updatedBy,
                    IsActive = true
                };

                return await _taxonomyRepository.Create(newConfig);
            }
        }

        public async Task<TaxonomyData> AddCustomTopic(Guid organizationId, TopicDefinition topic, Guid updatedBy)
        {
            var taxonomy = await GetTaxonomyForOrganization(organizationId);

            // Check if topic already exists
            var existingTopic = taxonomy.Topics.FirstOrDefault(t => t.Key == topic.Key);
            if (existingTopic != null)
            {
                taxonomy.Topics.Remove(existingTopic);
            }

            topic.IsCustom = true;
            taxonomy.Topics.Add(topic);

            await SaveOrganizationTaxonomy(organizationId, taxonomy, updatedBy);
            _logger.LogInformation("Added custom topic {TopicKey} to org {OrgId}", topic.Key, organizationId);

            return taxonomy;
        }

        public async Task<TaxonomyData> RemoveCustomTopic(Guid organizationId, string topicKey, Guid updatedBy)
        {
            var taxonomy = await GetTaxonomyForOrganization(organizationId);

            var topic = taxonomy.Topics.FirstOrDefault(t => t.Key == topicKey && t.IsCustom);
            if (topic != null)
            {
                taxonomy.Topics.Remove(topic);
                await SaveOrganizationTaxonomy(organizationId, taxonomy, updatedBy);
                _logger.LogInformation("Removed custom topic {TopicKey} from org {OrgId}", topicKey, organizationId);
            }

            return taxonomy;
        }

        public async Task<TaxonomyData> AddCustomRole(Guid organizationId, RoleDefinition role, Guid updatedBy)
        {
            var taxonomy = await GetTaxonomyForOrganization(organizationId);

            var existingRole = taxonomy.Roles.FirstOrDefault(r => r.Key == role.Key);
            if (existingRole != null)
            {
                taxonomy.Roles.Remove(existingRole);
            }

            taxonomy.Roles.Add(role);

            await SaveOrganizationTaxonomy(organizationId, taxonomy, updatedBy);
            _logger.LogInformation("Added custom role {RoleKey} to org {OrgId}", role.Key, organizationId);

            return taxonomy;
        }

        public async Task<TaxonomyData> RemoveCustomRole(Guid organizationId, string roleKey, Guid updatedBy)
        {
            var taxonomy = await GetTaxonomyForOrganization(organizationId);

            // Don't allow removing default roles
            var defaultRoles = new[] { "customer", "representative", "manager", "vendor", "internal_team_member", "unknown" };
            if (defaultRoles.Contains(roleKey))
            {
                _logger.LogWarning("Attempted to remove default role {RoleKey} from org {OrgId}", roleKey, organizationId);
                return taxonomy;
            }

            var role = taxonomy.Roles.FirstOrDefault(r => r.Key == roleKey);
            if (role != null)
            {
                taxonomy.Roles.Remove(role);
                await SaveOrganizationTaxonomy(organizationId, taxonomy, updatedBy);
                _logger.LogInformation("Removed custom role {RoleKey} from org {OrgId}", roleKey, organizationId);
            }

            return taxonomy;
        }

        private void MergeTaxonomy(TaxonomyData baseData, TaxonomyData overrides)
        {
            // Merge topics - overrides replace base topics with same key
            foreach (var topic in overrides.Topics)
            {
                var existing = baseData.Topics.FirstOrDefault(t => t.Key == topic.Key);
                if (existing != null)
                {
                    baseData.Topics.Remove(existing);
                }
                baseData.Topics.Add(topic);
            }

            // Merge roles
            foreach (var role in overrides.Roles)
            {
                var existing = baseData.Roles.FirstOrDefault(r => r.Key == role.Key);
                if (existing != null)
                {
                    baseData.Roles.Remove(existing);
                }
                baseData.Roles.Add(role);
            }

            // Add severity rules (org rules take precedence - inserted first)
            baseData.SeverityRules.InsertRange(0, overrides.SeverityRules);

            // Merge category values
            foreach (var category in overrides.Categories)
            {
                var existingCat = baseData.Categories.FirstOrDefault(c => c.Key == category.Key);
                if (existingCat != null)
                {
                    foreach (var value in category.Values)
                    {
                        var existingVal = existingCat.Values.FirstOrDefault(v => v.Key == value.Key);
                        if (existingVal != null)
                        {
                            existingCat.Values.Remove(existingVal);
                        }
                        existingCat.Values.Add(value);
                    }
                }
                else
                {
                    baseData.Categories.Add(category);
                }
            }
        }
    }
}
