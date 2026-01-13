using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class AnalyzeOutlookFunction
    {
        private readonly ILogger _logger;
        private readonly IUserService _userService;
        private readonly IConversationParser _parser;
        private readonly IConversationAnalyzer _analyzer;
        private readonly IThreadCapsuleBuilder _builder;
        private readonly IOrganizationService _organizationService;
        private readonly IInsightService _insightService;

        public AnalyzeOutlookFunction(
            ILoggerFactory loggerFactory,
            IUserService userService,
            IConversationParser parser,
            IConversationAnalyzer analyzer,
            IThreadCapsuleBuilder builder,
            IOrganizationService organizationService,
            IInsightService insightService)
        {
            _logger = loggerFactory.CreateLogger<AnalyzeOutlookFunction>();
            _userService = userService;
            _parser = parser;
            _analyzer = analyzer;
            _builder = builder;
            _organizationService = organizationService;
            _insightService = insightService;
        }

        [Function("analyze-outlook")]
        public async Task<HttpResponseData> AnalyzeThread(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "analyze-outlook")]
            HttpRequestData req)
        {
            // Handle CORS preflight
            if (req.Method == "OPTIONS")
            {
                var corsResponse = req.CreateResponse(HttpStatusCode.OK);
                return corsResponse;
            }

            _logger.LogInformation("Outlook add-in analysis request received");

            try
            {
                // Parse request body
                var request = await req.ReadFromJsonAsync<OutlookAnalyzeRequest>();

                if (request == null || string.IsNullOrEmpty(request.Body))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Missing email body");
                }

                // Find user by email addresses
                var user = await FindUserByEmails(request.Emails);
                if (user == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.Unauthorized,
                        "No registered user found. Please sign up at threadclear.com");
                }

                // Check if user is active
                if (!user.IsActive)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.Unauthorized,
                        "Your account is pending approval. Please wait for admin activation.");
                }

                _logger.LogInformation("Processing analysis for user {UserId}, conversation {ConversationId}",
                    user.Id, request.ConversationId);

                // Parse the conversation
                var capsule = await _parser.ParseConversation(
                    request.Body,
                    "outlook",
                    null);

                // Set metadata
                capsule.Metadata["OutlookConversationId"] = request.ConversationId ?? "";
                capsule.Metadata["Source"] = "outlook-addin";
                capsule.Metadata["UserEmail"] = request.UserEmail ?? "";
                capsule.Metadata["Subject"] = request.Subject ?? "";

                // Build analysis options from user permissions
                AnalysisOptions? options = null;
                if (user.Permissions != null)
                {
                    options = new AnalysisOptions
                    {
                        EnableUnansweredQuestions = user.Permissions.UnansweredQuestions,
                        EnableTensionPoints = user.Permissions.TensionPoints,
                        EnableMisalignments = user.Permissions.Misalignments,
                        EnableConversationHealth = user.Permissions.ConversationHealth,
                        EnableSuggestedActions = user.Permissions.SuggestedActions
                    };
                }

                // Run analysis
                await _analyzer.AnalyzeConversation(capsule, options);

                // Enrich with additional features
                await _builder.EnrichWithLinguisticFeatures(capsule);
                await _builder.CalculateMetadata(capsule);
                var summary = await _builder.GenerateSummary(capsule);
                capsule.Summary = summary;

                // Store insight
                await StoreInsightAsync(user, capsule);

                // Create response
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    analysisId = capsule.CapsuleId,
                    conversationId = request.ConversationId,
                    analysis = capsule.Analysis,
                    suggestedActions = capsule.SuggestedActions,
                    summary = capsule.Summary
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Outlook analysis request");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError,
                    "Analysis failed: " + ex.Message);
            }
        }

        [Function("analyze-draft")]
        public async Task<HttpResponseData> AnalyzeDraft(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "analyze-draft")]
            HttpRequestData req)
        {
            // Handle CORS preflight
            if (req.Method == "OPTIONS")
            {
                var corsResponse = req.CreateResponse(HttpStatusCode.OK);
                return corsResponse;
            }

            _logger.LogInformation("Outlook draft analysis request received");

            try
            {
                var request = await req.ReadFromJsonAsync<OutlookDraftRequest>();

                if (request == null || string.IsNullOrEmpty(request.DraftBody))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Missing draft body");
                }

                // Find user by email addresses
                var user = await FindUserByEmails(request.Emails);
                if (user == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.Unauthorized,
                        "No registered user found. Please sign up at threadclear.com");
                }

                if (!user.IsActive)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.Unauthorized,
                        "Your account is pending approval.");
                }

                // If we have original body (the email being replied to), parse it first
                ThreadCapsule? originalCapsule = null;
                if (!string.IsNullOrEmpty(request.OriginalBody))
                {
                    originalCapsule = await _parser.ParseConversation(
                        request.OriginalBody,
                        "outlook",
                        null);

                    // Run analysis to get unanswered questions
                    await _analyzer.AnalyzeConversation(originalCapsule, null);
                }

                // Analyze the draft
                var draftAnalysis = await _analyzer.AnalyzeDraft(
                    originalCapsule ?? new ThreadCapsule(),
                    request.DraftBody);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    conversationId = request.ConversationId,
                    draftAnalysis = draftAnalysis
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing draft analysis request");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError,
                    "Analysis failed: " + ex.Message);
            }
        }

        private async Task<User?> FindUserByEmails(List<string>? emails)
        {
            if (emails == null || !emails.Any())
                return null;

            foreach (var email in emails)
            {
                var user = await _userService.GetUserByEmail(email);
                if (user != null)
                    return user;
            }

            return null;
        }

        private async Task StoreInsightAsync(User user, ThreadCapsule capsule)
        {
            try
            {
                var userOrgs = await _organizationService.GetUserOrganizations(user.Id);
                if (userOrgs == null || userOrgs.Count == 0)
                {
                    _logger.LogDebug("Skipping insight storage - user has no organization");
                    return;
                }

                var orgId = userOrgs[0].Id;
                await _insightService.StoreInsight(orgId, user.Id, capsule);
                _logger.LogInformation("Stored insight for user {UserId} in org {OrgId}", user.Id, orgId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store insight for user {UserId}", user.Id);
            }
        }

        private async Task<HttpResponseData> CreateErrorResponse(
            HttpRequestData req,
            HttpStatusCode statusCode,
            string message)
        {
            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(new
            {
                success = false,
                error = message
            });
            return response;
        }
    }

    // Request models
    public class OutlookAnalyzeRequest
    {
        public string? ConversationId { get; set; }
        public string? Subject { get; set; }
        public string? Body { get; set; }
        public List<string>? Emails { get; set; }
        public string? UserEmail { get; set; }
        public string? Timestamp { get; set; }
    }

    public class OutlookDraftRequest
    {
        public string? ConversationId { get; set; }
        public string? Subject { get; set; }
        public string? OriginalBody { get; set; }
        public string? DraftBody { get; set; }
        public List<string>? Emails { get; set; }
        public string? UserEmail { get; set; }
        public string? Timestamp { get; set; }
    }
}