using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IOrganizationRepository
    {
        // Organization CRUD
        Task<Organization?> GetById(Guid id);
        Task<Organization?> GetBySlug(string slug);
        Task<Organization> Create(Organization organization);
        Task<Organization> Update(Organization organization);
        Task<bool> Delete(Guid id);

        // Membership operations
        Task<List<Organization>> GetByUserId(Guid userId);
        Task<OrganizationMembership?> GetMembership(Guid userId, Guid organizationId);
        Task<List<OrganizationMembership>> GetMembers(Guid organizationId);
        Task<OrganizationMembership> AddMember(OrganizationMembership membership);
        Task<OrganizationMembership> UpdateMembership(OrganizationMembership membership);
        Task<bool> RemoveMember(Guid userId, Guid organizationId);

        // Invite operations
        Task<OrganizationMembership?> GetByInviteToken(string token);
        Task<OrganizationMembership> CreateInvite(Guid organizationId, string email, string role, Guid invitedBy);

        Task<List<Organization>> GetAll();
    }
}