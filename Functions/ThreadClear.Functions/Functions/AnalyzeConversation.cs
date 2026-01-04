using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;
using ThreadClear.Functions.Services.Implementations;

namespace ThreadClear.Functions.Functions
{
    public class AnalyzeConversation
    {
        private readonly ILogger _logger;
        private readonly IConversationParser _parser;
        private readonly IConversationAnalyzer _analyzer;
        private readonly IThreadCapsuleBuilder _builder;
        private readonly IUserService _userService;
        private readonly IOrganizationService _organizationService;
        private readonly IInsightService _insightService;

        public AnalyzeConversation(
            ILoggerFactory loggerFactory,
            IConversationParser parser,
            IConversationAnalyzer analyzer,
            IThreadCapsuleBuilder builder,
            IUserService userService,
            IOrganizationService organizationService,
            IInsightService insightService)
        {
            _logger = loggerFactory.CreateLogger<AnalyzeConversation>();
            _parser = parser;
            _analyzer = analyzer;
            _builder = builder;
            _userService = userService;
            _organizationService = organizationService;
            _insightService = insightService;
        }

        [Function("AnalyzeConversation")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "analyze")]
            HttpRequestData req)
        {
            // Handle CORS preflight
            if (req.Method == "OPTIONS")
            {
                var corsResponse = req.CreateResponse(HttpStatusCode.OK);
                return corsResponse;
            }

            _logger.LogInformation("Processing conversation analysis request");

            try
            {
                // Check for token authentication
                User? authenticatedUser = null;
                if (req.Headers.TryGetValues("Authorization", out var authHeaders))
                {
                    var authHeader = authHeaders.FirstOrDefault();
                    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                    {
                        var token = authHeader.Substring("Bearer ".Length);
                        authenticatedUser = await _userService.ValidateToken(token);

                        if (authenticatedUser != null)
                        {
                            _logger.LogInformation("Authenticated user via token: {Email}", authenticatedUser.Email);
                        }
                    }
                }

                // If no Bearer token, check for email/password headers (backward compatibility)
                if (authenticatedUser == null)
                {
                    var email = req.Headers.TryGetValues("X-User-Email", out var emailValues) ? emailValues.FirstOrDefault() : null;
                    var password = req.Headers.TryGetValues("X-User-Password", out var passValues) ? passValues.FirstOrDefault() : null;

                    if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
                    {
                        authenticatedUser = await _userService.ValidateLogin(email, password);
                        if (authenticatedUser != null)
                        {
                            _logger.LogInformation("Authenticated user via headers: {Email}", authenticatedUser.Email);
                        }
                    }
                }

                // If still no auth, allow unauthenticated but with limited features
                if (authenticatedUser == null)
                {
                    _logger.LogInformation("Unauthenticated request - limited features, no insight storage");
                }

                // Parse request
                var request = await req.ReadFromJsonAsync<AnalysisRequest>();

                if (request == null || string.IsNullOrWhiteSpace(request.ConversationText))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request");
                }

                // Determine parsing mode
                ParsingMode? mode = request.ParsingMode;

                if (mode == null && !string.IsNullOrEmpty(request.PriorityLevel))
                {
                    mode = request.PriorityLevel?.ToLower() switch
                    {
                        "high" => ParsingMode.Advanced,
                        "low" => ParsingMode.Basic,
                        _ => null
                    };
                }

                // Parse conversation
                var capsule = await _parser.ParseConversation(
                    request.ConversationText,
                    request.SourceType ?? "simple",
                    mode);

                var modeUsed = capsule.Metadata["ParsingMode"];
                _logger.LogInformation("Parsed conversation {Id} using {Mode} mode",
                    capsule.CapsuleId, modeUsed);

                // Build analysis options from request or user permissions
                AnalysisOptions? options = null;
                if (authenticatedUser != null && authenticatedUser.Permissions != null)
                {
                    // Use user's permissions
                    options = new AnalysisOptions
                    {
                        EnableUnansweredQuestions = authenticatedUser.Permissions.UnansweredQuestions,
                        EnableTensionPoints = authenticatedUser.Permissions.TensionPoints,
                        EnableMisalignments = authenticatedUser.Permissions.Misalignments,
                        EnableConversationHealth = authenticatedUser.Permissions.ConversationHealth,
                        EnableSuggestedActions = authenticatedUser.Permissions.SuggestedActions
                    };
                    _logger.LogInformation("Using user permissions for analysis");
                }
                else if (request.HasPermissionFlags())
                {
                    options = new AnalysisOptions
                    {
                        EnableUnansweredQuestions = request.EnableUnansweredQuestions ?? true,
                        EnableTensionPoints = request.EnableTensionPoints ?? true,
                        EnableMisalignments = request.EnableMisalignments ?? true,
                        EnableConversationHealth = request.EnableConversationHealth ?? true,
                        EnableSuggestedActions = request.EnableSuggestedActions ?? true
                    };
                }

                if (options != null)
                {
                    _logger.LogInformation("Analysis options: UQ={UQ}, TP={TP}, MA={MA}, CH={CH}, SA={SA}",
                        options.EnableUnansweredQuestions, options.EnableTensionPoints,
                        options.EnableMisalignments, options.EnableConversationHealth,
                        options.EnableSuggestedActions);
                }

                // Perform analysis (combined if options provided, full if not)
                await _analyzer.AnalyzeConversation(capsule, options);

                // Enrich with additional features
                await _builder.EnrichWithLinguisticFeatures(capsule);
                await _builder.CalculateMetadata(capsule);
                var summary = await _builder.GenerateSummary(capsule);
                capsule.Summary = summary;

                // Analyze draft if provided
                DraftAnalysis? draftAnalysis = null;
                if (!string.IsNullOrWhiteSpace(request.DraftMessage))
                {
                    _logger.LogInformation("Analyzing draft message");
                    draftAnalysis = await _analyzer.AnalyzeDraft(capsule, request.DraftMessage);
                }

                // ============================================
                // STORE INSIGHT (if authenticated user with org)
                // ============================================
                await StoreInsightAsync(authenticatedUser, capsule, request.SourceType ?? "simple");

                // Create response
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    capsule = capsule,
                    parsingMode = modeUsed,
                    draftAnalysis = draftAnalysis,
                    user = authenticatedUser?.Email
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing conversation");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError,
                    "An error occurred processing your request");
            }
        }

        /// <summary>
        /// Stores privacy-safe insight from analysis results
        /// </summary>
        private async Task StoreInsightAsync(User? user, ThreadCapsule capsule, string sourceType)
        {
            if (user == null)
            {
                _logger.LogDebug("Skipping insight storage - no authenticated user");
                return;
            }

            try
            {
                // Get user's default organization
                var userOrgs = await _organizationService.GetUserOrganizations(user.Id);
                if (userOrgs == null || userOrgs.Count == 0)
                {
                    _logger.LogDebug("Skipping insight storage - user has no organization");
                    return;
                }

                var orgId = userOrgs[0].Id; // Use default (first) organization

                // Store the insight (InsightService handles the transformation)
                await _insightService.StoreInsight(orgId, user.Id, capsule);

                _logger.LogInformation("Stored insight for user {UserId} in org {OrgId}", user.Id, orgId);
            }
            catch (Exception ex)
            {
                // Log but don't fail the request - insight storage is secondary
                _logger.LogError(ex, "Failed to store insight for user {UserId}", user?.Id);
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
}
