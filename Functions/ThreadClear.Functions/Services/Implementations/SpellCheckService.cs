using Microsoft.Extensions.Logging;
using System.Text.Json;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class SpellCheckService : ISpellCheckService
    {
        private readonly ILogger<SpellCheckService> _logger;
        private readonly IAIService _aiService;

        public SpellCheckService(ILogger<SpellCheckService> logger, IAIService aiService)
        {
            _logger = logger;
            _aiService = aiService;
        }

        public async Task<SpellCheckResult> CheckTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new SpellCheckResult { OriginalText = text };
            }

            try
            {
                var prompt = BuildSpellCheckPrompt(text);
                var response = await _aiService.GenerateStructuredResponseAsync(prompt);
                var issues = ParseSpellCheckResponse(response, text);

                return new SpellCheckResult
                {
                    OriginalText = text,
                    Issues = issues
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking spelling/grammar");
                return new SpellCheckResult { OriginalText = text };
            }
        }

        public async Task<List<MessageSpellCheckResult>> CheckMessagesAsync(List<MessageToCheck> messages)
        {
            var results = new List<MessageSpellCheckResult>();

            if (messages == null || messages.Count == 0)
            {
                return results;
            }

            try
            {
                // Batch all messages into one AI call for efficiency
                var prompt = BuildBatchSpellCheckPrompt(messages);
                var response = await _aiService.GenerateStructuredResponseAsync(prompt);
                results = ParseBatchSpellCheckResponse(response, messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch checking spelling/grammar");
                // Return empty results for each message
                foreach (var msg in messages)
                {
                    results.Add(new MessageSpellCheckResult
                    {
                        MessageId = msg.MessageId,
                        Issues = new List<SpellCheckIssue>()
                    });
                }
            }

            return results;
        }

        private string BuildSpellCheckPrompt(string text)
        {
            return $@"Analyze the following text for spelling errors, grammar issues, and typos. 
Return a JSON array of issues found. For each issue include:
- ""word"": the exact word or phrase with the issue
- ""startIndex"": character position where the issue starts (0-based)
- ""type"": ""spelling"", ""grammar"", or ""typo""
- ""message"": brief explanation of the issue
- ""suggestions"": array of 1-3 suggested corrections

Only flag clear errors. Do not flag:
- Proper nouns or names (even if unusual)
- Technical jargon or industry terms
- Informal but intentional language
- Abbreviations

If no issues found, return an empty array: []

Text to analyze:
{text}

Return ONLY valid JSON array, no other text.";
        }

        private string BuildBatchSpellCheckPrompt(List<MessageToCheck> messages)
        {
            var messagesJson = JsonSerializer.Serialize(messages.Select(m => new
            {
                id = m.MessageId,
                text = m.Text
            }));

            return $@"Analyze each message for spelling errors, grammar issues, and typos.
Return a JSON array with results for each message.

For each message, return:
{{
  ""messageId"": ""the message id"",
  ""issues"": [
    {{
      ""word"": ""the exact word with issue"",
      ""startIndex"": 0,
      ""type"": ""spelling|grammar|typo"",
      ""message"": ""brief explanation"",
      ""suggestions"": [""suggestion1"", ""suggestion2""]
    }}
  ]
}}

Only flag clear errors. Do not flag:
- Proper nouns or names (even if unusual)
- Technical jargon or industry terms  
- Informal but intentional language
- Abbreviations

Messages to analyze:
{messagesJson}

Return ONLY valid JSON array, no other text.";
        }

        private List<SpellCheckIssue> ParseSpellCheckResponse(string response, string originalText)
        {
            var issues = new List<SpellCheckIssue>();

            try
            {
                // Clean up response - remove markdown code blocks if present
                var cleanJson = CleanJsonResponse(response);

                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        var issue = ParseIssueFromJson(item, originalText);
                        if (issue != null)
                        {
                            issues.Add(issue);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse spell check response");
            }

            return issues;
        }

        private List<MessageSpellCheckResult> ParseBatchSpellCheckResponse(string response, List<MessageToCheck> messages)
        {
            var results = new List<MessageSpellCheckResult>();

            try
            {
                var cleanJson = CleanJsonResponse(response);
                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        var messageId = item.TryGetProperty("messageId", out var idProp)
                            ? idProp.GetString() ?? ""
                            : "";

                        var originalMessage = messages.FirstOrDefault(m => m.MessageId == messageId);
                        var originalText = originalMessage?.Text ?? "";

                        var result = new MessageSpellCheckResult
                        {
                            MessageId = messageId,
                            Issues = new List<SpellCheckIssue>()
                        };

                        if (item.TryGetProperty("issues", out var issuesArray) && issuesArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var issueJson in issuesArray.EnumerateArray())
                            {
                                var issue = ParseIssueFromJson(issueJson, originalText);
                                if (issue != null)
                                {
                                    result.Issues.Add(issue);
                                }
                            }
                        }

                        results.Add(result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse batch spell check response");

                // Return empty results for each message
                foreach (var msg in messages)
                {
                    results.Add(new MessageSpellCheckResult
                    {
                        MessageId = msg.MessageId,
                        Issues = new List<SpellCheckIssue>()
                    });
                }
            }

            return results;
        }

        private SpellCheckIssue? ParseIssueFromJson(JsonElement item, string originalText)
        {
            try
            {
                var word = item.TryGetProperty("word", out var wordProp) ? wordProp.GetString() ?? "" : "";

                if (string.IsNullOrEmpty(word)) return null;

                // Get or calculate start index
                int startIndex = 0;
                if (item.TryGetProperty("startIndex", out var startProp))
                {
                    startIndex = startProp.GetInt32();
                }
                else
                {
                    // Find the word in the original text
                    startIndex = originalText.IndexOf(word, StringComparison.OrdinalIgnoreCase);
                }

                var issue = new SpellCheckIssue
                {
                    Word = word,
                    StartIndex = startIndex,
                    EndIndex = startIndex + word.Length,
                    Type = item.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "spelling" : "spelling",
                    Message = item.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "",
                    Suggestions = new List<string>()
                };

                if (item.TryGetProperty("suggestions", out var suggArray) && suggArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sugg in suggArray.EnumerateArray())
                    {
                        var suggText = sugg.GetString();
                        if (!string.IsNullOrEmpty(suggText))
                        {
                            issue.Suggestions.Add(suggText);
                        }
                    }
                }

                return issue;
            }
            catch
            {
                return null;
            }
        }

        private string CleanJsonResponse(string response)
        {
            var cleaned = response.Trim();

            // Remove markdown code blocks
            if (cleaned.StartsWith("```json"))
            {
                cleaned = cleaned.Substring(7);
            }
            else if (cleaned.StartsWith("```"))
            {
                cleaned = cleaned.Substring(3);
            }

            if (cleaned.EndsWith("```"))
            {
                cleaned = cleaned.Substring(0, cleaned.Length - 3);
            }

            return cleaned.Trim();
        }
    }
}