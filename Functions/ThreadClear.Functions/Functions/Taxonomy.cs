using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Data;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class Taxonomy
    {
        private readonly ITaxonomyService _taxonomyService;
        private readonly IOrganizationService _organizationService;
        private readonly IUserService _userService;
        private readonly ILogger<Taxonomy> _logger;

        public Taxonomy(
            ITaxonomyService taxonomyService,
            IOrganizationService organizationService,
            IUserService userService,
            ILogger<Taxonomy> logger)
        {
            _taxonomyService = taxonomyService;
            _organizationService = organizationService;
            _userService = userService;
            _logger = logger;
        }

        [Function("GetIndustryTypes")]
        public async Task<HttpResponseData> GetIndustryTypes(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "taxonomy/industries")] HttpRequestData req)
        {
            var industries = IndustryTemplates.GetAvailableIndustries();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                industries = industries.Select(i => new
                {
                    key = i,
                    displayName = i.Substring(0, 1).ToUpper() + i.Substring(1)
                })
            });
            return response;
        }

        [Function("GetIndustryTemplate")]
        public async Task<HttpResponseData> GetIndustryTemplate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "taxonomy/industries/{industryType}")] HttpRequestData req,
            string industryType)
        {
            var template = await _taxonomyService.GetIndustryTemplate(industryType);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                industryType,
                taxonomy = template
            });
            return response;
        }

        [Function("GetOrganizationTaxonomy")]
        public async Task<HttpResponseData> GetOrganizationTaxonomy(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "organizations/{orgId}/taxonomy")] HttpRequestData req,
            string orgId)
        {
            var user = await ValidateRequest(req);
            if (user == null)
            {
                return await UnauthorizedResponse(req);
            }

            if (!Guid.TryParse(orgId, out var organizationId))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Invalid organization ID" });
                return badRequest;
            }

            if (!await _organizationService.CanUserAccessOrganization(user.Id, organizationId))
            {
                return await ForbiddenResponse(req);
            }

            var taxonomy = await _taxonomyService.GetTaxonomyForOrganization(organizationId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                taxonomy
            });
            return response;
        }

        [Function("UpdateOrganizationTaxonomy")]
        public async Task<HttpResponseData> UpdateOrganizationTaxonomy(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "organizations/{orgId}/taxonomy")] HttpRequestData req,
            string orgId)
        {
            var user = await ValidateRequest(req);
            if (user == null)
            {
                return await UnauthorizedResponse(req);
            }

            if (!Guid.TryParse(orgId, out var organizationId))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Invalid organization ID" });
                return badRequest;
            }

            if (!await _organizationService.CanUserManageOrganization(user.Id, organizationId))
            {
                return await ForbiddenResponse(req);
            }

            var requestBody = await req.ReadAsStringAsync();
            var taxonomy = JsonSerializer.Deserialize<TaxonomyData>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (taxonomy == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Invalid taxonomy data" });
                return badRequest;
            }

            await _taxonomyService.SaveOrganizationTaxonomy(organizationId, taxonomy, user.Id);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { success = true });
            return response;
        }

        [Function("AddCustomTopic")]
        public async Task<HttpResponseData> AddCustomTopic(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "organizations/{orgId}/taxonomy/topics")] HttpRequestData req,
            string orgId)
        {
            var user = await ValidateRequest(req);
            if (user == null)
            {
                return await UnauthorizedResponse(req);
            }

            if (!Guid.TryParse(orgId, out var organizationId))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Invalid organization ID" });
                return badRequest;
            }

            if (!await _organizationService.CanUserManageOrganization(user.Id, organizationId))
            {
                return await ForbiddenResponse(req);
            }

            var requestBody = await req.ReadAsStringAsync();
            var topic = JsonSerializer.Deserialize<TopicDefinition>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (topic == null || string.IsNullOrEmpty(topic.Key))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Topic key is required" });
                return badRequest;
            }

            var taxonomy = await _taxonomyService.AddCustomTopic(organizationId, topic, user.Id);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                topic,
                totalTopics = taxonomy.Topics.Count
            });
            return response;
        }

        [Function("RemoveCustomTopic")]
        public async Task<HttpResponseData> RemoveCustomTopic(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "organizations/{orgId}/taxonomy/topics/{topicKey}")] HttpRequestData req,
            string orgId, string topicKey)
        {
            var user = await ValidateRequest(req);
            if (user == null)
            {
                return await UnauthorizedResponse(req);
            }

            if (!Guid.TryParse(orgId, out var organizationId))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Invalid organization ID" });
                return badRequest;
            }

            if (!await _organizationService.CanUserManageOrganization(user.Id, organizationId))
            {
                return await ForbiddenResponse(req);
            }

            await _taxonomyService.RemoveCustomTopic(organizationId, topicKey, user.Id);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { success = true });
            return response;
        }

        [Function("AddCustomRole")]
        public async Task<HttpResponseData> AddCustomRole(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "organizations/{orgId}/taxonomy/roles")] HttpRequestData req,
            string orgId)
        {
            var user = await ValidateRequest(req);
            if (user == null)
            {
                return await UnauthorizedResponse(req);
            }

            if (!Guid.TryParse(orgId, out var organizationId))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Invalid organization ID" });
                return badRequest;
            }

            if (!await _organizationService.CanUserManageOrganization(user.Id, organizationId))
            {
                return await ForbiddenResponse(req);
            }

            var requestBody = await req.ReadAsStringAsync();
            var role = JsonSerializer.Deserialize<RoleDefinition>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (role == null || string.IsNullOrEmpty(role.Key))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Role key is required" });
                return badRequest;
            }

            var taxonomy = await _taxonomyService.AddCustomRole(organizationId, role, user.Id);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                role,
                totalRoles = taxonomy.Roles.Count
            });
            return response;
        }

        [Function("RemoveCustomRole")]
        public async Task<HttpResponseData> RemoveCustomRole(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "organizations/{orgId}/taxonomy/roles/{roleKey}")] HttpRequestData req,
            string orgId, string roleKey)
        {
            var user = await ValidateRequest(req);
            if (user == null)
            {
                return await UnauthorizedResponse(req);
            }

            if (!Guid.TryParse(orgId, out var organizationId))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Invalid organization ID" });
                return badRequest;
            }

            if (!await _organizationService.CanUserManageOrganization(user.Id, organizationId))
            {
                return await ForbiddenResponse(req);
            }

            await _taxonomyService.RemoveCustomRole(organizationId, roleKey, user.Id);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { success = true });
            return response;
        }

        private async Task<User?> ValidateRequest(HttpRequestData req)
        {
            // Try Bearer token first
            if (req.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                var token = authHeaders.FirstOrDefault()?.Replace("Bearer ", "");
                if (!string.IsNullOrEmpty(token) && token != "null")
                {
                    return await _userService.ValidateToken(token);
                }
            }

            // Fall back to email/password headers
            if (req.Headers.TryGetValues("X-User-Email", out var emailHeaders) &&
                req.Headers.TryGetValues("X-User-Password", out var passwordHeaders))
            {
                var email = emailHeaders.FirstOrDefault();
                var password = passwordHeaders.FirstOrDefault();

                if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
                {
                    return await _userService.ValidateLogin(email, password);
                }
            }

            return null;
        }

        private async Task<HttpResponseData> UnauthorizedResponse(HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.Unauthorized);
            await response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return response;
        }

        private async Task<HttpResponseData> ForbiddenResponse(HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.Forbidden);
            await response.WriteAsJsonAsync(new { error = "Access denied" });
            return response;
        }
    }
}
