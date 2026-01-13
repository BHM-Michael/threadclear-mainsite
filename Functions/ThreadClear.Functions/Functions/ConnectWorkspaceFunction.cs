using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class ConnectWorkspaceFunction
    {
        private readonly ILogger _logger;
        private readonly ISlackWorkspaceRepository _slackRepo;
        private readonly ITeamsWorkspaceRepository _teamsRepo;
        private readonly IOrganizationService _orgService;
        private readonly IUserService _userService;

        public ConnectWorkspaceFunction(
            ILoggerFactory loggerFactory,
            ISlackWorkspaceRepository slackRepo,
            ITeamsWorkspaceRepository teamsRepo,
            IOrganizationService orgService,
            IUserService userService)
        {
            _logger = loggerFactory.CreateLogger<ConnectWorkspaceFunction>();
            _slackRepo = slackRepo;
            _teamsRepo = teamsRepo;
            _orgService = orgService;
            _userService = userService;
        }

        /// <summary>
        /// Get workspace info for the connect page
        /// GET /api/workspace/{platform}/{id}
        /// </summary>
        [Function("get-workspace")]
        public async Task<HttpResponseData> GetWorkspace(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "workspace/{platform}/{id}")]
            HttpRequestData req,
            string platform,
            string id)
        {
            _logger.LogInformation("Getting workspace info: {Platform} {Id}", platform, id);

            try
            {
                string? name = null;
                string? tier = null;
                int? usage = null;
                int? limit = null;
                bool isConnected = false;
                string? connectedOrgName = null;

                if (platform.ToLower() == "slack")
                {
                    var slack = await _slackRepo.GetByTeamIdAsync(id);
                    if (slack != null)
                    {
                        name = slack.TeamName;
                        tier = slack.Tier;
                        usage = slack.MonthlyAnalysisCount;
                        limit = slack.MonthlyAnalysisLimit;
                        isConnected = slack.OrganizationId.HasValue;

                        if (slack.OrganizationId.HasValue)
                        {
                            var org = await _orgService.GetOrganization(slack.OrganizationId.Value);
                            connectedOrgName = org?.Name;
                        }
                    }
                }
                else if (platform.ToLower() == "teams")
                {
                    var teams = await _teamsRepo.GetByTenantIdAsync(id);
                    if (teams != null)
                    {
                        name = teams.TenantName;
                        tier = teams.Tier;
                        usage = teams.MonthlyAnalysisCount;
                        limit = teams.MonthlyAnalysisLimit;
                        isConnected = teams.OrganizationId.HasValue;

                        if (teams.OrganizationId.HasValue)
                        {
                            var org = await _orgService.GetOrganization(teams.OrganizationId.Value);
                            connectedOrgName = org?.Name;
                        }
                    }
                }

                if (name == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteAsJsonAsync(new { success = false, error = "Workspace not found" });
                    return notFoundResponse;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    workspace = new
                    {
                        platform,
                        id,
                        name,
                        tier,
                        usage,
                        limit,
                        isConnected,
                        connectedOrgName
                    }
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workspace");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { success = false, error = ex.Message });
                return errorResponse;
            }
        }

        /// <summary>
        /// Connect a workspace to a ThreadClear organization
        /// POST /api/connect-workspace
        /// Body: { platform, workspaceId, organizationId }
        /// Requires authenticated user who is admin of the organization
        /// </summary>
        [Function("connect-workspace")]
        public async Task<HttpResponseData> ConnectWorkspace(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "connect-workspace")]
            HttpRequestData req)
        {
            _logger.LogInformation("Connect workspace request");

            try
            {
                // Authenticate user (same pattern as AnalyzeConversation)
                User? user = null;

                // Check for Bearer token first
                if (req.Headers.TryGetValues("Authorization", out var authHeaders))
                {
                    var authHeader = authHeaders.FirstOrDefault();
                    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                    {
                        var token = authHeader.Substring("Bearer ".Length);
                        user = await _userService.ValidateToken(token);
                    }
                }

                // If no Bearer token, check for email/password headers
                if (user == null)
                {
                    var email = req.Headers.TryGetValues("X-User-Email", out var emailValues) ? emailValues.FirstOrDefault() : null;
                    var password = req.Headers.TryGetValues("X-User-Password", out var passValues) ? passValues.FirstOrDefault() : null;

                    if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
                    {
                        user = await _userService.ValidateLogin(email, password);
                    }
                }

                if (user == null)
                {
                    var unauthResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauthResponse.WriteAsJsonAsync(new { success = false, error = "Authentication required" });
                    return unauthResponse;
                }

                // Parse request body
                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<ConnectRequest>(body ?? "", new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || string.IsNullOrEmpty(request.Platform) || string.IsNullOrEmpty(request.WorkspaceId))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { success = false, error = "Platform and workspaceId required" });
                    return badResponse;
                }

                // Get user's organization via membership
                Guid? userOrgId = null;
                var userOrgs = await _orgService.GetUserOrganizations(user.Id);
                if (userOrgs.Any())
                {
                    // Use the first org (or default org if we add that later)
                    userOrgId = userOrgs.First().Id;
                }

                var orgId = request.OrganizationId ?? userOrgId;

                if (!orgId.HasValue)
                {
                    var noOrgResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await noOrgResponse.WriteAsJsonAsync(new { success = false, error = "You must belong to an organization to connect a workspace" });
                    return noOrgResponse;
                }

                // Verify user is admin of the organization
                var org = await _orgService.GetOrganization(orgId.Value);
                if (org == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteAsJsonAsync(new { success = false, error = "Organization not found" });
                    return notFoundResponse;
                }

                // Check if user is org admin (check membership role)
                var membership = await _orgService.GetMembership(user.Id, orgId.Value);
                var isAdmin = user.IsAdmin || membership?.Role == "Admin" || membership?.Role == "Owner";
                if (!isAdmin)
                {
                    var forbiddenResponse = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbiddenResponse.WriteAsJsonAsync(new { success = false, error = "Only organization admins can connect workspaces" });
                    return forbiddenResponse;
                }

                // Get org plan to apply to workspace
                var tier = org.Plan == "free" ? "free" : "pro";

                // Connect the workspace
                if (request.Platform.ToLower() == "slack")
                {
                    var slack = await _slackRepo.GetByTeamIdAsync(request.WorkspaceId);
                    if (slack == null)
                    {
                        var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                        await notFoundResponse.WriteAsJsonAsync(new { success = false, error = "Slack workspace not found. Make sure the ThreadClear app is installed." });
                        return notFoundResponse;
                    }

                    slack.OrganizationId = orgId;
                    slack.Tier = tier;
                    slack.MonthlyAnalysisLimit = tier == "free" ? 20 : int.MaxValue;
                    await _slackRepo.UpdateAsync(slack);

                    _logger.LogInformation("Connected Slack workspace {TeamId} to org {OrgId}", request.WorkspaceId, orgId);
                }
                else if (request.Platform.ToLower() == "teams")
                {
                    var teams = await _teamsRepo.GetByTenantIdAsync(request.WorkspaceId);
                    if (teams == null)
                    {
                        var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                        await notFoundResponse.WriteAsJsonAsync(new { success = false, error = "Teams tenant not found. Make sure the ThreadClear app is installed." });
                        return notFoundResponse;
                    }

                    teams.OrganizationId = orgId;
                    teams.Tier = tier;
                    teams.MonthlyAnalysisLimit = tier == "free" ? 20 : int.MaxValue;
                    await _teamsRepo.UpdateAsync(teams);

                    _logger.LogInformation("Connected Teams tenant {TenantId} to org {OrgId}", request.WorkspaceId, orgId);
                }
                else
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { success = false, error = "Platform must be 'slack' or 'teams'" });
                    return badResponse;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    message = $"Workspace connected to {org.Name}",
                    tier
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting workspace");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { success = false, error = ex.Message });
                return errorResponse;
            }
        }

        /// <summary>
        /// Disconnect a workspace from an organization
        /// POST /api/disconnect-workspace
        /// </summary>
        [Function("disconnect-workspace")]
        public async Task<HttpResponseData> DisconnectWorkspace(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "disconnect-workspace")]
            HttpRequestData req)
        {
            _logger.LogInformation("Disconnect workspace request");

            try
            {
                // Authenticate user (same pattern as AnalyzeConversation)
                User? user = null;

                // Check for Bearer token first
                if (req.Headers.TryGetValues("Authorization", out var authHeaders))
                {
                    var authHeader = authHeaders.FirstOrDefault();
                    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                    {
                        var token = authHeader.Substring("Bearer ".Length);
                        user = await _userService.ValidateToken(token);
                    }
                }

                // If no Bearer token, check for email/password headers
                if (user == null)
                {
                    var email = req.Headers.TryGetValues("X-User-Email", out var emailValues) ? emailValues.FirstOrDefault() : null;
                    var password = req.Headers.TryGetValues("X-User-Password", out var passValues) ? passValues.FirstOrDefault() : null;

                    if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
                    {
                        user = await _userService.ValidateLogin(email, password);
                    }
                }

                if (user == null)
                {
                    var unauthResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauthResponse.WriteAsJsonAsync(new { success = false, error = "Authentication required" });
                    return unauthResponse;
                }

                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<ConnectRequest>(body ?? "", new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || string.IsNullOrEmpty(request.Platform) || string.IsNullOrEmpty(request.WorkspaceId))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { success = false, error = "Platform and workspaceId required" });
                    return badResponse;
                }

                // Get user's organizations
                var userOrgs = await _orgService.GetUserOrganizations(user.Id);
                var userOrgIds = userOrgs.Select(o => o.Id).ToList();

                // Disconnect
                if (request.Platform.ToLower() == "slack")
                {
                    var slack = await _slackRepo.GetByTeamIdAsync(request.WorkspaceId);
                    if (slack != null)
                    {
                        // Verify user has permission (is member of connected org)
                        if (slack.OrganizationId.HasValue && userOrgIds.Contains(slack.OrganizationId.Value))
                        {
                            slack.OrganizationId = null;
                            slack.Tier = "free";
                            slack.MonthlyAnalysisLimit = 20;
                            await _slackRepo.UpdateAsync(slack);
                        }
                    }
                }
                else if (request.Platform.ToLower() == "teams")
                {
                    var teams = await _teamsRepo.GetByTenantIdAsync(request.WorkspaceId);
                    if (teams != null)
                    {
                        if (teams.OrganizationId.HasValue && userOrgIds.Contains(teams.OrganizationId.Value))
                        {
                            teams.OrganizationId = null;
                            teams.Tier = "free";
                            teams.MonthlyAnalysisLimit = 20;
                            await _teamsRepo.UpdateAsync(teams);
                        }
                    }
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, message = "Workspace disconnected" });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting workspace");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { success = false, error = ex.Message });
                return errorResponse;
            }
        }

        private class ConnectRequest
        {
            public string? Platform { get; set; }
            public string? WorkspaceId { get; set; }
            public Guid? OrganizationId { get; set; }
        }
    }
}