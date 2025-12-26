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

        public AnalyzeConversation(
            ILoggerFactory loggerFactory,
            IConversationParser parser,
            IConversationAnalyzer analyzer,
            IThreadCapsuleBuilder builder,
            IUserService userService)
        {
            _logger = loggerFactory.CreateLogger<AnalyzeConversation>();
            _parser = parser;
            _analyzer = analyzer;
            _builder = builder;
            _userService = userService;
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

                        if (authenticatedUser == null)
                        {
                            _logger.LogWarning("Invalid or expired token");
                            return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Invalid or expired token");
                        }
                        _logger.LogInformation("Authenticated user: {Email}", authenticatedUser.Email);
                    }
                }

                // If no token, check for function key (backward compatibility)
                if (authenticatedUser == null)
                {
                    // Function key is validated by Azure Functions runtime when AuthorizationLevel.Function
                    // Since we changed to Anonymous, we need to check manually or allow unauthenticated
                    // For now, we'll allow unauthenticated but with limited features
                    _logger.LogInformation("Unauthenticated request - limited features");
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