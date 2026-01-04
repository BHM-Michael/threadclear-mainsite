using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class AdminFunctions
    {
        private readonly ILogger<AdminFunctions> _logger;
        private readonly IUserService _userService;
        private readonly IOrganizationService _organizationService;
        private readonly IOrganizationRepository _organizationRepository;

        public AdminFunctions(
            ILogger<AdminFunctions> logger,
            IUserService userService,
            IOrganizationService organizationService,
            IOrganizationRepository organizationRepository)
        {
            _logger = logger;
            _userService = userService;
            _organizationService = organizationService;
            _organizationRepository = organizationRepository;
        }

        private async Task<User?> ValidateAdminFromHeaders(HttpRequestData req)
        {
            var adminEmail = req.Headers.TryGetValues("X-Admin-Email", out var emailValues)
                ? emailValues.FirstOrDefault() : null;
            var adminPassword = req.Headers.TryGetValues("X-Admin-Password", out var passValues)
                ? passValues.FirstOrDefault() : null;

            if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPassword))
                return null;

            var admin = await _userService.ValidateLogin(adminEmail, adminPassword);
            if (admin == null || admin.Role != "admin")
                return null;

            return admin;
        }

        #region User Management (Existing)

        [Function("AdminCreateUser")]
        public async Task<HttpResponseData> CreateUser(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "manage/users")] HttpRequestData req)
        {
            try
            {
                var admin = await ValidateAdminFromHeaders(req);
                if (admin == null)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteAsJsonAsync(new { error = "Admin access required" });
                    return forbidden;
                }

                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<CreateUserRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Email and password required" });
                    return badRequest;
                }

                var existingUser = await _userService.GetUserByEmail(request.Email);
                if (existingUser != null)
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteAsJsonAsync(new { error = "User already exists" });
                    return conflict;
                }

                var user = await _userService.CreateUser(request, admin.Id);

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new { success = true, user = user });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create user error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = "Create user failed", details = ex.Message });
                return error;
            }
        }

        [Function("AdminGetUsers")]
        public async Task<HttpResponseData> GetUsers(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage/users")] HttpRequestData req)
        {
            try
            {
                var admin = await ValidateAdminFromHeaders(req);
                if (admin == null)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteAsJsonAsync(new { error = "Admin access required" });
                    return forbidden;
                }

                var users = await _userService.GetAllUsers();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, users = users });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Get users error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = "Failed to get users" });
                return error;
            }
        }

        [Function("AdminUpdatePermissions")]
        public async Task<HttpResponseData> UpdateUserPermissions(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "manage/permissions/{userId}")] HttpRequestData req,
            string userId)
        {
            try
            {
                var admin = await ValidateAdminFromHeaders(req);
                if (admin == null)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteAsJsonAsync(new { error = "Admin access required" });
                    return forbidden;
                }

                var body = await req.ReadAsStringAsync();
                var permissions = JsonSerializer.Deserialize<UserPermissions>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (permissions == null)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Permissions required" });
                    return badRequest;
                }

                await _userService.UpdateUserPermissions(Guid.Parse(userId), permissions);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update permissions error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = "Update failed" });
                return error;
            }
        }

        [Function("AdminDeleteUser")]
        public async Task<HttpResponseData> DeleteUser(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "manage/user/{userId}")] HttpRequestData req,
            string userId)
        {
            try
            {
                var admin = await ValidateAdminFromHeaders(req);
                if (admin == null)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteAsJsonAsync(new { error = "Admin access required" });
                    return forbidden;
                }

                var deleted = await _userService.DeleteUser(Guid.Parse(userId));

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = deleted });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete user error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = "Delete failed" });
                return error;
            }
        }

        [Function("AdminGetPricing")]
        public async Task<HttpResponseData> GetFeaturePricing(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage/pricing")] HttpRequestData req)
        {
            try
            {
                var pricing = await _userService.GetFeaturePricing();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, pricing = pricing });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Get pricing error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = "Failed to get pricing" });
                return error;
            }
        }

        [Function("AdminUpdatePricing")]
        public async Task<HttpResponseData> UpdateFeaturePricing(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "manage/pricing/{featureName}")] HttpRequestData req,
            string featureName)
        {
            try
            {
                var admin = await ValidateAdminFromHeaders(req);
                if (admin == null)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteAsJsonAsync(new { error = "Admin access required" });
                    return forbidden;
                }

                var body = await req.ReadAsStringAsync();
                var priceData = JsonSerializer.Deserialize<Dictionary<string, decimal>>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (priceData == null || !priceData.ContainsKey("price"))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Price required" });
                    return badRequest;
                }

                await _userService.UpdateFeaturePricing(featureName, priceData["price"], admin.Id);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update pricing error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = "Update failed" });
                return error;
            }
        }

        #endregion

        #region Organization Management (New)

        [Function("AdminCreateOrganization")]
        public async Task<HttpResponseData> CreateOrganization(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "manage/organizations")] HttpRequestData req)
        {
            var admin = await ValidateAdminFromHeaders(req);
            if (admin == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Admin authentication required" });
                return unauthorized;
            }

            try
            {
                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<AdminCreateOrganizationRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || string.IsNullOrEmpty(request.Name) || string.IsNullOrEmpty(request.OwnerEmail))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "Name and ownerEmail are required" });
                    return badRequest;
                }

                // Check if owner email already exists
                var existingUser = await _userService.GetUserByEmail(request.OwnerEmail);

                // Create or get user for owner
                Guid ownerId;
                User ownerUser;
                string? inviteToken = null;

                if (existingUser != null)
                {
                    ownerId = existingUser.Id;
                    ownerUser = existingUser;
                }
                else
                {
                    // Create placeholder user with invite token
                    inviteToken = Guid.NewGuid().ToString("N");
                    ownerUser = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = request.OwnerEmail,
                        Role = "user",
                        IsActive = false, // Not active until they complete registration
                        CreatedAt = DateTime.UtcNow
                    };
                    await _userService.CreateUserDirect(ownerUser);
                    ownerId = ownerUser.Id;
                }

                // Create the organization
                var organization = await _organizationService.CreateOrganization(
                    request.Name,
                    request.IndustryType ?? "default",
                    ownerId
                );

                // Update org plan if specified
                if (!string.IsNullOrEmpty(request.Plan))
                {
                    organization.Plan = request.Plan;
                    await _organizationService.UpdateOrganization(organization);
                }

                // If new user, update membership with invite token
                if (inviteToken != null)
                {
                    var membership = await _organizationRepository.GetMembership(ownerId, organization.Id);
                    if (membership != null)
                    {
                        membership.InviteToken = inviteToken;
                        membership.Status = "Invited";
                        await _organizationRepository.UpdateMembership(membership);
                    }
                }

                // Create default permissions for owner
                await CreateDefaultPermissions(ownerId);

                _logger.LogInformation("Admin {AdminId} created organization {OrgId} for owner {OwnerEmail}",
                    admin.Id, organization.Id, request.OwnerEmail);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    organization = new
                    {
                        id = organization.Id,
                        name = organization.Name,
                        slug = organization.Slug,
                        industryType = organization.IndustryType,
                        plan = organization.Plan
                    },
                    owner = new
                    {
                        id = ownerUser.Id,
                        email = ownerUser.Email,
                        isNew = inviteToken != null
                    },
                    inviteToken = inviteToken,
                    inviteUrl = inviteToken != null ? $"https://app.threadclear.com/register?invite={inviteToken}" : null
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating organization");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { success = false, error = "Failed to create organization" });
                return error;
            }
        }

        [Function("AdminListOrganizations")]
        public async Task<HttpResponseData> ListOrganizations(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage/organizations")] HttpRequestData req)
        {
            var admin = await ValidateAdminFromHeaders(req);
            if (admin == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Admin authentication required" });
                return unauthorized;
            }

            try
            {
                var organizations = await _organizationRepository.GetAll();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    count = organizations.Count,
                    organizations = organizations.Select(o => new
                    {
                        id = o.Id,
                        name = o.Name,
                        slug = o.Slug,
                        industryType = o.IndustryType,
                        plan = o.Plan,
                        isActive = o.IsActive,
                        createdAt = o.CreatedAt
                    })
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing organizations");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { success = false, error = "Failed to list organizations" });
                return error;
            }
        }

        [Function("AdminGetOrganization")]
        public async Task<HttpResponseData> GetOrganization(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage/organizations/{orgId}")] HttpRequestData req,
            string orgId)
        {
            var admin = await ValidateAdminFromHeaders(req);
            if (admin == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Admin authentication required" });
                return unauthorized;
            }

            try
            {
                if (!Guid.TryParse(orgId, out var organizationId))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "Invalid organization ID" });
                    return badRequest;
                }

                var organization = await _organizationService.GetOrganization(organizationId);
                if (organization == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { success = false, error = "Organization not found" });
                    return notFound;
                }

                var members = await _organizationService.GetMembers(organizationId);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    organization = new
                    {
                        id = organization.Id,
                        name = organization.Name,
                        slug = organization.Slug,
                        industryType = organization.IndustryType,
                        plan = organization.Plan,
                        isActive = organization.IsActive,
                        createdAt = organization.CreatedAt
                    },
                    members = members.Select(m => new
                    {
                        userId = m.UserId,
                        email = m.User?.Email,
                        displayName = m.User?.DisplayName,
                        role = m.Role,
                        status = m.Status,
                        inviteToken = m.InviteToken,
                        joinedAt = m.JoinedAt
                    })
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organization {OrgId}", orgId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { success = false, error = "Failed to get organization" });
                return error;
            }
        }

        #endregion

        #region User Invites (New)

        [Function("AdminInviteUser")]
        public async Task<HttpResponseData> InviteUser(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "manage/organizations/{orgId}/invite")] HttpRequestData req,
            string orgId)
        {
            var admin = await ValidateAdminFromHeaders(req);
            if (admin == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Admin authentication required" });
                return unauthorized;
            }

            try
            {
                if (!Guid.TryParse(orgId, out var organizationId))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "Invalid organization ID" });
                    return badRequest;
                }

                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<AdminInviteUserRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || string.IsNullOrEmpty(request.Email))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "Email is required" });
                    return badRequest;
                }

                // Check if user already exists
                var existingUser = await _userService.GetUserByEmail(request.Email);
                Guid userId;
                string inviteToken = Guid.NewGuid().ToString("N");

                if (existingUser != null)
                {
                    // Check if already a member
                    var existingMembership = await _organizationRepository.GetMembership(existingUser.Id, organizationId);
                    if (existingMembership != null && existingMembership.Status == "Active")
                    {
                        var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                        await conflict.WriteAsJsonAsync(new { success = false, error = "User is already a member" });
                        return conflict;
                    }
                    userId = existingUser.Id;
                }
                else
                {
                    // Create placeholder user
                    var newUser = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = request.Email,
                        Role = "user",
                        IsActive = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _userService.CreateUserDirect(newUser);
                    userId = newUser.Id;

                    // Create default permissions
                    await CreateDefaultPermissions(userId);
                }

                // Create membership with invite
                var membership = new OrganizationMembership
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OrganizationId = organizationId,
                    Role = request.Role ?? "Member",
                    Status = "Invited",
                    InviteToken = inviteToken,
                    InvitedBy = admin.Id,
                    InvitedAt = DateTime.UtcNow
                };

                await _organizationRepository.AddMember(membership);

                _logger.LogInformation("Admin {AdminId} invited {Email} to org {OrgId}",
                    admin.Id, request.Email, organizationId);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    userId = userId,
                    email = request.Email,
                    role = membership.Role,
                    inviteToken = inviteToken,
                    inviteUrl = $"https://app.threadclear.com/register?invite={inviteToken}"
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inviting user to organization {OrgId}", orgId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { success = false, error = "Failed to invite user" });
                return error;
            }
        }

        [Function("AdminBulkInvite")]
        public async Task<HttpResponseData> BulkInvite(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "manage/organizations/{orgId}/bulk-invite")] HttpRequestData req,
            string orgId)
        {
            var admin = await ValidateAdminFromHeaders(req);
            if (admin == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Admin authentication required" });
                return unauthorized;
            }

            try
            {
                if (!Guid.TryParse(orgId, out var organizationId))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "Invalid organization ID" });
                    return badRequest;
                }

                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<AdminBulkInviteRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || request.Users == null || request.Users.Count == 0)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "Users list is required" });
                    return badRequest;
                }

                var results = new List<object>();
                var successCount = 0;
                var failCount = 0;

                foreach (var userRequest in request.Users)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(userRequest.Email))
                        {
                            results.Add(new { email = userRequest.Email, success = false, error = "Email required" });
                            failCount++;
                            continue;
                        }

                        // Check if user already exists
                        var existingUser = await _userService.GetUserByEmail(userRequest.Email);
                        Guid userId;
                        string inviteToken = Guid.NewGuid().ToString("N");

                        if (existingUser != null)
                        {
                            // Check if already a member
                            var existingMembership = await _organizationRepository.GetMembership(existingUser.Id, organizationId);
                            if (existingMembership != null)
                            {
                                results.Add(new { email = userRequest.Email, success = false, error = "Already a member" });
                                failCount++;
                                continue;
                            }
                            userId = existingUser.Id;
                        }
                        else
                        {
                            // Create placeholder user
                            var newUser = new User
                            {
                                Id = Guid.NewGuid(),
                                Email = userRequest.Email,
                                Role = "user",
                                IsActive = false,
                                CreatedAt = DateTime.UtcNow
                            };
                            await _userService.CreateUserDirect(newUser);
                            userId = newUser.Id;

                            // Create default permissions
                            await CreateDefaultPermissions(userId);
                        }

                        // Create membership with invite
                        var membership = new OrganizationMembership
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            OrganizationId = organizationId,
                            Role = userRequest.Role ?? request.DefaultRole ?? "Member",
                            Status = "Invited",
                            InviteToken = inviteToken,
                            InvitedBy = admin.Id,
                            InvitedAt = DateTime.UtcNow
                        };

                        await _organizationRepository.AddMember(membership);

                        results.Add(new
                        {
                            email = userRequest.Email,
                            success = true,
                            userId = userId,
                            inviteToken = inviteToken,
                            inviteUrl = $"https://app.threadclear.com/register?invite={inviteToken}"
                        });
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inviting {Email}", userRequest.Email);
                        results.Add(new { email = userRequest.Email, success = false, error = "Failed to invite" });
                        failCount++;
                    }
                }

                _logger.LogInformation("Admin {AdminId} bulk invited {Success}/{Total} users to org {OrgId}",
                    admin.Id, successCount, request.Users.Count, organizationId);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    summary = new
                    {
                        total = request.Users.Count,
                        succeeded = successCount,
                        failed = failCount
                    },
                    results
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk inviting to organization {OrgId}", orgId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { success = false, error = "Failed to process bulk invite" });
                return error;
            }
        }

        [Function("AdminResendInvite")]
        public async Task<HttpResponseData> ResendInvite(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "manage/organizations/{orgId}/resend-invite/{userId}")] HttpRequestData req,
            string orgId,
            string userId)
        {
            var admin = await ValidateAdminFromHeaders(req);
            if (admin == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Admin authentication required" });
                return unauthorized;
            }

            try
            {
                if (!Guid.TryParse(orgId, out var organizationId) || !Guid.TryParse(userId, out var uid))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "Invalid IDs" });
                    return badRequest;
                }

                var membership = await _organizationRepository.GetMembership(uid, organizationId);
                if (membership == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { success = false, error = "Membership not found" });
                    return notFound;
                }

                if (membership.Status == "Active")
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "User has already accepted invite" });
                    return badRequest;
                }

                // Generate new invite token
                var newToken = Guid.NewGuid().ToString("N");
                membership.InviteToken = newToken;
                membership.InvitedAt = DateTime.UtcNow;
                await _organizationRepository.UpdateMembership(membership);

                var user = await _userService.GetUserById(uid);

                _logger.LogInformation("Admin {AdminId} resent invite to {UserId} for org {OrgId}",
                    admin.Id, userId, organizationId);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    email = user?.Email,
                    inviteToken = newToken,
                    inviteUrl = $"https://app.threadclear.com/register?invite={newToken}"
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending invite");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { success = false, error = "Failed to resend invite" });
                return error;
            }
        }

        #endregion

        #region Helpers

        private async Task CreateDefaultPermissions(Guid userId)
        {
            try
            {
                // This creates full permissions - adjust as needed for your pricing model
                var permissions = new UserPermissions
                {
                    UnansweredQuestions = true,
                    TensionPoints = true,
                    Misalignments = true,
                    ConversationHealth = true,
                    SuggestedActions = true
                };
                await _userService.UpdateUserPermissions(userId, permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating default permissions for user {UserId}", userId);
            }
        }

        #endregion
    }

    #region Request DTOs

    public class AdminCreateOrganizationRequest
    {
        public string Name { get; set; } = "";
        public string OwnerEmail { get; set; } = "";
        public string? IndustryType { get; set; }
        public string? Plan { get; set; }
    }

    public class AdminInviteUserRequest
    {
        public string Email { get; set; } = "";
        public string? Role { get; set; }
    }

    public class AdminBulkInviteRequest
    {
        public List<AdminBulkInviteUser> Users { get; set; } = new();
        public string? DefaultRole { get; set; }
    }

    public class AdminBulkInviteUser
    {
        public string Email { get; set; } = "";
        public string? Role { get; set; }
    }

    #endregion
}