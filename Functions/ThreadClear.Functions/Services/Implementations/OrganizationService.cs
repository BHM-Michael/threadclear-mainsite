using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class OrganizationService : IOrganizationService
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserService _userService;
        private readonly ILogger<OrganizationService> _logger;

        public OrganizationService(
            IOrganizationRepository organizationRepository,
            IUserService userService,
            ILogger<OrganizationService> logger)
        {
            _organizationRepository = organizationRepository;
            _userService = userService;
            _logger = logger;
        }

        public async Task<Organization> CreateOrganization(string name, string industryType, Guid ownerId)
        {
            var slug = GenerateSlug(name);
            
            // Ensure slug is unique
            var existing = await _organizationRepository.GetBySlug(slug);
            if (existing != null)
            {
                slug = $"{slug}-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
            }

            var organization = new Organization
            {
                Id = Guid.NewGuid(),
                Name = name,
                Slug = slug,
                IndustryType = industryType,
                Plan = "free",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _organizationRepository.Create(organization);

            // Add owner as member
            var membership = new OrganizationMembership
            {
                Id = Guid.NewGuid(),
                UserId = ownerId,
                OrganizationId = organization.Id,
                Role = MemberRoles.Owner,
                Status = "Active",
                JoinedAt = DateTime.UtcNow
            };

            await _organizationRepository.AddMember(membership);

            _logger.LogInformation("Created organization {OrgId} - {Name} with owner {OwnerId}", 
                organization.Id, name, ownerId);

            return organization;
        }

        public async Task<Organization?> GetOrganization(Guid organizationId)
        {
            return await _organizationRepository.GetById(organizationId);
        }

        public async Task<Organization?> GetOrganizationBySlug(string slug)
        {
            return await _organizationRepository.GetBySlug(slug);
        }

        public async Task<Organization> UpdateOrganization(Organization organization)
        {
            return await _organizationRepository.Update(organization);
        }

        public async Task<bool> DeleteOrganization(Guid organizationId, Guid requestingUserId)
        {
            var membership = await _organizationRepository.GetMembership(requestingUserId, organizationId);
            if (membership == null || !MemberRoles.CanDeleteOrganization(membership.Role))
            {
                _logger.LogWarning("User {UserId} attempted to delete org {OrgId} without permission", 
                    requestingUserId, organizationId);
                return false;
            }

            return await _organizationRepository.Delete(organizationId);
        }

        public async Task<List<Organization>> GetUserOrganizations(Guid userId)
        {
            return await _organizationRepository.GetByUserId(userId);
        }

        public async Task<Organization?> GetUserDefaultOrganization(Guid userId)
        {
            var orgs = await _organizationRepository.GetByUserId(userId);
            return orgs.Count > 0 ? orgs[0] : null;
        }

        public async Task<OrganizationMembership?> GetMembership(Guid userId, Guid organizationId)
        {
            return await _organizationRepository.GetMembership(userId, organizationId);
        }

        public async Task<List<OrganizationMembership>> GetMembers(Guid organizationId)
        {
            return await _organizationRepository.GetMembers(organizationId);
        }

        public async Task<OrganizationMembership> InviteMember(Guid organizationId, string email, string role, Guid invitedBy)
        {
            // Verify inviter has permission
            var inviterMembership = await _organizationRepository.GetMembership(invitedBy, organizationId);
            if (inviterMembership == null || !MemberRoles.CanManageMembers(inviterMembership.Role))
            {
                throw new UnauthorizedAccessException("You don't have permission to invite members");
            }

            // Check if already a member
            var existingUser = await _userService.GetUserByEmail(email);
            if (existingUser != null)
            {
                var existingMembership = await _organizationRepository.GetMembership(existingUser.Id, organizationId);
                if (existingMembership != null && existingMembership.Status == "Active")
                {
                    throw new InvalidOperationException("User is already a member of this organization");
                }
            }

            return await _organizationRepository.CreateInvite(organizationId, email, role, invitedBy);
        }

        public async Task<OrganizationMembership> AcceptInvite(string inviteToken, Guid userId, string password)
        {
            var membership = await _organizationRepository.GetByInviteToken(inviteToken);
            if (membership == null)
            {
                throw new InvalidOperationException("Invalid or expired invite token");
            }

            // Update membership
            membership.Status = "Active";
            membership.JoinedAt = DateTime.UtcNow;
            membership.InviteToken = null;

            await _organizationRepository.UpdateMembership(membership);

            _logger.LogInformation("User {UserId} accepted invite to org {OrgId}", userId, membership.OrganizationId);

            return membership;
        }

        public async Task<OrganizationMembership> UpdateMemberRole(Guid organizationId, Guid userId, string newRole, Guid requestingUserId)
        {
            // Verify requester has permission
            var requesterMembership = await _organizationRepository.GetMembership(requestingUserId, organizationId);
            if (requesterMembership == null || !MemberRoles.CanManageMembers(requesterMembership.Role))
            {
                throw new UnauthorizedAccessException("You don't have permission to update member roles");
            }

            // Can't change owner role unless you're the owner
            var targetMembership = await _organizationRepository.GetMembership(userId, organizationId);
            if (targetMembership == null)
            {
                throw new InvalidOperationException("User is not a member of this organization");
            }

            if (targetMembership.Role == MemberRoles.Owner && requesterMembership.Role != MemberRoles.Owner)
            {
                throw new UnauthorizedAccessException("Only the owner can change the owner role");
            }

            targetMembership.Role = newRole;
            return await _organizationRepository.UpdateMembership(targetMembership);
        }

        public async Task<bool> RemoveMember(Guid organizationId, Guid userId, Guid requestingUserId)
        {
            // Verify requester has permission
            var requesterMembership = await _organizationRepository.GetMembership(requestingUserId, organizationId);
            if (requesterMembership == null || !MemberRoles.CanManageMembers(requesterMembership.Role))
            {
                throw new UnauthorizedAccessException("You don't have permission to remove members");
            }

            // Can't remove the owner
            var targetMembership = await _organizationRepository.GetMembership(userId, organizationId);
            if (targetMembership?.Role == MemberRoles.Owner)
            {
                throw new InvalidOperationException("Cannot remove the organization owner");
            }

            return await _organizationRepository.RemoveMember(userId, organizationId);
        }

        public async Task<bool> CanUserAccessOrganization(Guid userId, Guid organizationId)
        {
            var membership = await _organizationRepository.GetMembership(userId, organizationId);
            return membership != null && membership.Status == "Active";
        }

        public async Task<bool> CanUserManageOrganization(Guid userId, Guid organizationId)
        {
            var membership = await _organizationRepository.GetMembership(userId, organizationId);
            return membership != null && 
                   membership.Status == "Active" && 
                   MemberRoles.CanManageSettings(membership.Role);
        }

        private string GenerateSlug(string name)
        {
            var slug = name.ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');
            return slug.Length > 50 ? slug.Substring(0, 50) : slug;
        }
    }
}
