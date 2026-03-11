using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class ConversationAnalysisService : IConversationAnalysisService
    {
        private readonly IAIService _aiService;
        private readonly ILogger<ConversationAnalysisService> _logger;

        public ConversationAnalysisService(IAIService aiService, ILogger<ConversationAnalysisService> logger)
        {
            _aiService = aiService;
            _logger = logger;
        }

        public async Task<object> AnalyzeSummary(string text)
        {
            var prompt = $@"Summarize this conversation in 2-3 concise sentences. Focus on the main topic, key decisions, and current status.

CONVERSATION:
{TruncateText(text, 6000)}

Return ONLY valid JSON:
{{""Summary"": ""Your 2-3 sentence summary here""}}";

            var response = await _aiService.GenerateStructuredResponseAsync(prompt);
            return ParseJsonObject(response, new { Summary = "Unable to generate summary" });
        }

        public async Task<object> AnalyzeQuestions(string text)
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

        public async Task<object> AnalyzeTensions(string text)
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

        public async Task<object> AnalyzeHealth(string text)
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

        public async Task<object> AnalyzeActions(string text)
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

        public async Task<object> AnalyzeMisalignments(string text)
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
                _logger.LogWarning(ex, "Failed to parse JSON response: {Response}",
                    response?.Substring(0, Math.Min(200, response?.Length ?? 0)));
                return defaultValue;
            }
        }

        private static string CleanJsonResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return "{}";

            var cleaned = response.Trim();
            if (cleaned.StartsWith("```json")) cleaned = cleaned.Substring(7);
            else if (cleaned.StartsWith("```")) cleaned = cleaned.Substring(3);
            if (cleaned.EndsWith("```")) cleaned = cleaned.Substring(0, cleaned.Length - 3);
            cleaned = cleaned.Trim();

            var startIndex = cleaned.IndexOf('{');
            var endIndex = cleaned.LastIndexOf('}');
            if (startIndex >= 0 && endIndex > startIndex)
                cleaned = cleaned.Substring(startIndex, endIndex - startIndex + 1);

            return cleaned;
        }
    }
}