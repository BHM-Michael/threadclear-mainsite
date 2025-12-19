using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadClear.Functions.Helpers;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    /// <summary>
    /// AI Service implementation using Anthropic Claude API
    /// </summary>
    public class AnthropicAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private const string ApiUrl = "https://api.anthropic.com/v1/messages";

        public AnthropicAIService(string apiKey, string model = "claude-sonnet-4-20250514")
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _model = model;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }

        public async Task<string> GenerateResponseAsync(string prompt)
        {
            var requestBody = new
            {
                model = _model,
                max_tokens = 4096,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(ApiUrl, content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(JsonHelper.CleanJsonResponse(responseBody));
            
            // Extract text from Claude's response
            var contentArray = doc.RootElement.GetProperty("content");
            foreach (var item in contentArray.EnumerateArray())
            {
                if (item.GetProperty("type").GetString() == "text")
                {
                    return item.GetProperty("text").GetString() ?? "";
                }
            }

            return "";
        }

        public async Task<string> GenerateStructuredResponseAsync(string prompt)
        {
            // For structured responses, we add explicit JSON formatting instructions
            var enhancedPrompt = prompt + "\n\nIMPORTANT: Return ONLY valid JSON with no additional text, explanations, or markdown formatting.";
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
            return await GenerateResponseAsync(prompt);
        }

        public async Task<List<string>> GenerateSuggestedActions(ThreadCapsule capsule)
        {
            var prompt = $@"Based on this conversation, suggest 3-5 actionable next steps.

Participants: {string.Join(", ", capsule.Participants.Select(p => p.Name))}
Messages: {capsule.Messages.Count}

Return suggestions as a JSON array:
{{
  ""suggestions"": [""suggestion 1"", ""suggestion 2"", ...]
}}";

            var response = await GenerateStructuredResponseAsync(prompt);

            try
            {
                using var doc = JsonDocument.Parse(JsonHelper.CleanJsonResponse(response));
                var suggestions = new List<string>();

                if (doc.RootElement.TryGetProperty("suggestions", out var suggestionsArray))
                {
                    foreach (var suggestion in suggestionsArray.EnumerateArray())
                    {
                        var text = suggestion.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            suggestions.Add(text);
                        }
                    }
                }

                return suggestions;
            }
            catch
            {
                return new List<string> { "Review conversation for communication improvements" };
            }
        }
    }
}
