using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class StoreInsight
    {
        private readonly ILogger _logger;
        private readonly IUserService _userService;
        private readonly IOrganizationService _organizationService;
        private readonly IInsightService _insightService;

        public StoreInsight(
            ILoggerFactory loggerFactory,
            IUserService userService,
            IOrganizationService organizationService,
            IInsightService insightService)
        {
            _logger = loggerFactory.CreateLogger<StoreInsight>();
            _userService = userService;
            _organizationService = organizationService;
            _insightService = insightService;
        }

        [Function("StoreInsight")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "insights/store")]
            HttpRequestData req)
        {
            if (req.Method == "OPTIONS")
            {
                return req.CreateResponse(HttpStatusCode.OK);
            }

            _logger.LogInformation("Processing insight storage request");

            try
            {
                var user = await AuthenticateUser(req);
                if (user == null)
                {
                    _logger.LogDebug("Skipping insight storage - no authenticated user");
                    var noAuthResponse = req.CreateResponse(HttpStatusCode.OK);
                    await noAuthResponse.WriteAsJsonAsync(new { success = true, stored = false, reason = "not_authenticated" });
                    return noAuthResponse;
                }

                var requestJson = await req.ReadFromJsonAsync<JsonElement>();

                if (!requestJson.TryGetProperty("capsule", out var capsuleJson))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request - capsule required");
                }

                // Build a minimal capsule with just what InsightService needs
                var capsule = new ThreadCapsule
                {
                    CapsuleId = capsuleJson.TryGetProperty("CapsuleId", out var id) ? id.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString(),
                    SourceType = capsuleJson.TryGetProperty("SourceType", out var st) ? st.GetString() ?? "unknown" : "unknown",
                    Participants = ExtractParticipants(capsuleJson),
                    Messages = new List<Message>(),
                    Analysis = ExtractAnalysis(capsuleJson),
                    Summary = capsuleJson.TryGetProperty("Summary", out var sum) ? sum.GetString() : null
                };

                var userOrgs = await _organizationService.GetUserOrganizations(user.Id);
                if (userOrgs == null || userOrgs.Count == 0)
                {
                    _logger.LogDebug("Skipping insight storage - user has no organization");
                    var noOrgResponse = req.CreateResponse(HttpStatusCode.OK);
                    await noOrgResponse.WriteAsJsonAsync(new { success = true, stored = false, reason = "no_organization" });
                    return noOrgResponse;
                }

                var orgId = userOrgs[0].Id;
                await _insightService.StoreInsight(orgId, user.Id, capsule);

                _logger.LogInformation("Stored insight for user {UserId} in org {OrgId}", user.Id, orgId);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, stored = true });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing insight");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to store insight");
            }
        }

        private List<Participant> ExtractParticipants(JsonElement capsuleJson)
        {
            var participants = new List<Participant>();

            if (capsuleJson.TryGetProperty("Participants", out var participantsJson))
            {
                foreach (var p in participantsJson.EnumerateArray())
                {
                    participants.Add(new Participant
                    {
                        Id = p.TryGetProperty("Id", out var pid) ? pid.GetString() ?? "" : "",
                        Name = p.TryGetProperty("Name", out var name) ? name.GetString() ?? "" : "",
                        Email = p.TryGetProperty("Email", out var email) ? email.GetString() : null
                    });
                }
            }

            return participants;
        }

        private ConversationAnalysis ExtractAnalysis(JsonElement capsuleJson)
        {
            var analysis = new ConversationAnalysis
            {
                UnansweredQuestions = new List<UnansweredQuestion>(),
                TensionPoints = new List<TensionPoint>(),
                Misalignments = new List<Misalignment>(),
                Decisions = new List<DecisionPoint>(),
                ActionItems = new List<ActionItem>()
            };

            if (capsuleJson.TryGetProperty("Analysis", out var analysisJson))
            {
                // Extract unanswered questions
                if (analysisJson.TryGetProperty("UnansweredQuestions", out var uq) && uq.ValueKind == JsonValueKind.Array)
                {
                    foreach (var q in uq.EnumerateArray())
                    {
                        analysis.UnansweredQuestions.Add(new UnansweredQuestion
                        {
                            Question = q.TryGetProperty("Question", out var qText) ? qText.GetString() : null,
                            AskedBy = q.TryGetProperty("AskedBy", out var ab) ? ab.GetString() : null,
                            AskedAt = q.TryGetProperty("AskedAt", out var aa) && aa.TryGetDateTime(out var aaVal) ? aaVal : DateTime.UtcNow,
                            TimesAsked = q.TryGetProperty("TimesAsked", out var ta) ? ta.GetInt32() : 1,
                            MessageId = q.TryGetProperty("MessageId", out var mid) ? mid.GetString() : null,
                            DaysUnanswered = q.TryGetProperty("DaysUnanswered", out var du) ? du.GetDouble() : 0
                        });
                    }
                }

                // Extract tension points
                if (analysisJson.TryGetProperty("TensionPoints", out var tp) && tp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var t in tp.EnumerateArray())
                    {
                        var tensionPoint = new TensionPoint
                        {
                            Type = t.TryGetProperty("Type", out var type) ? type.GetString() : null,
                            Severity = t.TryGetProperty("Severity", out var sev) ? sev.GetString() : null,
                            Description = t.TryGetProperty("Description", out var desc) ? desc.GetString() : null,
                            MessageId = t.TryGetProperty("MessageId", out var mid) ? mid.GetString() : null,
                            Timestamp = t.TryGetProperty("Timestamp", out var ts) && ts.TryGetDateTime(out var tsVal) ? tsVal : DateTime.UtcNow,
                            DetectedAt = t.TryGetProperty("DetectedAt", out var da) && da.TryGetDateTime(out var daVal) ? daVal : DateTime.UtcNow,
                            Reasoning = t.TryGetProperty("Reasoning", out var r) ? r.GetString() : null
                        };

                        if (t.TryGetProperty("Participants", out var parts) && parts.ValueKind == JsonValueKind.Array)
                        {
                            tensionPoint.Participants = parts.EnumerateArray()
                                .Select(p => p.GetString() ?? "")
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();
                        }

                        if (t.TryGetProperty("Evidence", out var ev) && ev.ValueKind == JsonValueKind.Array)
                        {
                            tensionPoint.Evidence = ev.EnumerateArray()
                                .Select(e => e.GetString() ?? "")
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();
                        }

                        analysis.TensionPoints.Add(tensionPoint);
                    }
                }

                // Extract misalignments
                if (analysisJson.TryGetProperty("Misalignments", out var ma) && ma.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in ma.EnumerateArray())
                    {
                        var misalignment = new Misalignment
                        {
                            Type = m.TryGetProperty("Type", out var type) ? type.GetString() : null,
                            Severity = m.TryGetProperty("Severity", out var sev) ? sev.GetString() : null,
                            Description = m.TryGetProperty("Description", out var desc) ? desc.GetString() : null,
                            SuggestedResolution = m.TryGetProperty("SuggestedResolution", out var sr) ? sr.GetString() : null,
                            Reasoning = m.TryGetProperty("Reasoning", out var r) ? r.GetString() : null
                        };

                        if (m.TryGetProperty("ParticipantsInvolved", out var parts) && parts.ValueKind == JsonValueKind.Array)
                        {
                            misalignment.ParticipantsInvolved = parts.EnumerateArray()
                                .Select(p => p.GetString() ?? "")
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();
                        }

                        if (m.TryGetProperty("Evidence", out var ev) && ev.ValueKind == JsonValueKind.Array)
                        {
                            misalignment.Evidence = ev.EnumerateArray()
                                .Select(e => e.GetString() ?? "")
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();
                        }

                        analysis.Misalignments.Add(misalignment);
                    }
                }

                // Extract conversation health
                if (analysisJson.TryGetProperty("ConversationHealth", out var ch) && ch.ValueKind == JsonValueKind.Object)
                {
                    var health = new ConversationHealth
                    {
                        RiskLevel = ch.TryGetProperty("RiskLevel", out var rl) ? rl.GetString() : null,
                        HealthScore = ch.TryGetProperty("HealthScore", out var hs) ? hs.GetDouble() : 0.5,
                        ResponsivenessScore = ch.TryGetProperty("ResponsivenessScore", out var rs) ? rs.GetDouble() : 0.5,
                        ClarityScore = ch.TryGetProperty("ClarityScore", out var cs) ? cs.GetDouble() : 0.5,
                        AlignmentScore = ch.TryGetProperty("AlignmentScore", out var als) ? als.GetDouble() : 0.5,
                        Reasoning = ch.TryGetProperty("Reasoning", out var r) ? r.GetString() : null
                    };

                    if (ch.TryGetProperty("Issues", out var issues) && issues.ValueKind == JsonValueKind.Array)
                    {
                        health.Issues = issues.EnumerateArray().Select(i => i.GetString()).ToList();
                    }

                    if (ch.TryGetProperty("Strengths", out var strengths) && strengths.ValueKind == JsonValueKind.Array)
                    {
                        health.Strengths = strengths.EnumerateArray().Select(s => s.GetString()).ToList();
                    }

                    if (ch.TryGetProperty("Recommendations", out var recs) && recs.ValueKind == JsonValueKind.Array)
                    {
                        health.Recommendations = recs.EnumerateArray().Select(rc => rc.GetString()).ToList();
                    }

                    if (ch.TryGetProperty("Evidence", out var ev) && ev.ValueKind == JsonValueKind.Array)
                    {
                        health.Evidence = ev.EnumerateArray()
                            .Select(e => e.GetString() ?? "")
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();
                    }

                    analysis.ConversationHealth = health;
                }
            }

            return analysis;
        }


        private async Task<User?> AuthenticateUser(HttpRequestData req)
        {
            if (req.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                var authHeader = authHeaders.FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring("Bearer ".Length);
                    return await _userService.ValidateToken(token);
                }
            }

            var email = req.Headers.TryGetValues("X-User-Email", out var emailValues) ? emailValues.FirstOrDefault() : null;
            var password = req.Headers.TryGetValues("X-User-Password", out var passValues) ? passValues.FirstOrDefault() : null;

            if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
            {
                return await _userService.ValidateLogin(email, password);
            }

            return null;
        }

        private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
        {
            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(new { success = false, error = message });
            return response;
        }
    }
}