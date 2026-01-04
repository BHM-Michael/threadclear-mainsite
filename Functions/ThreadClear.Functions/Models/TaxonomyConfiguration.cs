using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ThreadClear.Functions.Models
{
    public class TaxonomyConfiguration
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Scope { get; set; } = "Organization"; // System, Industry, Organization
        public string? IndustryType { get; set; }
        public Guid? OrganizationId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string TaxonomyJson { get; set; } = "{}";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public Guid? UpdatedBy { get; set; }
        public bool IsActive { get; set; } = true;

        // Parsed taxonomy
        private TaxonomyData? _taxonomy;
        public TaxonomyData Taxonomy
        {
            get
            {
                if (_taxonomy == null && !string.IsNullOrEmpty(TaxonomyJson))
                {
                    _taxonomy = JsonSerializer.Deserialize<TaxonomyData>(TaxonomyJson);
                }
                return _taxonomy ?? new TaxonomyData();
            }
            set
            {
                _taxonomy = value;
                TaxonomyJson = JsonSerializer.Serialize(value);
            }
        }
    }

    public class TaxonomyData
    {
        public List<CategoryDefinition> Categories { get; set; } = new();
        public List<TopicDefinition> Topics { get; set; } = new();
        public List<RoleDefinition> Roles { get; set; } = new();
        public List<SeverityRule> SeverityRules { get; set; } = new();
    }

    public class CategoryDefinition
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Description { get; set; }
        public List<ValueDefinition> Values { get; set; } = new();
    }

    public class ValueDefinition
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Template { get; set; }
        public string[] TriggerPatterns { get; set; } = Array.Empty<string>();
    }

    public class TopicDefinition
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string[] Keywords { get; set; } = Array.Empty<string>();
        public bool IsCustom { get; set; } = false;
    }

    public class RoleDefinition
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string[] Keywords { get; set; } = Array.Empty<string>();
        public string[] EmailDomainPatterns { get; set; } = Array.Empty<string>();
    }

    public class SeverityRule
    {
        public string Category { get; set; } = "";
        public string Value { get; set; } = "";
        public string Condition { get; set; } = "";
        public string Severity { get; set; } = "";
    }
}