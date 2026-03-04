using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class GmailAddonFunction
    {
        private readonly ILogger<GmailAddonFunction> _logger;
        private readonly GmailService _gmailService;
        private readonly IConversationParser _parser;
        private readonly IConversationAnalyzer _analyzer;
        private readonly IThreadCapsuleBuilder _builder;

        public GmailAddonFunction(
            ILogger<GmailAddonFunction> logger,
            GmailService gmailService,
            IConversationParser parser,
            IConversationAnalyzer analyzer,
            IThreadCapsuleBuilder builder)
        {
            _logger = logger;
            _gmailService = gmailService;
            _parser = parser;
            _analyzer = analyzer;
            _builder = builder;
        }

        [Function("GmailAddonTrigger")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options",
                Route = "gmail-addon/trigger")] HttpRequestData req)
        {
            if (req.Method == "OPTIONS")
            {
                var corsResponse = req.CreateResponse(HttpStatusCode.OK);
                corsResponse.Headers.Add("Access-Control-Allow-Origin", "https://script.google.com");
                corsResponse.Headers.Add("Access-Control-Allow-Methods", "POST");
                corsResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                return corsResponse;
            }

            try
            {
                var request = await req.ReadFromJsonAsync<AddonTriggerRequest>();

                if (request == null || string.IsNullOrEmpty(request.MessageId))
                    return await ErrorResponse(req, "Missing messageId");

                if (string.IsNullOrEmpty(request.AccessToken))
                    return await ErrorResponse(req, "Missing accessToken");

                _logger.LogInformation("Gmail Add-on trigger for message {MessageId}", request.MessageId);

                // Fetch the full thread using the access token passed from Apps Script
                var thread = await _gmailService.GetThreadByMessageIdAsync(
                    request.AccessToken,
                    request.MessageId);

                if (thread == null)
                    return await ErrorResponse(req, "Could not fetch thread");

                // Convert thread to conversation text (reuse existing method)
                var conversationText = _gmailService.ConvertThreadToConversation(thread);

                if (string.IsNullOrWhiteSpace(conversationText))
                    return await ErrorResponse(req, "Thread has no content");

                // Run analysis pipeline (same as everywhere else)
                var capsule = await _parser.ParseConversation(
                    conversationText, "email", null);

                var options = new AnalysisOptions
                {
                    EnableUnansweredQuestions = true,
                    EnableTensionPoints = true,
                    EnableMisalignments = true,
                    EnableConversationHealth = true,
                    EnableSuggestedActions = true
                };

                await _analyzer.AnalyzeConversation(capsule, options, null);
                await _builder.CalculateMetadata(capsule);

                // Build response for Apps Script card
                var health = capsule.Analysis?.ConversationHealth;
                var response = req.CreateResponse(HttpStatusCode.OK);

                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    messageId = request.MessageId,
                    healthScore = (int)(health?.HealthScore ?? 0),
                    riskLevel = health?.RiskLevel ?? "Unknown",
                    unansweredQuestions = capsule.Analysis?.UnansweredQuestions?
                        .Select(q => q.Question)
                        .Take(5)
                        .ToList() ?? new List<string>(),
                    commitments = capsule.Analysis?.ActionItems?
                        .Select(a => a.Action)
                        .Take(5)
                        .ToList() ?? new List<string>(),
                    tensionPoints = capsule.Analysis?.TensionPoints?
                        .Select(t => t.Description)
                        .Take(3)
                        .ToList() ?? new List<string>()
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gmail Add-on trigger failed");
                return await ErrorResponse(req, "Analysis failed");
            }
        }

        private async Task<HttpResponseData> ErrorResponse(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteAsJsonAsync(new { success = false, error = message });
            return response;
        }
    }

    public class AddonTriggerRequest
    {
        public string? MessageId { get; set; }
        public string? AccessToken { get; set; }
    }
}