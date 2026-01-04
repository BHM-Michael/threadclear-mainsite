using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;
using ThreadClear.Functions.Helpers;

namespace ThreadClear.Functions.Functions
{
    public class Insights
    {
        private readonly IInsightService _insightService;
        private readonly IOrganizationService _organizationService;
        private readonly IUserService _userService;
        private readonly ILogger<Insights> _logger;

        public Insights(
            IInsightService insightService,
            IOrganizationService organizationService,
            IUserService userService,
            ILogger<Insights> logger)
        {
            _insightService = insightService;
            _organizationService = organizationService;
            _userService = userService;
            _logger = logger;
        }

        private async Task<User?> AuthenticateRequest(HttpRequestData req)
        {
            return await AuthHelper.AuthenticateRequest(req, _userService);
        }

        [Function("GetDashboardSummary")]
        public async Task<HttpResponseData> GetDashboardSummary(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "organizations/{orgId}/insights/summary")] HttpRequestData req,
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

            // Verify user has access to this org
            if (!await _organizationService.CanUserAccessOrganization(user.Id, organizationId))
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteAsJsonAsync(new { success = false, error = "Access denied" });
                return forbidden;
            }

            // Get days parameter
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var days = int.TryParse(query["days"], out var d) ? d : 30;

            var summary = await _insightService.GetDashboardSummary(organizationId, days);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                days,
                summary
            });
            return response;
        }

        [Function("GetInsightTrends")]
        public async Task<HttpResponseData> GetInsightTrends(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "organizations/{orgId}/insights/trends")] HttpRequestData req,
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

            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var days = int.TryParse(query["days"], out var d) ? d : 30;
            var groupBy = query["groupBy"] ?? "day";

            var trends = await _insightService.GetInsightTrends(organizationId, days, groupBy);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                days,
                groupBy,
                trends
            });
            return response;
        }

        [Function("GetTopicAnalysis")]
        public async Task<HttpResponseData> GetTopicAnalysis(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "organizations/{orgId}/insights/topics")] HttpRequestData req,
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

            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var days = int.TryParse(query["days"], out var d) ? d : 30;

            var topics = await _insightService.GetTopicBreakdown(organizationId, days);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                days,
                topics
            });
            return response;
        }

        [Function("GetRecentInsights")]
        public async Task<HttpResponseData> GetRecentInsights(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "organizations/{orgId}/insights")] HttpRequestData req,
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

            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var limit = int.TryParse(query["limit"], out var l) ? l : 50;

            var insights = await _insightService.GetRecentInsights(organizationId, limit);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                count = insights.Count,
                insights
            });
            return response;
        }

        [Function("GetInsightDetail")]
        public async Task<HttpResponseData> GetInsightDetail(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "insights/{insightId}")] HttpRequestData req,
            string insightId)
        {
            var user = await AuthenticateRequest(req);
            if (user == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Authentication required" });
                return unauthorized;
            }

            if (!Guid.TryParse(insightId, out var id))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { success = false, error = "Invalid insight ID" });
                return badRequest;
            }

            var insight = await _insightService.GetInsightById(id);
            if (insight == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new { success = false, error = "Insight not found" });
                return notFound;
            }

            // Verify user has access to the insight's org
            if (!await _organizationService.CanUserAccessOrganization(user.Id, insight.OrganizationId))
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteAsJsonAsync(new { success = false, error = "Access denied" });
                return forbidden;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                insight
            });
            return response;
        }

        [Function("GetMyInsights")]
        public async Task<HttpResponseData> GetMyInsights(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "insights/mine")] HttpRequestData req)
        {
            var user = await AuthenticateRequest(req);
            if (user == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { success = false, error = "Authentication required" });
                return unauthorized;
            }

            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var limit = int.TryParse(query["limit"], out var l) ? l : 50;

            var insights = await _insightService.GetUserInsights(user.Id, limit);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                count = insights.Count,
                insights
            });
            return response;
        }
    }
}