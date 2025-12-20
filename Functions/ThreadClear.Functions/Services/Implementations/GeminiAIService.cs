using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadClear.Functions.Helpers;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class GeminiAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private const string ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

        public GeminiAIService(string apiKey, string model = "gemini-pro")
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _model = model;
            _httpClient = new HttpClient();
        }

        public async Task<string> GenerateResponseAsync(string prompt)
        {
            var url = $"{ApiUrl}{_model}:generateContent?key={_apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(JsonHelper.CleanJsonResponse(responseBody));

            return doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "";
        }

        public async Task<string> GenerateStructuredResponseAsync(string prompt)
        {
            var enhancedPrompt = prompt + "\n\nIMPORTANT: Return ONLY valid JSON with no additional text.";
            return await GenerateResponseAsync(enhancedPrompt);
        }

        public async Task<string> AnalyzeTextAsync(string text)
        {
            var prompt = $@"Analyze the sentiment, urgency, and tone of this text:

""{text}""

Return a JSON object with:
- sentiment (Positive/Negative/Neutral)
- urgency (High/Medium/Low)
- politenessScore (0.0 to 1.0)

Return ONLY the JSON object.";

            return await GenerateStructuredResponseAsync(prompt);
        }

        public async Task<string> AnalyzeConversation(string prompt, ThreadCapsule? capsule)
        {
            return await GenerateStructuredResponseAsync(prompt);
        }

        public async Task<List<SuggestedActionItem>> GenerateSuggestedActions(ThreadCapsule capsule)
        {
            var prompt = $@"Based on this conversation, suggest 3-5 actionable next steps.

Participants: {string.Join(", ", capsule.Participants.Select(p => p.Name))}
Messages: {capsule.Messages.Count}

Return suggestions as a JSON array:
{{
  ""suggestions"": [
    {{
      ""action"": ""suggestion text"",
      ""priority"": ""Low|Medium|High"",
      ""reasoning"": ""why this action is recommended"",
      ""evidence"": [""quote or observation supporting this""]
    }}
  ]
}}";

            var response = await GenerateStructuredResponseAsync(prompt);

            try
            {
                using var doc = JsonDocument.Parse(JsonHelper.CleanJsonResponse(response));
                var suggestions = new List<SuggestedActionItem>();

                if (doc.RootElement.TryGetProperty("suggestions", out var suggestionsArray))
                {
                    foreach (var item in suggestionsArray.EnumerateArray())
                    {
                        var action = new SuggestedActionItem
                        {
                            Action = item.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "",
                            Priority = item.TryGetProperty("priority", out var p) ? p.GetString() : "Medium",
                            Reasoning = item.TryGetProperty("reasoning", out var r) ? r.GetString() : null,
                            Evidence = new List<string>()
                        };

                        if (item.TryGetProperty("evidence", out var evidence))
                        {
                            action.Evidence = evidence.EnumerateArray()
                                .Select(e => e.GetString() ?? "")
                                .Where(e => !string.IsNullOrEmpty(e))
                                .ToList();
                        }

                        if (!string.IsNullOrEmpty(action.Action))
                        {
                            suggestions.Add(action);
                        }
                    }
                }

                return suggestions;
            }
            catch
            {
                return new List<SuggestedActionItem>
        {
            new SuggestedActionItem
            {
                Action = "Review conversation for communication improvements",
                Priority = "Medium",
                Reasoning = "General recommendation based on conversation analysis"
            }
        };
            }
        }

        public async Task<string> ExtractTextFromImage(string base64Image, string mimeType)
        {
            return null;
        }
    }
}