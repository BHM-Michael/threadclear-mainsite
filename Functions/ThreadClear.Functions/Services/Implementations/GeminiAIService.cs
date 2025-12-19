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

        public async Task<List<string>> GenerateSuggestedActions(ThreadCapsule capsule)
        {
            var prompt = $@"Based on this conversation, suggest 3-5 action items.

Conversation has {capsule.Messages.Count} messages between {capsule.Participants.Count} participants.

Return a JSON array of strings: [""action 1"", ""action 2"", ""action 3""]

Return ONLY the JSON array.";

            var response = await GenerateStructuredResponseAsync(prompt);

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(response) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public async Task<string> ExtractTextFromImage(string base64Image, string mimeType)
        {
            return null;
        }
    }
}