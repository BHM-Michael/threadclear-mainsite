using System;

namespace ThreadClear.Functions.Models
{
    public class OrganizationMembership
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }
        public string Role { get; set; } = "Member"; // Member, Analyst, Admin, Owner
        public string Status { get; set; } = "Active"; // Invited, Active, Suspended, Removed
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime? InvitedAt { get; set; }
        public Guid? InvitedBy { get; set; }
        public string? InviteToken { get; set; }

        // Navigation properties (populated by service layer)
        public User? User { get; set; }
        public Organization? Organization { get; set; }
    }

    public static class MemberRoles
    {
        public const string Member = "Member";
        public const string Analyst = "Analyst";
        public const string Admin = "Admin";
        public const string Owner = "Owner";

        public static bool CanManageMembers(string role) =>
            role == Admin || role == Owner;

        public static bool CanManageSettings(string role) =>
            role == Admin || role == Owner;

        public static bool CanManageTaxonomy(string role) =>
            role == Admin || role == Owner;

        public static bool CanViewInsights(string role) =>
            role == Analyst || role == Admin || role == Owner;

        public static bool CanDeleteOrganization(string role) =>
            role == Owner;
    }
}