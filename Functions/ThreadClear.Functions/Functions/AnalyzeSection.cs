using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using ThreadClear.Functions.Services.Interfaces;
using ThreadClear.Functions.Helpers;

namespace ThreadClear.Functions.Functions
{
    public class AnalyzeSection
    {
        private readonly ILogger _logger;
        private readonly IAIService _aiService;

        public AnalyzeSection(ILoggerFactory loggerFactory, IAIService aiService)
        {
            _logger = loggerFactory.CreateLogger<AnalyzeSection>();
            _aiService = aiService;
        }

        [Function("AnalyzeSection")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "analyze/section")]
            HttpRequestData req)
        {
            // Handle CORS preflight
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                return req.CreateResponse(HttpStatusCode.OK);
            }

            var request = await req.ReadFromJsonAsync<SectionAnalysisRequest>();
            if (request == null || string.IsNullOrWhiteSpace(request.ConversationText) || string.IsNullOrWhiteSpace(request.Section))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteAsJsonAsync(new { success = false, error = "Invalid request - conversationText and section are required" });
                return errorResponse;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("AnalyzeSection started: {Section}", request.Section);

            try
            {
                object result = request.Section.ToLower() switch
                {
                    "summary" => await AnalyzeSummary(request.ConversationText),
                    "questions" => await AnalyzeQuestions(request.ConversationText),
                    "tensions" => await AnalyzeTensions(request.ConversationText),
                    "health" => await AnalyzeHealth(request.ConversationText),
                    "actions" => await AnalyzeActions(request.ConversationText),
                    "misalignments" => await AnalyzeMisalignments(request.ConversationText),
                    _ => throw new ArgumentException($"Unknown section: {request.Section}")
                };

                _logger.LogInformation("AnalyzeSection completed: {Section} in {Ms}ms", request.Section, sw.ElapsedMilliseconds);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    section = request.Section,
                    data = result,
                    timeMs = sw.ElapsedMilliseconds
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AnalyzeSection error for section {Section}", request.Section);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new
                {
                    success = false,
                    section = request.Section,
                    error = ex.Message
                });
                return errorResponse;
            }
        }

        private async Task<object> AnalyzeSummary(string text)
        {
            var prompt = $@"Summarize this conversation in 2-3 concise sentences. Focus on the main topic, key decisions, and current status.

CONVERSATION:
{TruncateText(text, 6000)}

Return ONLY valid JSON:
{{""Summary"": ""Your 2-3 sentence summary here""}}";

            var response = await _aiService.GenerateStructuredResponseAsync(prompt);
            return ParseJsonObject(response, new { Summary = "Unable to generate summary" });
        }

        private async Task<object> AnalyzeQuestions(string text)
        {
            var prompt = $@"Find all unanswered questions in this conversation. Only include direct questions (ending with ?) that received no clear response in subsequent messages.

CONVERSATION:
{TruncateText(text, 6000)}

Return ONLY valid JSON:
{{""UnansweredQuestions"": [
    {{""Question"": ""The exact question text?"", ""AskedBy"": ""Person's name"", ""TimesAsked"": 1, ""DaysUnanswered"": 0}}
]}}

If no unanswered questions, return: {{""UnansweredQuestions"": []}}";

            var response = await _aiService.GenerateStructuredResponseAsync(prompt);
            return ParseJsonObject(response, new { UnansweredQuestions = new List<object>() });
        }

        private async Task<object> AnalyzeTensions(string text)
        {
            var prompt = $@"Identify tension points in this conversation - frustration, urgency, conflict, repeated unanswered requests, or disagreements.

CONVERSATION:
{TruncateText(text, 6000)}

Return ONLY valid JSON:
{{""TensionPoints"": [
    {{
        ""Type"": ""Frustration|Urgency|Conflict|Repeated Request|Disagreement"",
        ""Description"": ""Brief description of the tension"",
        ""Severity"": ""Low|Medium|High"",
        ""Participants"": [""Name1"", ""Name2""],
        ""Reasoning"": ""Why this is considered a tension point"",
        ""Evidence"": [""Quote or example from conversation""]
    }}
]}}

If no tensions found, return: {{""TensionPoints"": []}}";

            var response = await _aiService.GenerateStructuredResponseAsync(prompt);
            return ParseJsonObject(response, new { TensionPoints = new List<object>() });
        }

        private async Task<object> AnalyzeHealth(string text)
        {
            var prompt = $@"Assess the overall health of this conversation. Consider responsiveness, clarity, alignment, and professional tone.

CONVERSATION:
{TruncateText(text, 6000)}

Return ONLY valid JSON:
{{""ConversationHealth"": {{
    ""OverallScore"": 75,
    ""RiskLevel"": ""Low|Medium|High"",
    ""ResponsivenessScore"": 80,
    ""ClarityScore"": 70,
    ""AlignmentScore"": 75,
    ""Reasoning"": ""Brief explanation of the scores"",
    ""Evidence"": [""Supporting observation 1"", ""Supporting observation 2""]
}}}}

Scores should be 0-100. RiskLevel: Low (70+), Medium (40-69), High (below 40).";

            var response = await _aiService.GenerateStructuredResponseAsync(prompt);
            return ParseJsonObject(response, new
            {
                ConversationHealth = new
                {
                    OverallScore = 50,
                    RiskLevel = "Unknown",
                    ResponsivenessScore = 50,
                    ClarityScore = 50,
                    AlignmentScore = 50
                }
            });
        }

        private async Task<object> AnalyzeActions(string text)
        {
            var prompt = $@"Suggest 3-5 specific, actionable next steps based on this conversation. Focus on addressing unanswered questions, resolving tensions, and moving the conversation forward.

CONVERSATION:
{TruncateText(text, 6000)}

Return ONLY valid JSON:
{{""SuggestedActions"": [
    {{
        ""Action"": ""Specific actionable next step"",
        ""Priority"": ""High|Medium|Low"",
        ""Reasoning"": ""Why this action is recommended"",
        ""Evidence"": [""Quote or example supporting this recommendation""]
    }}
]}}";

            var response = await _aiService.GenerateStructuredResponseAsync(prompt);
            return ParseJsonObject(response, new { SuggestedActions = new List<object>() });
        }

        private async Task<object> AnalyzeMisalignments(string text)
        {
            var prompt = $@"Identify any misalignments in this conversation - different expectations, conflicting goals, miscommunication, or assumptions that don't match between participants.

CONVERSATION:
{TruncateText(text, 6000)}

Return ONLY valid JSON:
{{""Misalignments"": [
    {{
        ""Type"": ""Expectation|Goal|Communication|Assumption"",
        ""Description"": ""What the misalignment is about"",
        ""Severity"": ""Low|Medium|High"",
        ""Participants"": [""Name1"", ""Name2""],
        ""SuggestedResolution"": ""How to resolve this misalignment"",
        ""Reasoning"": ""Why this is considered a misalignment"",
        ""Evidence"": [""Quote showing the misalignment""]
    }}
]}}

If no misalignments found, return: {{""Misalignments"": []}}";

            var response = await _aiService.GenerateStructuredResponseAsync(prompt);
            return ParseJsonObject(response, new { Misalignments = new List<object>() });
        }

        private string TruncateText(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
                return text;
            return text.Substring(0, maxChars) + "\n...[truncated]";
        }

        private object ParseJsonObject(string response, object defaultValue)
        {
            try
            {
                var clean = CleanJsonResponse(response);
                using var doc = JsonDocument.Parse(clean);
                return JsonSerializer.Deserialize<JsonElement>(clean);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON response: {Response}", response?.Substring(0, Math.Min(200, response?.Length ?? 0)));
                return defaultValue;
            }
        }

        private string CleanJsonResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return "{}";

            var cleaned = response.Trim();

            // Remove markdown code blocks
            if (cleaned.StartsWith("```json"))
                cleaned = cleaned.Substring(7);
            else if (cleaned.StartsWith("```"))
                cleaned = cleaned.Substring(3);

            if (cleaned.EndsWith("```"))
                cleaned = cleaned.Substring(0, cleaned.Length - 3);

            cleaned = cleaned.Trim();

            // Find JSON boundaries
            var startIndex = cleaned.IndexOf('{');
            var endIndex = cleaned.LastIndexOf('}');

            if (startIndex >= 0 && endIndex > startIndex)
            {
                cleaned = cleaned.Substring(startIndex, endIndex - startIndex + 1);
            }

            return cleaned;
        }
    }

    public class SectionAnalysisRequest
    {
        public string ConversationText { get; set; } = "";
        public string Section { get; set; } = "";
    }
}