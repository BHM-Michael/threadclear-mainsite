using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;
using BCrypt.Net;

namespace ThreadClear.Functions.Services.Implementations
{
    public class RegistrationResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public User? User { get; set; }
        public Organization? Organization { get; set; }
        public string? Token { get; set; }
    }

    public class RegistrationService : IRegistrationService
    {
        private readonly IUserService _userService;
        private readonly IOrganizationService _organizationService;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ILogger<RegistrationService> _logger;

        public RegistrationService(
            IUserService userService,
            IOrganizationService organizationService,
            IOrganizationRepository organizationRepository,
            ILogger<RegistrationService> logger)
        {
            _userService = userService;
            _organizationService = organizationService;
            _organizationRepository = organizationRepository;
            _logger = logger;
        }

        public async Task<RegistrationResult> RegisterUser(UserRegistration registration)
        {
            try
            {
                // Validate email isn't already registered
                var existingUser = await _userService.GetUserByEmail(registration.Email);
                if (existingUser != null && existingUser.IsActive)
                {
                    return new RegistrationResult
                    {
                        Success = false,
                        Error = "An account with this email already exists"
                    };
                }

                // Check if joining via invite
                if (!string.IsNullOrEmpty(registration.InviteToken))
                {
                    return await AcceptInvite(registration.InviteToken, registration.Password);
                }

                // Create the user
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(registration.Password);
                var user = new User
                {
                    Id = existingUser?.Id ?? Guid.NewGuid(),
                    Email = registration.Email,
                    PasswordHash = passwordHash,
                    FirstName = registration.FirstName,
                    LastName = registration.LastName,
                    DisplayName = !string.IsNullOrEmpty(registration.FirstName) 
                        ? $"{registration.FirstName} {registration.LastName}".Trim() 
                        : null,
                    Role = "user",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    EmailVerificationToken = Guid.NewGuid().ToString("N")
                };

                // If existing placeholder user (from invite), update it
                if (existingUser != null)
                {
                    await _userService.UpdateUser(user);
                }
                else
                {
                    await _userService.CreateUserDirect(user);
                }

                // Create organization if name provided, otherwise create personal workspace
                Organization organization;
                if (!string.IsNullOrEmpty(registration.OrganizationName))
                {
                    organization = await _organizationService.CreateOrganization(
                        registration.OrganizationName,
                        registration.IndustryType ?? "default",
                        user.Id);
                }
                else
                {
                    // Create personal workspace
                    organization = await _organizationService.CreateOrganization(
                        $"{user.FullName}'s Workspace",
                        "default",
                        user.Id);
                }

                // Generate auth token
                var token = await _userService.CreateUserToken(user.Id, "registration");

                _logger.LogInformation("User {UserId} registered successfully with org {OrgId}",
                    user.Id, organization.Id);

                return new RegistrationResult
                {
                    Success = true,
                    User = user,
                    Organization = organization,
                    Token = token
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration failed for {Email}", registration.Email);
                return new RegistrationResult
                {
                    Success = false,
                    Error = "Registration failed. Please try again."
                };
            }
        }

        public async Task<RegistrationResult> AcceptInvite(string inviteToken, string password)
        {
            try
            {
                var membership = await _organizationRepository.GetByInviteToken(inviteToken);
                if (membership == null)
                {
                    return new RegistrationResult
                    {
                        Success = false,
                        Error = "Invalid or expired invite token"
                    };
                }

                // Get or create user
                var user = await _userService.GetUserById(membership.UserId);
                if (user == null)
                {
                    return new RegistrationResult
                    {
                        Success = false,
                        Error = "User not found"
                    };
                }

                // Set password and activate
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                user.IsActive = true;
                user.EmailVerifiedAt = DateTime.UtcNow;
                await _userService.UpdateUser(user);

                // Accept the membership
                membership.Status = "Active";
                membership.JoinedAt = DateTime.UtcNow;
                membership.InviteToken = null;
                await _organizationRepository.UpdateMembership(membership);

                // Get the organization
                var organization = await _organizationService.GetOrganization(membership.OrganizationId);

                // Generate token
                var token = await _userService.CreateUserToken(user.Id, "invite-accept");

                _logger.LogInformation("User {UserId} accepted invite to org {OrgId}",
                    user.Id, membership.OrganizationId);

                return new RegistrationResult
                {
                    Success = true,
                    User = user,
                    Organization = organization,
                    Token = token
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Accept invite failed for token {Token}", inviteToken);
                return new RegistrationResult
                {
                    Success = false,
                    Error = "Failed to accept invite. Please try again."
                };
            }
        }

        public async Task<bool> VerifyEmail(string verificationToken)
        {
            try
            {
                var user = await _userService.GetUserByVerificationToken(verificationToken);
                if (user == null)
                {
                    return false;
                }

                user.EmailVerifiedAt = DateTime.UtcNow;
                user.EmailVerificationToken = null;
                await _userService.UpdateUser(user);

                _logger.LogInformation("Email verified for user {UserId}", user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email verification failed for token {Token}", verificationToken);
                return false;
            }
        }

        public async Task<bool> RequestPasswordReset(string email)
        {
            try
            {
                var user = await _userService.GetUserByEmail(email);
                if (user == null || !user.IsActive)
                {
                    // Don't reveal if user exists
                    return true;
                }

                user.PasswordResetToken = Guid.NewGuid().ToString("N");
                user.PasswordResetExpires = DateTime.UtcNow.AddHours(24);
                await _userService.UpdateUser(user);

                // TODO: Send email with reset link
                _logger.LogInformation("Password reset requested for user {UserId}", user.Id);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password reset request failed for {Email}", email);
                return false;
            }
        }

        public async Task<bool> ResetPassword(string resetToken, string newPassword)
        {
            try
            {
                var user = await _userService.GetUserByResetToken(resetToken);
                if (user == null || !user.CanResetPassword)
                {
                    return false;
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                user.PasswordResetToken = null;
                user.PasswordResetExpires = null;
                await _userService.UpdateUser(user);

                _logger.LogInformation("Password reset completed for user {UserId}", user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password reset failed for token {Token}", resetToken);
                return false;
            }
        }
    }
}
