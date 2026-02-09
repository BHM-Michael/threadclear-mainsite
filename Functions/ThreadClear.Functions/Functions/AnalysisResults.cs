using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;
using ThreadClear.Functions.Services;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class AnalysisResults
    {
        private readonly ILogger _logger;
        private readonly IAnalysisResultService _analysisResultService;
        private readonly IUserService _userService;
        private readonly IOrganizationService _organizationService;

        public AnalysisResults(
            ILoggerFactory loggerFactory,
            IAnalysisResultService analysisResultService,
            IUserService userService,
            IOrganizationService organizationService)
        {
            _logger = loggerFactory.CreateLogger<AnalysisResults>();
            _analysisResultService = analysisResultService;
            _userService = userService;
            _organizationService = organizationService;
        }

        /// <summary>
        /// Get analysis history for current user (personal hub)
        /// </summary>
        [Function("GetMyAnalyses")]
        public async Task<HttpResponseData> GetMyAnalyses(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "my-analyses")] HttpRequestData req)
        {
            if (req.Method == "OPTIONS")
            {
                return req.CreateResponse(HttpStatusCode.OK);
            }

            try
            {
                var user = await AuthenticateUser(req);
                if (user == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Authentication required");
                }

                var limit = 50;
                if (req.Query["limit"] != null && int.TryParse(req.Query["limit"], out var parsedLimit))
                {
                    limit = Math.Min(parsedLimit, 100);
                }

                var results = await _analysisResultService.GetByUserAsync(user.Id, limit);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, analyses = results });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user analyses");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to fetch analyses");
            }
        }

        /// <summary>
        /// Get analysis history for organization (org dashboard)
        /// </summary>
        [Function("GetOrgAnalyses")]
        public async Task<HttpResponseData> GetOrgAnalyses(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "analyses/org/{orgId}")] HttpRequestData req,
            string orgId)
        {
            if (req.Method == "OPTIONS")
            {
                return req.CreateResponse(HttpStatusCode.OK);
            }

            try
            {
                var user = await AuthenticateUser(req);
                if (user == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Authentication required");
                }

                if (!Guid.TryParse(orgId, out var organizationId))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid organization ID");
                }

                // Verify user belongs to this org
                var userOrgs = await _organizationService.GetUserOrganizations(user.Id);
                if (userOrgs == null || !userOrgs.Exists(o => o.Id == organizationId))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.Forbidden, "Access denied to this organization");
                }

                var limit = 100;
                if (req.Query["limit"] != null && int.TryParse(req.Query["limit"], out var parsedLimit))
                {
                    limit = Math.Min(parsedLimit, 500);
                }

                var results = await _analysisResultService.GetByOrganizationAsync(organizationId, limit);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, analyses = results });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching org analyses");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to fetch analyses");
            }
        }

        /// <summary>
        /// Get single analysis by ID
        /// </summary>
        [Function("GetAnalysisById")]
        public async Task<HttpResponseData> GetAnalysisById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "analyses/{id}")] HttpRequestData req,
            string id)
        {
            if (req.Method == "OPTIONS")
            {
                return req.CreateResponse(HttpStatusCode.OK);
            }

            try
            {
                var user = await AuthenticateUser(req);
                if (user == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Authentication required");
                }

                if (!Guid.TryParse(id, out var analysisId))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid analysis ID");
                }

                var result = await _analysisResultService.GetByIdAsync(analysisId);
                if (result == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Analysis not found");
                }

                // Verify user has access (owns it or belongs to same org)
                if (result.UserId != user.Id)
                {
                    if (result.OrganizationId.HasValue)
                    {
                        var userOrgs = await _organizationService.GetUserOrganizations(user.Id);
                        if (userOrgs == null || !userOrgs.Exists(o => o.Id == result.OrganizationId))
                        {
                            return await CreateErrorResponse(req, HttpStatusCode.Forbidden, "Access denied");
                        }
                    }
                    else
                    {
                        return await CreateErrorResponse(req, HttpStatusCode.Forbidden, "Access denied");
                    }
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, analysis = result });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching analysis {Id}", id);
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to fetch analysis");
            }
        }

        private async Task<Models.User?> AuthenticateUser(HttpRequestData req)
        {
            Models.User? authenticatedUser = null;

            if (req.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                var authHeader = authHeaders.FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring("Bearer ".Length);
                    authenticatedUser = await _userService.ValidateToken(token);
                }
            }

            if (authenticatedUser == null)
            {
                var email = req.Headers.TryGetValues("X-User-Email", out var emailValues) ? emailValues.FirstOrDefault() : null;
                var password = req.Headers.TryGetValues("X-User-Password", out var passValues) ? passValues.FirstOrDefault() : null;

                if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
                {
                    authenticatedUser = await _userService.ValidateLogin(email, password);
                }
            }

            return authenticatedUser;
        }

        private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
        {
            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(new { success = false, error = message });
            return response;
        }
    }
}