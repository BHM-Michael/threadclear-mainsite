using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class OrganizationRepository : IOrganizationRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<OrganizationRepository> _logger;

        public OrganizationRepository(string connectionString, ILogger<OrganizationRepository> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        #region Organization CRUD

        public async Task<Organization?> GetById(Guid id)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, Name, Slug, IndustryType, [Plan], Settings, 
                               CreatedAt, UpdatedAt, IsActive 
                        FROM Organizations WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapOrganization(reader);
            }
            return null;
        }

        public async Task<Organization?> GetBySlug(string slug)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, Name, Slug, IndustryType, [Plan], Settings, 
                               CreatedAt, UpdatedAt, IsActive 
                        FROM Organizations WHERE Slug = @Slug AND IsActive = 1";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Slug", slug);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapOrganization(reader);
            }
            return null;
        }

        public async Task<Organization> Create(Organization organization)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"INSERT INTO Organizations (Id, Name, Slug, IndustryType, [Plan], Settings, CreatedAt, UpdatedAt, IsActive)
                        VALUES (@Id, @Name, @Slug, @IndustryType, @Plan, @Settings, @CreatedAt, @UpdatedAt, @IsActive)";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", organization.Id);
            cmd.Parameters.AddWithValue("@Name", organization.Name);
            cmd.Parameters.AddWithValue("@Slug", (object?)organization.Slug ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IndustryType", organization.IndustryType);
            cmd.Parameters.AddWithValue("@Plan", organization.Plan);
            cmd.Parameters.AddWithValue("@Settings", (object?)organization.SettingsJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedAt", organization.CreatedAt);
            cmd.Parameters.AddWithValue("@UpdatedAt", organization.UpdatedAt);
            cmd.Parameters.AddWithValue("@IsActive", organization.IsActive);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Created organization {OrgId} - {Name}", organization.Id, organization.Name);

            return organization;
        }

        public async Task<Organization> Update(Organization organization)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            organization.UpdatedAt = DateTime.UtcNow;

            var sql = @"UPDATE Organizations 
                        SET Name = @Name, Slug = @Slug, IndustryType = @IndustryType, 
                            [Plan] = @Plan, Settings = @Settings, UpdatedAt = @UpdatedAt, IsActive = @IsActive
                        WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", organization.Id);
            cmd.Parameters.AddWithValue("@Name", organization.Name);
            cmd.Parameters.AddWithValue("@Slug", (object?)organization.Slug ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IndustryType", organization.IndustryType);
            cmd.Parameters.AddWithValue("@Plan", organization.Plan);
            cmd.Parameters.AddWithValue("@Settings", (object?)organization.SettingsJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UpdatedAt", organization.UpdatedAt);
            cmd.Parameters.AddWithValue("@IsActive", organization.IsActive);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Updated organization {OrgId}", organization.Id);

            return organization;
        }

        public async Task<bool> Delete(Guid id)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Soft delete
            var sql = "UPDATE Organizations SET IsActive = 0, UpdatedAt = @UpdatedAt WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        #endregion

        #region Membership Operations

        public async Task<List<Organization>> GetByUserId(Guid userId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT o.Id, o.Name, o.Slug, o.IndustryType, o.[Plan], o.Settings, 
                               o.CreatedAt, o.UpdatedAt, o.IsActive
                        FROM Organizations o
                        INNER JOIN OrganizationMemberships m ON o.Id = m.OrganizationId
                        WHERE m.UserId = @UserId AND m.Status = 'Active' AND o.IsActive = 1
                        ORDER BY o.Name";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);

            var orgs = new List<Organization>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                orgs.Add(MapOrganization(reader));
            }
            return orgs;
        }

        public async Task<OrganizationMembership?> GetMembership(Guid userId, Guid organizationId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT m.Id, m.UserId, m.OrganizationId, m.Role, m.Status, 
                               m.JoinedAt, m.InvitedAt, m.InvitedBy, m.InviteToken,
                               u.Email, u.DisplayName, u.FirstName, u.LastName,
                               o.Name as OrgName, o.Slug, o.IndustryType
                        FROM OrganizationMemberships m
                        INNER JOIN Users u ON m.UserId = u.Id
                        INNER JOIN Organizations o ON m.OrganizationId = o.Id
                        WHERE m.UserId = @UserId AND m.OrganizationId = @OrganizationId";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@OrganizationId", organizationId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapMembershipWithDetails(reader);
            }
            return null;
        }

        public async Task<List<OrganizationMembership>> GetMembers(Guid organizationId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT m.Id, m.UserId, m.OrganizationId, m.Role, m.Status, 
                               m.JoinedAt, m.InvitedAt, m.InvitedBy, m.InviteToken,
                               u.Email, u.DisplayName, u.FirstName, u.LastName, u.IsActive as UserActive
                        FROM OrganizationMemberships m
                        INNER JOIN Users u ON m.UserId = u.Id
                        WHERE m.OrganizationId = @OrganizationId AND m.Status != 'Removed'
                        ORDER BY m.Role DESC, u.Email";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@OrganizationId", organizationId);

            var members = new List<OrganizationMembership>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                members.Add(MapMembershipWithUser(reader));
            }
            return members;
        }

        public async Task<OrganizationMembership> AddMember(OrganizationMembership membership)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"INSERT INTO OrganizationMemberships 
                        (Id, UserId, OrganizationId, Role, Status, JoinedAt, InvitedAt, InvitedBy, InviteToken)
                        VALUES (@Id, @UserId, @OrganizationId, @Role, @Status, @JoinedAt, @InvitedAt, @InvitedBy, @InviteToken)";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", membership.Id);
            cmd.Parameters.AddWithValue("@UserId", membership.UserId);
            cmd.Parameters.AddWithValue("@OrganizationId", membership.OrganizationId);
            cmd.Parameters.AddWithValue("@Role", membership.Role);
            cmd.Parameters.AddWithValue("@Status", membership.Status);
            cmd.Parameters.AddWithValue("@JoinedAt", membership.JoinedAt);
            cmd.Parameters.AddWithValue("@InvitedAt", (object?)membership.InvitedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InvitedBy", (object?)membership.InvitedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InviteToken", (object?)membership.InviteToken ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Added member {UserId} to org {OrgId} as {Role}",
                membership.UserId, membership.OrganizationId, membership.Role);

            return membership;
        }

        public async Task<OrganizationMembership> UpdateMembership(OrganizationMembership membership)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"UPDATE OrganizationMemberships 
                        SET Role = @Role, Status = @Status, InviteToken = @InviteToken
                        WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", membership.Id);
            cmd.Parameters.AddWithValue("@Role", membership.Role);
            cmd.Parameters.AddWithValue("@Status", membership.Status);
            cmd.Parameters.AddWithValue("@InviteToken", (object?)membership.InviteToken ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            return membership;
        }

        public async Task<bool> RemoveMember(Guid userId, Guid organizationId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"UPDATE OrganizationMemberships 
                        SET Status = 'Removed' 
                        WHERE UserId = @UserId AND OrganizationId = @OrganizationId";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@OrganizationId", organizationId);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        #endregion

        #region Invite Operations

        public async Task<OrganizationMembership?> GetByInviteToken(string token)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT m.Id, m.UserId, m.OrganizationId, m.Role, m.Status, 
                               m.JoinedAt, m.InvitedAt, m.InvitedBy, m.InviteToken,
                               o.Name as OrgName, o.Slug, o.IndustryType
                        FROM OrganizationMemberships m
                        INNER JOIN Organizations o ON m.OrganizationId = o.Id
                        WHERE m.InviteToken = @Token AND m.Status = 'Invited'";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Token", token);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var membership = MapMembership(reader);
                membership.Organization = new Organization
                {
                    Id = membership.OrganizationId,
                    Name = reader.GetString(reader.GetOrdinal("OrgName")),
                    Slug = reader.IsDBNull(reader.GetOrdinal("Slug")) ? null : reader.GetString(reader.GetOrdinal("Slug")),
                    IndustryType = reader.GetString(reader.GetOrdinal("IndustryType"))
                };
                return membership;
            }
            return null;
        }

        public async Task<OrganizationMembership> CreateInvite(Guid organizationId, string email, string role, Guid invitedBy)
        {
            // First check if user exists
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Find or create a placeholder for the user
            var findUserSql = "SELECT Id FROM Users WHERE Email = @Email";
            using var findCmd = new SqlCommand(findUserSql, connection);
            findCmd.Parameters.AddWithValue("@Email", email);

            var userIdObj = await findCmd.ExecuteScalarAsync();
            Guid userId;

            if (userIdObj == null)
            {
                // Create placeholder user (they'll set password when accepting invite)
                userId = Guid.NewGuid();
                var createUserSql = @"INSERT INTO Users (Id, Email, PasswordHash, Role, IsActive, CreatedAt)
                                      VALUES (@Id, @Email, '', 'user', 0, @CreatedAt)";
                using var createCmd = new SqlCommand(createUserSql, connection);
                createCmd.Parameters.AddWithValue("@Id", userId);
                createCmd.Parameters.AddWithValue("@Email", email);
                createCmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
                await createCmd.ExecuteNonQueryAsync();
            }
            else
            {
                userId = (Guid)userIdObj;
            }

            // Create the invite
            var membership = new OrganizationMembership
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = organizationId,
                Role = role,
                Status = "Invited",
                InvitedAt = DateTime.UtcNow,
                InvitedBy = invitedBy,
                InviteToken = Guid.NewGuid().ToString("N")
            };

            return await AddMember(membership);
        }

        #endregion

        #region Mappers

        private Organization MapOrganization(SqlDataReader reader)
        {
            return new Organization
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Slug = reader.IsDBNull(reader.GetOrdinal("Slug")) ? null : reader.GetString(reader.GetOrdinal("Slug")),
                IndustryType = reader.GetString(reader.GetOrdinal("IndustryType")),
                Plan = reader.GetString(reader.GetOrdinal("Plan")),
                SettingsJson = reader.IsDBNull(reader.GetOrdinal("Settings")) ? null : reader.GetString(reader.GetOrdinal("Settings")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
            };
        }

        private OrganizationMembership MapMembership(SqlDataReader reader)
        {
            return new OrganizationMembership
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
                OrganizationId = reader.GetGuid(reader.GetOrdinal("OrganizationId")),
                Role = reader.GetString(reader.GetOrdinal("Role")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                JoinedAt = reader.GetDateTime(reader.GetOrdinal("JoinedAt")),
                InvitedAt = reader.IsDBNull(reader.GetOrdinal("InvitedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("InvitedAt")),
                InvitedBy = reader.IsDBNull(reader.GetOrdinal("InvitedBy")) ? null : reader.GetGuid(reader.GetOrdinal("InvitedBy")),
                InviteToken = reader.IsDBNull(reader.GetOrdinal("InviteToken")) ? null : reader.GetString(reader.GetOrdinal("InviteToken"))
            };
        }

        private OrganizationMembership MapMembershipWithUser(SqlDataReader reader)
        {
            var membership = MapMembership(reader);
            membership.User = new User
            {
                Id = membership.UserId,
                Email = reader.GetString(reader.GetOrdinal("Email")),
                DisplayName = reader.IsDBNull(reader.GetOrdinal("DisplayName")) ? null : reader.GetString(reader.GetOrdinal("DisplayName")),
                FirstName = reader.IsDBNull(reader.GetOrdinal("FirstName")) ? null : reader.GetString(reader.GetOrdinal("FirstName")),
                LastName = reader.IsDBNull(reader.GetOrdinal("LastName")) ? null : reader.GetString(reader.GetOrdinal("LastName")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("UserActive"))
            };
            return membership;
        }

        private OrganizationMembership MapMembershipWithDetails(SqlDataReader reader)
        {
            var membership = MapMembership(reader);
            membership.User = new User
            {
                Id = membership.UserId,
                Email = reader.GetString(reader.GetOrdinal("Email")),
                DisplayName = reader.IsDBNull(reader.GetOrdinal("DisplayName")) ? null : reader.GetString(reader.GetOrdinal("DisplayName")),
                FirstName = reader.IsDBNull(reader.GetOrdinal("FirstName")) ? null : reader.GetString(reader.GetOrdinal("FirstName")),
                LastName = reader.IsDBNull(reader.GetOrdinal("LastName")) ? null : reader.GetString(reader.GetOrdinal("LastName"))
            };
            membership.Organization = new Organization
            {
                Id = membership.OrganizationId,
                Name = reader.GetString(reader.GetOrdinal("OrgName")),
                Slug = reader.IsDBNull(reader.GetOrdinal("Slug")) ? null : reader.GetString(reader.GetOrdinal("Slug")),
                IndustryType = reader.GetString(reader.GetOrdinal("IndustryType"))
            };
            return membership;
        }

        #endregion

        public async Task<List<Organization>> GetAll()
        {
            var organizations = new List<Organization>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Id, Name, Slug, IndustryType, [Plan], Settings, IsActive, CreatedAt, UpdatedAt 
                FROM Organizations 
                ORDER BY CreatedAt DESC";

            using var cmd = new SqlCommand(sql, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                organizations.Add(new Organization
                {
                    Id = reader.GetGuid(0),
                    Name = reader.GetString(1),
                    Slug = reader.GetString(2),
                    IndustryType = reader.IsDBNull(3) ? "default" : reader.GetString(3),
                    Plan = reader.IsDBNull(4) ? "free" : reader.GetString(4),
                    Settings = reader.IsDBNull(5) ? null : JsonSerializer.Deserialize<OrganizationSettings>(reader.GetString(5)),
                    IsActive = reader.GetBoolean(6),
                    CreatedAt = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7),
                    UpdatedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                });
            }

            return organizations;
        }
    }
}