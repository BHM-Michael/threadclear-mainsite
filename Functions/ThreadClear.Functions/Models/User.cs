using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThreadClear.Functions.Models
{
    public class User
    {
        // Core identity
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";

        // Profile (NEW)
        public string? DisplayName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        // System fields
        public string Role { get; set; } = "user"; // admin, user (system-level role)
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid? CreatedBy { get; set; }

        // Authentication tracking (NEW)
        public DateTime? LastLoginAt { get; set; }
        public DateTime? EmailVerifiedAt { get; set; }
        public string? EmailVerificationToken { get; set; }
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetExpires { get; set; }

        // Preferences JSON (NEW)
        public string? PreferencesJson { get; set; }

        // EXISTING - Keep for backward compatibility
        public UserPermissions? Permissions { get; set; }

        // Parsed preferences (not stored directly in DB)
        private UserPreferences? _preferences;

        [JsonIgnore]
        public UserPreferences UserPrefs
        {
            get
            {
                if (_preferences == null)
                {
                    if (!string.IsNullOrEmpty(PreferencesJson))
                    {
                        try
                        {
                            _preferences = JsonSerializer.Deserialize<UserPreferences>(PreferencesJson);
                        }
                        catch
                        {
                            _preferences = new UserPreferences();
                        }
                    }
                    else
                    {
                        _preferences = new UserPreferences();
                    }
                }
                return _preferences;
            }
            set
            {
                _preferences = value;
                PreferencesJson = JsonSerializer.Serialize(value);
            }
        }

        // Computed properties
        [JsonIgnore]
        public string FullName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(DisplayName))
                    return DisplayName;

                if (!string.IsNullOrWhiteSpace(FirstName))
                    return $"{FirstName} {LastName}".Trim();

                return Email.Split('@')[0];
            }
        }

        [JsonIgnore]
        public bool IsEmailVerified => EmailVerifiedAt.HasValue;

        [JsonIgnore]
        public bool CanResetPassword =>
            !string.IsNullOrEmpty(PasswordResetToken) &&
            PasswordResetExpires.HasValue &&
            PasswordResetExpires > DateTime.UtcNow;

        [JsonIgnore]
        public bool IsAdmin => Role.Equals("admin", StringComparison.OrdinalIgnoreCase);
    }

    // EXISTING - Keep exactly as is
    public class UserPermissions
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public bool UnansweredQuestions { get; set; }
        public bool TensionPoints { get; set; }
        public bool Misalignments { get; set; }
        public bool ConversationHealth { get; set; }
        public bool SuggestedActions { get; set; }
    }

    // EXISTING - Keep exactly as is
    public class FeaturePricing
    {
        public Guid Id { get; set; }
        public string FeatureName { get; set; } = string.Empty;
        public decimal PricePerUse { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
    }

    // EXISTING - Keep exactly as is
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // EXISTING - Keep exactly as is
    public class LoginResponse
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public User? User { get; set; }
        public string? Error { get; set; }
    }

    // EXISTING - Keep exactly as is
    public class CreateUserRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UnansweredQuestions { get; set; }
        public bool TensionPoints { get; set; }
        public bool Misalignments { get; set; }
        public bool ConversationHealth { get; set; }
        public bool SuggestedActions { get; set; }
    }

    // NEW - User preferences
    public class UserPreferences
    {
        public string Theme { get; set; } = "system";
        public string DefaultSourceType { get; set; } = "auto";
        public string DefaultParsingMode { get; set; } = "auto";
        public bool EmailNotifications { get; set; } = true;
        public string Timezone { get; set; } = "UTC";
        public string Language { get; set; } = "en";
        public bool ShowTutorials { get; set; } = true;
    }

    // NEW - Registration DTO (for new signup flow)
    public class UserRegistration
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? OrganizationName { get; set; }
        public string? InviteToken { get; set; }
        public string? IndustryType { get; set; }
    }

    // NEW - Safe API response (excludes password hash)
    public class UserResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsActive { get; set; }
        public bool IsEmailVerified { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public UserPreferences? Preferences { get; set; }
        public UserPermissions? Permissions { get; set; }

        public static UserResponse FromUser(User user)
        {
            return new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Role = user.Role,
                IsActive = user.IsActive,
                IsEmailVerified = user.IsEmailVerified,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                Preferences = user.UserPrefs,
                Permissions = user.Permissions
            };
        }
    }

    // NEW - Authenticated user with org context
    public class AuthenticatedUser
    {
        public User User { get; set; } = null!;
        public Organization? CurrentOrganization { get; set; }
        public OrganizationMembership? Membership { get; set; }

        public Guid UserId => User.Id;
        public Guid? OrganizationId => CurrentOrganization?.Id;
        public string OrganizationRole => Membership?.Role ?? "Member";

        public bool CanManageMembers => MemberRoles.CanManageMembers(OrganizationRole);
        public bool CanManageSettings => MemberRoles.CanManageSettings(OrganizationRole);
        public bool CanManageTaxonomy => MemberRoles.CanManageTaxonomy(OrganizationRole);
        public bool CanViewInsights => MemberRoles.CanViewInsights(OrganizationRole);
        public bool IsOrgOwner => MemberRoles.CanDeleteOrganization(OrganizationRole);
        public bool IsSystemAdmin => User.IsAdmin;
    }
}