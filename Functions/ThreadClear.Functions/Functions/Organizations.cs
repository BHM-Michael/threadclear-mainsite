using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;
using ThreadClear.Functions.Helpers;

namespace ThreadClear.Functions.Functions
{
    public class Organizations
    {
        private readonly IOrganizationService _organizationService;
        private readonly IUserService _userService;
        private readonly ILogger<Organizations> _logger;

        public Organizations(
            IOrganizationService organizationService,
            IUserService userService,
            ILogger<Organizations> logger)
        {
            _organizationService = organizationService;
            _userService = userService;
            _logger = logger;
        }

        private async Task<User?> AuthenticateRequest(HttpRequestData req)
        {
            return await AuthHelper.AuthenticateRequest(req, _userService);
        }

        [Function("GetMyOrganizations")]
        public async Task<HttpResponseData> GetMyOrganizations(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "organizations")] HttpRequestData req)
        {
            var user = await AuthenticateRequest(req);
            if (user == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Authentication required" });
                return unauthorized;
            }

            var organizations = await _organizationService.GetUserOrganizations(user.Id);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                organizations = organizations.Select(o => new
                {
                    id = o.Id,
                    name = o.Name,
                    slug = o.Slug,
                    industryType = o.IndustryType,
                    plan = o.Plan,
                    isActive = o.IsActive
                })
            });
            return response;
        }

        [Function("GetOrganization")]
        public async Task<HttpResponseData> GetOrganization(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "organizations/{orgId}")] HttpRequestData req,
            string orgId)
        {
            var user = await AuthenticateRequest(req);
            if (user == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Authentication required" });
                return unauthorized;
            }

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

            var membership = await _organizationService.GetMembership(user.Id, organizationId);
            if (membership == null)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteAsJsonAsync(new { success = false, error = "Access denied" });
                return forbidden;
            }

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
                    settings = organization.Settings,
                    isActive = organization.IsActive,
                    createdAt = organization.CreatedAt
                },
                membership = new
                {
                    role = membership.Role,
                    status = membership.Status,
                    joinedAt = membership.JoinedAt
                }
            });
            return response;
        }

        [Function("CreateOrganization")]
        public async Task<HttpResponseData> CreateOrganization(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "organizations")] HttpRequestData req)
        {
            var user = await AuthenticateRequest(req);
            if (user == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Authentication required" });
                return unauthorized;
            }

            try
            {
                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<CreateOrgRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || string.IsNullOrEmpty(request.Name))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "Organization name is required" });
                    return badRequest;
                }

                var organization = await _organizationService.CreateOrganization(
                    request.Name,
                    request.IndustryType ?? "default",
                    user.Id);

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
                    }
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

        [Function("UpdateOrganization")]
        public async Task<HttpResponseData> UpdateOrganization(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "organizations/{orgId}")] HttpRequestData req,
            string orgId)
        {
            var user = await AuthenticateRequest(req);
            if (user == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Authentication required" });
                return unauthorized;
            }

            if (!Guid.TryParse(orgId, out var organizationId))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { success = false, error = "Invalid organization ID" });
                return badRequest;
            }

            if (!await _organizationService.CanUserManageOrganization(user.Id, organizationId))
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteAsJsonAsync(new { success = false, error = "Access denied" });
                return forbidden;
            }

            try
            {
                var organization = await _organizationService.GetOrganization(organizationId);
                if (organization == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { success = false, error = "Organization not found" });
                    return notFound;
                }

                var body = await req.ReadAsStringAsync();
                var updates = JsonSerializer.Deserialize<UpdateOrgRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (updates != null)
                {
                    if (!string.IsNullOrEmpty(updates.Name)) organization.Name = updates.Name;
                    if (!string.IsNullOrEmpty(updates.IndustryType)) organization.IndustryType = updates.IndustryType;
                    if (updates.Settings != null) organization.Settings = updates.Settings;
                }

                organization.UpdatedAt = DateTime.UtcNow;
                var updated = await _organizationService.UpdateOrganization(organization);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    organization = new
                    {
                        id = updated.Id,
                        name = updated.Name,
                        slug = updated.Slug,
                        industryType = updated.IndustryType,
                        plan = updated.Plan,
                        settings = updated.Settings
                    }
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating organization {OrgId}", orgId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { success = false, error = "Failed to update organization" });
                return error;
            }
        }

        [Function("GetOrganizationMembers")]
        public async Task<HttpResponseData> GetOrganizationMembers(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "organizations/{orgId}/members")] HttpRequestData req,
            string orgId)
        {
            var user = await AuthenticateRequest(req);
            if (user == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Authentication required" });
                return unauthorized;
            }

            if (!Guid.TryParse(orgId, out var organizationId))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { success = false, error = "Invalid organization ID" });
                return badRequest;
            }

            if (!await _organizationService.CanUserAccessOrganization(user.Id, organizationId))
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteAsJsonAsync(new { success = false, error = "Access denied" });
                return forbidden;
            }

            var members = await _organizationService.GetMembers(organizationId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                members = members.Select(m => new
                {
                    userId = m.UserId,
                    email = m.User?.Email,
                    displayName = m.User?.DisplayName,
                    role = m.Role,
                    status = m.Status,
                    joinedAt = m.JoinedAt
                })
            });
            return response;
        }

        [Function("InviteMember")]
        public async Task<HttpResponseData> InviteMember(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "organizations/{orgId}/members/invite")] HttpRequestData req,
            string orgId)
        {
            var user = await AuthenticateRequest(req);
            if (user == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Authentication required" });
                return unauthorized;
            }

            if (!Guid.TryParse(orgId, out var organizationId))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { success = false, error = "Invalid organization ID" });
                return badRequest;
            }

            try
            {
                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<InviteMemberRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || string.IsNullOrEmpty(request.Email))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "Email is required" });
                    return badRequest;
                }

                var membership = await _organizationService.InviteMember(
                    organizationId,
                    request.Email,
                    request.Role ?? "Member",
                    user.Id);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    inviteToken = membership.InviteToken,
                    message = $"Invitation sent to {request.Email}"
                });
                return response;
            }
            catch (UnauthorizedAccessException ex)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteAsJsonAsync(new { success = false, error = ex.Message });
                return forbidden;
            }
            catch (InvalidOperationException ex)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { success = false, error = ex.Message });
                return badRequest;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inviting member to organization {OrgId}", orgId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { success = false, error = "Failed to invite member" });
                return error;
            }
        }

        [Function("UpdateMemberRole")]
        public async Task<HttpResponseData> UpdateMemberRole(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "organizations/{orgId}/members/{userId}")] HttpRequestData req,
            string orgId,
            string userId)
        {
            var user = await AuthenticateRequest(req);
            if (user == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Authentication required" });
                return unauthorized;
            }

            if (!Guid.TryParse(orgId, out var organizationId) || !Guid.TryParse(userId, out var targetUserId))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { success = false, error = "Invalid IDs" });
                return badRequest;
            }

            try
            {
                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<UpdateRoleRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || string.IsNullOrEmpty(request.Role))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "Role is required" });
                    return badRequest;
                }

                await _organizationService.UpdateMemberRole(organizationId, targetUserId, request.Role, user.Id);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true });
                return response;
            }
            catch (UnauthorizedAccessException ex)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteAsJsonAsync(new { success = false, error = ex.Message });
                return forbidden;
            }
            catch (InvalidOperationException ex)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { success = false, error = ex.Message });
                return badRequest;
            }
        }

        [Function("RemoveMember")]
        public async Task<HttpResponseData> RemoveMember(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "organizations/{orgId}/members/{userId}")] HttpRequestData req,
            string orgId,
            string userId)
        {
            var user = await AuthenticateRequest(req);
            if (user == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Authentication required" });
                return unauthorized;
            }

            if (!Guid.TryParse(orgId, out var organizationId) || !Guid.TryParse(userId, out var targetUserId))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { success = false, error = "Invalid IDs" });
                return badRequest;
            }

            try
            {
                var removed = await _organizationService.RemoveMember(organizationId, targetUserId, user.Id);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = removed });
                return response;
            }
            catch (UnauthorizedAccessException ex)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteAsJsonAsync(new { success = false, error = ex.Message });
                return forbidden;
            }
            catch (InvalidOperationException ex)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { success = false, error = ex.Message });
                return badRequest;
            }
        }
    }

    // Request DTOs
    public class CreateOrgRequest
    {
        public string Name { get; set; } = "";
        public string? IndustryType { get; set; }
    }

    public class UpdateOrgRequest
    {
        public string? Name { get; set; }
        public string? IndustryType { get; set; }
        public OrganizationSettings? Settings { get; set; }
    }

    public class InviteMemberRequest
    {
        public string Email { get; set; } = "";
        public string? Role { get; set; }
    }

    public class UpdateRoleRequest
    {
        public string Role { get; set; } = "";
    }
}
