using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Net;
using System.Text.Json;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Implementations;
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
                corsResponse.Headers.Add("Access-Control-Allow-Origin", "http://localhost:4200");
                corsResponse.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                corsResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
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
                
                // Override based on priority if not explicitly set
                if (mode == null && !string.IsNullOrEmpty(request.PriorityLevel))
                {
                    mode = request.PriorityLevel?.ToLower() switch
                    {
                        "high" => ParsingMode.Advanced,
                        "low" => ParsingMode.Basic,
                        _ => null // Use default Auto
                    };
                }

                // Parse conversation
                var capsule = await _parser.ParseConversation(
                    request.ConversationText,
                    request.SourceType ?? "simple",
                    mode);

                // Log which mode was used
                var modeUsed = capsule.Metadata["ParsingMode"];
                _logger.LogInformation("Parsed conversation {Id} using {Mode} mode", 
                    capsule.CapsuleId, modeUsed);

                // TODO: Perform additional analysis with IConversationAnalyzer
                await _analyzer.AnalyzeConversation(capsule);

                // Enrich with additional features
                await _builder.EnrichWithLinguisticFeatures(capsule);
                await _builder.CalculateMetadata(capsule);
                var summary = await _builder.GenerateSummary(capsule);

                // Debug: Log the full capsule as JSON
                var fullJson = System.Text.Json.JsonSerializer.Serialize(capsule, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                _logger.LogInformation("Full capsule: {Json}", fullJson);

                // Create response
                var response = req.CreateResponse(HttpStatusCode.OK);

                capsule.Summary = summary;  // Add this line

                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    capsule = capsule,
                    parsingMode = modeUsed
                });

                response.Headers.Add("Access-Control-Allow-Origin", "http://localhost:4200");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

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
