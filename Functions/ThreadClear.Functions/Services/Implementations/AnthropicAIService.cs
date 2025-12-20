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
            var requestBody = new
            {
                model = "claude-sonnet-4-20250514",
                max_tokens = 4096,
                messages = new[]
                {
            new
            {
                role = "user",
                content = new object[]
                {
                    new
                    {
                        type = "image",
                        source = new
                        {
                            type = "base64",
                            media_type = mimeType,
                            data = base64Image
                        }
                    },
                    new
                    {
                        type = "text",
                        text = @"Extract all conversation messages from this screenshot. 

Format the output as a conversation with each message on a new line:
- Use the format 'Name: Message' for each message
- Preserve the order of messages as they appear
- Include timestamps if visible
- If it's an email thread, include From/To/Subject headers

Only output the extracted conversation text, nothing else."
                    }
                }
            }
        }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                //_logger.LogError("Claude Vision API error: {Response}", responseBody);
                throw new Exception($"Claude API error: {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var textContent = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            return textContent ?? string.Empty;
        }
    }
}
