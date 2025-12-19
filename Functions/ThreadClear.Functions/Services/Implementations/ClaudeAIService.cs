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
    public class ClaudeAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string ClaudeApiUrl = "https://api.anthropic.com/v1/messages";
        private const string Model = "claude-sonnet-4-20250514"; // Latest Sonnet model

        public ClaudeAIService(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        }

        /// <summary>
        /// Generates a response from the AI model based on the prompt
        /// </summary>
        public async Task<string> GenerateResponseAsync(string prompt)
        {
            return await AnalyzeConversation(prompt, null);
        }

        /// <summary>
        /// Generates a structured JSON response from the AI model
        /// </summary>
        public async Task<string> GenerateStructuredResponseAsync(string prompt)
        {
            var structuredPrompt = prompt + "\n\nRespond with valid JSON only.";
            return await AnalyzeConversation(structuredPrompt, null);
        }

        /// <summary>
        /// Analyzes text and returns sentiment, urgency, and other metrics
        /// </summary>
        public async Task<string> AnalyzeTextAsync(string text)
        {
            var prompt = $@"Analyze the following text and return sentiment, urgency, and key metrics as JSON:

Text: {text}

Return JSON in this format:
{{
  ""sentiment"": ""positive|negative|neutral"",
  ""urgency"": ""low|medium|high"",
  ""keyTopics"": [""topic1"", ""topic2""],
  ""emotionalTone"": ""string""
}}";
            return await AnalyzeConversation(prompt, null);
        }

        /// <summary>
        /// General-purpose conversation analysis using Claude
        /// </summary>
        public async Task<string> AnalyzeConversation(string prompt, ThreadCapsule? capsule)
        {
            var request = new
            {
                model = Model,
                max_tokens = 4096,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            try
            {
                var response = await _httpClient.PostAsync(ClaudeApiUrl, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseContent);

                // Extract the text content from Claude's response
                return claudeResponse?.Content?.FirstOrDefault()?.Text ?? "{}";
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Claude API error: {ex.Message}");
                return "{}"; // Return empty JSON on error
            }
        }

        /// <summary>
        /// Determine if a question was answered using Claude's understanding
        /// </summary>
        public async Task<bool> IsQuestionAnswered(string question, List<Message> subsequentMessages)
        {
            if (!subsequentMessages.Any())
                return false;

            var messagesText = string.Join("\n\n", subsequentMessages.Select(m =>
                $"[{m.Timestamp:yyyy-MM-dd HH:mm}] {m.Content}"));

            var prompt = $@"Question: ""{question}""

Subsequent messages:
{messagesText}

Was this question answered (directly or indirectly) in the subsequent messages?
Respond with ONLY ""true"" or ""false"".";

            var response = await AnalyzeConversation(prompt, null);

            return response.Trim().ToLower().Contains("true");
        }

        /// <summary>
        /// Generate AI-powered action suggestions
        /// </summary>
        public async Task<List<string>> GenerateSuggestedActions(ThreadCapsule capsule)
        {
            var prompt = $@"Based on this conversation analysis, suggest 3-5 actionable next steps to improve communication and resolve issues.

Consider:
- Unanswered questions: {capsule.Analysis?.UnansweredQuestions?.Count ?? 0}
- Tension points: {capsule.Analysis?.TensionPoints?.Count ?? 0}
- Misalignments: {capsule.Analysis?.Misalignments?.Count ?? 0}
- Conversation health: {capsule.Analysis?.ConversationHealth?.RiskLevel ?? "Unknown"}

Return suggestions as a JSON array:
{{
  ""suggestions"": [""suggestion 1"", ""suggestion 2"", ...]
}}

Conversation summary:
Subject: {capsule.ThreadMetadata.Subject}
Messages: {capsule.Messages.Count}
Participants: {string.Join(", ", capsule.Participants.Select(p => p.Name))}";

            var response = await AnalyzeConversation(prompt, capsule);

            try
            {
                var parsed = JsonDocument.Parse(JsonHelper.CleanJsonResponse(response));
                var suggestions = new List<string>();

                if (parsed.RootElement.TryGetProperty("suggestions", out var suggestionsArray))
                {
                    foreach (var suggestion in suggestionsArray.EnumerateArray())
                    {
                        suggestions.Add(suggestion.GetString());
                    }
                }

                return suggestions;
            }
            catch
            {
                return new List<string> { "Review conversation for communication improvements" };
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
                _logger.LogError("Claude Vision API error: {Response}", responseBody);
                throw new Exception($"Claude API error: {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var textContent = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            return textContent ?? string.Empty;
        }

        #region Response Models

        private class ClaudeResponse
        {
            public string Id { get; set; }
            public string Type { get; set; }
            public string Role { get; set; }
            public List<ContentBlock> Content { get; set; }
            public string Model { get; set; }
            public string StopReason { get; set; }
            public Usage Usage { get; set; }
        }

        private class ContentBlock
        {
            public string Type { get; set; }
            public string Text { get; set; }
        }

        private class Usage
        {
            public int InputTokens { get; set; }
            public int OutputTokens { get; set; }
        }

        #endregion
    }
}
