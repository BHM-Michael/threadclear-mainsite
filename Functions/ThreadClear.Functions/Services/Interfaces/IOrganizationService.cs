using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IOrganizationService
    {
        // Organization management
        Task<Organization> CreateOrganization(string name, string industryType, Guid ownerId);
        Task<Organization?> GetOrganization(Guid organizationId);
        Task<Organization?> GetOrganizationBySlug(string slug);
        Task<Organization> UpdateOrganization(Organization organization);
        Task<bool> DeleteOrganization(Guid organizationId, Guid requestingUserId);
        
        // User's organizations
        Task<List<Organization>> GetUserOrganizations(Guid userId);
        Task<Organization?> GetUserDefaultOrganization(Guid userId);
        
        // Membership management
        Task<OrganizationMembership?> GetMembership(Guid userId, Guid organizationId);
        Task<List<OrganizationMembership>> GetMembers(Guid organizationId);
        Task<OrganizationMembership> InviteMember(Guid organizationId, string email, string role, Guid invitedBy);
        Task<OrganizationMembership> AcceptInvite(string inviteToken, Guid userId, string password);
        Task<OrganizationMembership> UpdateMemberRole(Guid organizationId, Guid userId, string newRole, Guid requestingUserId);
        Task<bool> RemoveMember(Guid organizationId, Guid userId, Guid requestingUserId);
        
        // Validation
        Task<bool> CanUserAccessOrganization(Guid userId, Guid organizationId);
        Task<bool> CanUserManageOrganization(Guid userId, Guid organizationId);
    }
}
