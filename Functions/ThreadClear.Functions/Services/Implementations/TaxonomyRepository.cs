using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class TaxonomyRepository : ITaxonomyRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<TaxonomyRepository> _logger;

        public TaxonomyRepository(string connectionString, ILogger<TaxonomyRepository> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task<TaxonomyConfiguration?> GetById(Guid id)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, Scope, IndustryType, OrganizationId, Name, Description, 
                               Taxonomy, CreatedAt, UpdatedAt, UpdatedBy, IsActive
                        FROM TaxonomyConfigurations 
                        WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapTaxonomyConfiguration(reader);
            }
            return null;
        }

        public async Task<TaxonomyConfiguration?> GetByOrganizationId(Guid organizationId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, Scope, IndustryType, OrganizationId, Name, Description, 
                               Taxonomy, CreatedAt, UpdatedAt, UpdatedBy, IsActive
                        FROM TaxonomyConfigurations 
                        WHERE OrganizationId = @OrganizationId AND Scope = 'Organization' AND IsActive = 1";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@OrganizationId", organizationId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapTaxonomyConfiguration(reader);
            }
            return null;
        }

        public async Task<TaxonomyConfiguration?> GetByIndustryType(string industryType)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, Scope, IndustryType, OrganizationId, Name, Description, 
                               Taxonomy, CreatedAt, UpdatedAt, UpdatedBy, IsActive
                        FROM TaxonomyConfigurations 
                        WHERE IndustryType = @IndustryType AND Scope = 'Industry' AND IsActive = 1";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@IndustryType", industryType);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapTaxonomyConfiguration(reader);
            }
            return null;
        }

        public async Task<TaxonomyConfiguration?> GetSystemDefault()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, Scope, IndustryType, OrganizationId, Name, Description, 
                               Taxonomy, CreatedAt, UpdatedAt, UpdatedBy, IsActive
                        FROM TaxonomyConfigurations 
                        WHERE Scope = 'System' AND IsActive = 1";

            using var cmd = new SqlCommand(sql, connection);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapTaxonomyConfiguration(reader);
            }
            return null;
        }

        public async Task<TaxonomyConfiguration> Create(TaxonomyConfiguration config)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"INSERT INTO TaxonomyConfigurations 
                        (Id, Scope, IndustryType, OrganizationId, Name, Description, Taxonomy, CreatedAt, UpdatedAt, UpdatedBy, IsActive)
                        VALUES (@Id, @Scope, @IndustryType, @OrganizationId, @Name, @Description, @Taxonomy, @CreatedAt, @UpdatedAt, @UpdatedBy, @IsActive)";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", config.Id);
            cmd.Parameters.AddWithValue("@Scope", config.Scope);
            cmd.Parameters.AddWithValue("@IndustryType", (object?)config.IndustryType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@OrganizationId", (object?)config.OrganizationId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Name", config.Name);
            cmd.Parameters.AddWithValue("@Description", (object?)config.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Taxonomy", config.TaxonomyJson);
            cmd.Parameters.AddWithValue("@CreatedAt", config.CreatedAt);
            cmd.Parameters.AddWithValue("@UpdatedAt", config.UpdatedAt);
            cmd.Parameters.AddWithValue("@UpdatedBy", (object?)config.UpdatedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", config.IsActive);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Created taxonomy config {Id} - {Name} ({Scope})", config.Id, config.Name, config.Scope);

            return config;
        }

        public async Task<TaxonomyConfiguration> Update(TaxonomyConfiguration config)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            config.UpdatedAt = DateTime.UtcNow;

            var sql = @"UPDATE TaxonomyConfigurations 
                        SET Name = @Name, Description = @Description, Taxonomy = @Taxonomy, 
                            UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy, IsActive = @IsActive
                        WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", config.Id);
            cmd.Parameters.AddWithValue("@Name", config.Name);
            cmd.Parameters.AddWithValue("@Description", (object?)config.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Taxonomy", config.TaxonomyJson);
            cmd.Parameters.AddWithValue("@UpdatedAt", config.UpdatedAt);
            cmd.Parameters.AddWithValue("@UpdatedBy", (object?)config.UpdatedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", config.IsActive);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Updated taxonomy config {Id}", config.Id);

            return config;
        }

        public async Task<bool> Delete(Guid id)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "UPDATE TaxonomyConfigurations SET IsActive = 0, UpdatedAt = @UpdatedAt WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        public async Task<List<TaxonomyConfiguration>> GetAllIndustryTemplates()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, Scope, IndustryType, OrganizationId, Name, Description, 
                               Taxonomy, CreatedAt, UpdatedAt, UpdatedBy, IsActive
                        FROM TaxonomyConfigurations 
                        WHERE Scope = 'Industry' AND IsActive = 1
                        ORDER BY Name";

            using var cmd = new SqlCommand(sql, connection);

            var configs = new List<TaxonomyConfiguration>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                configs.Add(MapTaxonomyConfiguration(reader));
            }
            return configs;
        }

        private TaxonomyConfiguration MapTaxonomyConfiguration(SqlDataReader reader)
        {
            return new TaxonomyConfiguration
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                Scope = reader.GetString(reader.GetOrdinal("Scope")),
                IndustryType = reader.IsDBNull(reader.GetOrdinal("IndustryType")) ? null : reader.GetString(reader.GetOrdinal("IndustryType")),
                OrganizationId = reader.IsDBNull(reader.GetOrdinal("OrganizationId")) ? null : reader.GetGuid(reader.GetOrdinal("OrganizationId")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                TaxonomyJson = reader.GetString(reader.GetOrdinal("Taxonomy")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                UpdatedBy = reader.IsDBNull(reader.GetOrdinal("UpdatedBy")) ? null : reader.GetGuid(reader.GetOrdinal("UpdatedBy")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
            };
        }
    }
}