using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class AnalyzeConversation
    {
        private readonly ILogger _logger;
        private readonly IConversationParser _parser;
        private readonly IConversationAnalyzer _analyzer;
        private readonly IThreadCapsuleBuilder _builder;

        public AnalyzeConversation(
            ILoggerFactory loggerFactory,
            IConversationParser parser,
            IConversationAnalyzer analyzer,
            IThreadCapsuleBuilder builder)
        {
            _logger = loggerFactory.CreateLogger<AnalyzeConversation>();
            _parser = parser;
            _analyzer = analyzer;
            _builder = builder;
        }

        [Function("AnalyzeConversation")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", "options", Route = "analyze")]
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

                // Build analysis options from request
                AnalysisOptions? options = null;
                if (request.HasPermissionFlags())
                {
                    options = new AnalysisOptions
                    {
                        EnableUnansweredQuestions = request.EnableUnansweredQuestions ?? true,
                        EnableTensionPoints = request.EnableTensionPoints ?? true,
                        EnableMisalignments = request.EnableMisalignments ?? true,
                        EnableConversationHealth = request.EnableConversationHealth ?? true,
                        EnableSuggestedActions = request.EnableSuggestedActions ?? true
                    };
                    _logger.LogInformation("Using combined analysis with options: UQ={UQ}, TP={TP}, MA={MA}, CH={CH}, SA={SA}",
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

                // Create response
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    capsule = capsule,
                    parsingMode = modeUsed
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