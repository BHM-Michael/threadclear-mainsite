using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ThreadClear.Functions.Helpers;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    /// <summary>
    /// Hybrid conversation parser supporting both regex (fast/free) and AI (smart/paid) parsing
    /// </summary>
    public class ConversationParser : IConversationParser
    {
        private readonly IAIService? _aiService;
        private readonly ParsingMode _defaultMode;

        // Regex patterns for Basic mode
        private static readonly Regex EmailHeaderPattern = new Regex(
            @"^(From|To|Cc|Subject|Date):\s*(.+)$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ParticipantPattern = new Regex(
            @"^([A-Za-z][A-Za-z0-9\s]*?):\s*(.+)$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex EmailAddressPattern = new Regex(
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            RegexOptions.Compiled);

        private static readonly Regex EmailSeparatorPattern = new Regex(
            @"^From:.*$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex SlackMessagePattern = new Regex(
            @"^(\w+)\s+\[(\d{1,2}:\d{2}\s*(?:AM|PM)?)\]:\s*(.+)$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex SimpleMessagePattern = new Regex(
            @"^(\w+):\s*(.+)$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Constructor for Basic mode (no AI service needed)
        /// </summary>
        public ConversationParser() : this(null, ParsingMode.Basic)
        {
        }

        /// <summary>
        /// Constructor with AI service for Advanced/Auto modes
        /// </summary>
        public ConversationParser(IAIService? aiService, ParsingMode defaultMode = ParsingMode.Auto)
        {
            _aiService = aiService;
            _defaultMode = defaultMode;

            if (defaultMode != ParsingMode.Basic && aiService == null)
            {
                throw new ArgumentException(
                    "AI service is required for Advanced or Auto parsing modes", 
                    nameof(aiService));
            }
        }

        public async Task<ThreadCapsule> ParseConversation(
            string conversationText, 
            string sourceType, 
            ParsingMode? mode = null)
        {
            if (string.IsNullOrWhiteSpace(conversationText))
            {
                throw new ArgumentException("Conversation text cannot be empty", nameof(conversationText));
            }

            var effectiveMode = DetermineParsingMode(mode ?? _defaultMode, conversationText, sourceType);

            var capsule = new ThreadCapsule
            {
                CapsuleId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                SourceType = sourceType,
                RawText = conversationText,
            };
            
            capsule.Metadata["ParsingMode"] = effectiveMode.ToString();

            // Parse based on mode
            if (effectiveMode == ParsingMode.Advanced)
            {
                await ParseWithAI(capsule, conversationText, sourceType);
            }
            else
            {
                await ParseWithRegex(capsule, conversationText, sourceType);
            }

            // Link messages to participants
            LinkMessagesToParticipants(capsule);

            return capsule;
        }

        public async Task<List<Participant>> ExtractParticipants(
            string conversationText, 
            string sourceType,
            ParsingMode? mode = null)
        {
            var effectiveMode = DetermineParsingMode(mode ?? _defaultMode, conversationText, sourceType);

            if (effectiveMode == ParsingMode.Advanced && _aiService != null)
            {
                return await ExtractParticipantsWithAI(conversationText, sourceType);
            }
            else
            {
                return await ExtractParticipantsWithRegex(conversationText, sourceType);
            }
        }

        public async Task<List<Message>> ExtractMessages(
            string conversationText, 
            string sourceType,
            ParsingMode? mode = null)
        {
            var effectiveMode = DetermineParsingMode(mode ?? _defaultMode, conversationText, sourceType);

            if (effectiveMode == ParsingMode.Advanced && _aiService != null)
            {
                return await ExtractMessagesWithAI(conversationText, sourceType);
            }
            else
            {
                return await ExtractMessagesWithRegex(conversationText, sourceType);
            }
        }

        #region Mode Selection

        private ParsingMode DetermineParsingMode(ParsingMode requestedMode, string text, string sourceType)
        {
            // If explicitly requested, honor it
            if (requestedMode != ParsingMode.Auto)
            {
                return requestedMode;
            }

            // Auto mode: decide based on complexity
            var complexityScore = CalculateComplexityScore(text, sourceType);

            // Use Advanced for complex conversations
            if (complexityScore > 0.6 && _aiService != null)
            {
                return ParsingMode.Advanced;
            }

            return ParsingMode.Basic;
        }

        private double CalculateComplexityScore(string text, string sourceType)
        {
            double score = 0.0;

            // Check for non-standard format indicators
            if (!EmailHeaderPattern.IsMatch(text) && sourceType?.ToLower() == "email")
                score += 0.3; // Email without clear headers

            if (text.Contains("...") || text.Contains("[unclear]"))
                score += 0.2; // Potentially ambiguous content

            // Check for mixed formats
            var hasEmailFormat = EmailHeaderPattern.IsMatch(text);
            var hasChatFormat = SlackMessagePattern.IsMatch(text);
            if (hasEmailFormat && hasChatFormat)
                score += 0.3; // Mixed format conversation

            // Check for non-ASCII characters (might indicate foreign language)
            if (text.Any(c => c > 127))
                score += 0.2;

            // Very short conversations are usually simple
            if (text.Length < 200)
                score -= 0.2;

            return Math.Max(0, Math.Min(1, score));
        }

        #endregion

        #region Basic (Regex) Parsing

        private async Task ParseWithRegex(ThreadCapsule capsule, string conversationText, string sourceType)
        {
            capsule.Participants = await ExtractParticipantsWithRegex(conversationText, sourceType);
            capsule.Messages = await ExtractMessagesWithRegex(conversationText, sourceType);
        }

        private async Task<List<Participant>> ExtractParticipantsWithRegex(string conversationText, string sourceType)
        {
            var participants = new HashSet<string>();

            switch (sourceType?.ToLower())
            {
                case "email":
                    participants.UnionWith(ExtractEmailParticipants(conversationText));
                    break;

                case "slack":
                case "teams":
                case "discord":
                    participants.UnionWith(ExtractChatParticipants(conversationText));
                    break;

                case "sms":
                case "simple":
                default:
                    participants.UnionWith(ExtractSimpleParticipants(conversationText));
                    break;
            }

            return await Task.FromResult(participants.Select((name, index) => new Participant
            {
                Id = $"p{index + 1}",
                Name = name,
                Email = ExtractEmailFromName(name, conversationText)
            }).ToList());
        }

        private async Task<List<Message>> ExtractMessagesWithRegex(string conversationText, string sourceType)
        {
            List<Message> messages;

            switch (sourceType?.ToLower())
            {
                case "email":
                    messages = ExtractEmailMessages(conversationText);
                    break;

                case "slack":
                case "teams":
                    messages = ExtractChatMessages(conversationText);
                    break;

                case "sms":
                case "simple":
                default:
                    messages = ExtractSimpleMessages(conversationText);
                    break;
            }

            // Use regex-based linguistic analysis for Basic mode
            foreach (var message in messages)
            {
                message.LinguisticFeatures = AnalyzeLinguisticFeaturesWithRegex(message.Content);
            }

            return await Task.FromResult(messages);
        }

        private HashSet<string> ExtractEmailParticipants(string emailText)
        {
            var participants = new HashSet<string>();
            var matches = EmailHeaderPattern.Matches(emailText);

            foreach (Match match in matches)
            {
                var headerName = match.Groups[1].Value.ToLower();
                var headerValue = match.Groups[2].Value;

                if (headerName == "from" || headerName == "to" || headerName == "cc")
                {
                    var emails = EmailAddressPattern.Matches(headerValue);
                    foreach (Match email in emails)
                    {
                        participants.Add(email.Value);
                    }
                }
            }

            return participants;
        }

        private List<Message> ExtractEmailMessages(string emailText)
        {
            var messages = new List<Message>();
            var emailParts = EmailSeparatorPattern.Split(emailText);

            foreach (var part in emailParts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;

                var message = ParseEmailMessage("From: " + part);
                if (message != null)
                {
                    messages.Add(message);
                }
            }

            if (messages.Count == 0)
            {
                var singleMessage = ParseEmailMessage(emailText);
                if (singleMessage != null)
                {
                    messages.Add(singleMessage);
                }
            }

            return messages;
        }

        private Message? ParseEmailMessage(string emailText)
        {
            var message = new Message
            {
                Id = $"msg{Guid.NewGuid().ToString().Substring(0, 8)}",
                Timestamp = DateTime.UtcNow
            };

            var headers = EmailHeaderPattern.Matches(emailText);
            var bodyStart = 0;

            foreach (Match match in headers)
            {
                var headerName = match.Groups[1].Value.ToLower();
                var headerValue = match.Groups[2].Value;
                bodyStart = Math.Max(bodyStart, match.Index + match.Length);

                switch (headerName)
                {
                    case "from":
                        message.ParticipantId = ExtractEmail(headerValue);
                        break;
                    case "date":
                        message.Timestamp = ParseEmailDate(headerValue);
                        break;
                    case "subject":
                        message.Metadata["Subject"] = headerValue;
                        break;
                }
            }

            if (bodyStart < emailText.Length)
            {
                var body = emailText.Substring(bodyStart).Trim();
                message.Content = CleanEmailBody(body);
            }

            return string.IsNullOrEmpty(message.Content) ? null : message;
        }

        private HashSet<string> ExtractChatParticipants(string chatText)
        {
            var participants = new HashSet<string>();
            var matches = SlackMessagePattern.Matches(chatText);

            foreach (Match match in matches)
            {
                participants.Add(match.Groups[1].Value);
            }

            if (participants.Count == 0)
            {
                return ExtractSimpleParticipants(chatText);
            }

            return participants;
        }

        private List<Message> ExtractChatMessages(string chatText)
        {
            var messages = new List<Message>();
            var matches = SlackMessagePattern.Matches(chatText);

            foreach (Match match in matches)
            {
                messages.Add(new Message
                {
                    Id = $"msg{messages.Count + 1}",
                    ParticipantId = match.Groups[1].Value,
                    Timestamp = ParseChatTimestamp(match.Groups[2].Value),
                    Content = match.Groups[3].Value.Trim()
                });
            }

            if (messages.Count == 0)
            {
                return ExtractSimpleMessages(chatText);
            }

            return messages;
        }

        private HashSet<string> ExtractSimpleParticipants(string text)
        {
            var participants = new HashSet<string>();
            var matches = SimpleMessagePattern.Matches(text);

            foreach (Match match in matches)
            {
                participants.Add(match.Groups[1].Value);
            }

            return participants;
        }

        private List<Message> ExtractSimpleMessages(string text)
        {
            var messages = new List<Message>();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var currentTimestamp = DateTime.UtcNow;

            foreach (var line in lines)
            {
                var match = SimpleMessagePattern.Match(line.Trim());
                if (match.Success)
                {
                    messages.Add(new Message
                    {
                        Id = $"msg{messages.Count + 1}",
                        ParticipantId = match.Groups[1].Value,
                        Timestamp = currentTimestamp.AddMinutes(messages.Count),
                        Content = match.Groups[2].Value.Trim()
                    });
                }
            }

            return messages;
        }

        private LinguisticFeatures AnalyzeLinguisticFeaturesWithRegex(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new LinguisticFeatures();
            }

            var features = new LinguisticFeatures
            {
                Questions = ExtractQuestions(content),
                ContainsQuestion = content.Contains("?"),
                WordCount = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                SentenceCount = Regex.Matches(content, @"[.!?]+").Count + 1
            };

            features.Sentiment = DetectSentiment(content);
            features.Urgency = DetectUrgency(content);
            features.Politeness = DetectPoliteness(content);

            return features;
        }

        private List<string> ExtractQuestions(string text)
        {
            var questions = new List<string>();
            var sentences = Regex.Split(text, @"[.!?]+\s*");

            foreach (var sentence in sentences)
            {
                var trimmed = sentence.Trim();
                if (trimmed.EndsWith("?") ||
                    Regex.IsMatch(trimmed, @"^\s*(what|when|where|who|why|how|can|could|would|should|is|are|do|does)\b", RegexOptions.IgnoreCase))
                {
                    questions.Add(trimmed.TrimEnd('?') + "?");
                }
            }

            return questions;
        }

        private string DetectSentiment(string text)
        {
            var lower = text.ToLower();

            if (Regex.IsMatch(lower, @"\b(frustrated|angry|disappointed|upset|annoyed|concerned)\b"))
                return "Negative";

            if (Regex.IsMatch(lower, @"\b(great|excellent|perfect|thank|appreciate|happy|glad)\b"))
                return "Positive";

            return "Neutral";
        }

        private string DetectUrgency(string text)
        {
            var lower = text.ToLower();

            if (Regex.IsMatch(lower, @"\b(asap|urgent|immediately|critical|emergency|now)\b") ||
                text.Contains("!!!"))
                return "High";

            if (Regex.IsMatch(lower, @"\b(soon|quickly|important|priority)\b") ||
                text.Contains("!!"))
                return "Medium";

            return "Low";
        }

        private double DetectPoliteness(string text)
        {
            var lower = text.ToLower();
            var score = 0.5;

            if (Regex.IsMatch(lower, @"\b(please|thank|appreciate|kindly|would you)\b"))
                score += 0.3;

            if (Regex.IsMatch(lower, @"\b(must|need|have to|should have)\b") && !lower.Contains("please"))
                score -= 0.2;

            if (text.Contains("!"))
                score -= 0.1;

            return Math.Max(0, Math.Min(1, score));
        }

        #endregion

        #region Advanced (AI) Parsing

        private async Task ParseWithAI(ThreadCapsule capsule, string conversationText, string sourceType)
        {
            if (_aiService == null)
            {
                throw new InvalidOperationException("AI service not available for Advanced parsing");
            }

            var parsedData = await ParseConversationWithAI(conversationText, sourceType);
            capsule.Participants = parsedData.Participants;
            capsule.Messages = parsedData.Messages;
        }

        private async Task<(List<Participant> Participants, List<Message> Messages)> ParseConversationWithAI(
            string conversationText, string sourceType)
        {
            var prompt = $@"Parse the following {sourceType} conversation and extract structured data.

Conversation:
{conversationText}

Provide a JSON response with the following structure:
{{
  ""participants"": [
    {{
      ""name"": ""participant name"",
      ""email"": ""email if available"",
      ""identifier"": ""unique identifier""
    }}
  ],
  ""messages"": [
    {{
      ""participantIdentifier"": ""who sent it"",
      ""timestamp"": ""ISO 8601 timestamp if available"",
      ""content"": ""message content""
    }}
  ]
}}

Instructions:
1. Identify all unique participants
2. Extract each message with its sender and timestamp
3. For emails, parse headers (From, To, Subject, Date)
4. For chat messages, identify username and timestamp patterns
5. Clean quoted text and signatures from email bodies
6. Preserve chronological order
7. If timestamps are not provided, use relative ordering
8. Return ONLY valid JSON, no additional text";

            var response = await _aiService!.GenerateStructuredResponseAsync(prompt);
            return ParseStructuredConversation(response);
        }

        private async Task<List<Participant>> ExtractParticipantsWithAI(string conversationText, string sourceType)
        {
            var prompt = BuildParticipantExtractionPrompt(conversationText, sourceType);
            var response = await _aiService!.GenerateStructuredResponseAsync(prompt);
            return ParseParticipantsFromResponse(response);
        }

        private async Task<List<Message>> ExtractMessagesWithAI(string conversationText, string sourceType)
        {
            var prompt = BuildMessageExtractionPrompt(conversationText, sourceType);
            var response = await _aiService!.GenerateStructuredResponseAsync(prompt);

            var messages = ParseMessagesFromResponse(response);

            foreach (var message in messages)
            {
                message.LinguisticFeatures = await AnalyzeLinguisticFeaturesWithAI(message.Content);
            }

            return messages;
        }

        private async Task<LinguisticFeatures> AnalyzeLinguisticFeaturesWithAI(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new LinguisticFeatures();
            }

            var prompt = $@"Analyze the following message and provide linguistic analysis.

Message: ""{content}""

Provide a JSON response with this structure:
{{
  ""questions"": [""list of questions""],
  ""containsQuestion"": true/false,
  ""wordCount"": number,
  ""sentenceCount"": number,
  ""sentiment"": ""Positive/Negative/Neutral"",
  ""urgency"": ""High/Medium/Low"",
  ""politenessScore"": 0.0-1.0
}}

Return ONLY valid JSON.";

            var response = await _aiService!.GenerateStructuredResponseAsync(prompt);
            return ParseLinguisticFeatures(response, content);
        }

        #endregion

        #region JSON Parsing

        private (List<Participant> Participants, List<Message> Messages) ParseStructuredConversation(string jsonResponse)
        {
            try
            {
                var json = ExtractJsonFromResponse(jsonResponse);
                
                using var doc = JsonDocument.Parse(JsonHelper.CleanJsonResponse(json));
                var root = doc.RootElement;

                var participants = new List<Participant>();
                if (root.TryGetProperty("participants", out var participantsArray))
                {
                    var index = 1;
                    foreach (var p in participantsArray.EnumerateArray())
                    {
                        participants.Add(new Participant
                        {
                            Id = $"p{index++}",
                            Name = p.TryGetProperty("name", out var name) ? name.GetString() ?? "Unknown" : "Unknown",
                            Email = p.TryGetProperty("email", out var email) ? email.GetString() : null
                        });
                    }
                }

                var messages = new List<Message>();
                if (root.TryGetProperty("messages", out var messagesArray))
                {
                    var msgIndex = 1;
                    foreach (var m in messagesArray.EnumerateArray())
                    {
                        var message = new Message
                        {
                            Id = $"msg{msgIndex++}",
                            ParticipantId = m.TryGetProperty("participantIdentifier", out var pid) 
                                ? pid.GetString() ?? "Unknown" : "Unknown",
                            Content = m.TryGetProperty("content", out var content) 
                                ? content.GetString() ?? "" : "",
                            Timestamp = DateTime.UtcNow
                        };

                        if (m.TryGetProperty("timestamp", out var ts) && 
                            DateTime.TryParse(ts.GetString(), out var timestamp))
                        {
                            message.Timestamp = timestamp;
                        }
                        else
                        {
                            message.Timestamp = DateTime.UtcNow.AddMinutes(msgIndex - 1);
                        }

                        messages.Add(message);
                    }
                }

                return (participants, messages);
            }
            catch (JsonException)
            {
                return (new List<Participant>(), new List<Message>());
            }
        }

        private List<Participant> ParseParticipantsFromResponse(string response)
        {
            try
            {
                var json = ExtractJsonFromResponse(response);
                using var doc = JsonDocument.Parse(JsonHelper.CleanJsonResponse(json));
                var root = doc.RootElement;

                var participants = new List<Participant>();
                var index = 1;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in root.EnumerateArray())
                    {
                        participants.Add(ParseSingleParticipant(p, index++));
                    }
                }
                else if (root.TryGetProperty("participants", out var participantsArray))
                {
                    foreach (var p in participantsArray.EnumerateArray())
                    {
                        participants.Add(ParseSingleParticipant(p, index++));
                    }
                }

                return participants;
            }
            catch
            {
                return new List<Participant>();
            }
        }

        private Participant ParseSingleParticipant(JsonElement element, int index)
        {
            return new Participant
            {
                Id = $"p{index}",
                Name = element.TryGetProperty("name", out var name) ? name.GetString() ?? "Unknown" : "Unknown",
                Email = element.TryGetProperty("email", out var email) ? email.GetString() : null
            };
        }

        private List<Message> ParseMessagesFromResponse(string response)
        {
            try
            {
                var json = ExtractJsonFromResponse(response);
                using var doc = JsonDocument.Parse(JsonHelper.CleanJsonResponse(json));
                var root = doc.RootElement;

                var messages = new List<Message>();
                var index = 1;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in root.EnumerateArray())
                    {
                        messages.Add(ParseSingleMessage(m, index++));
                    }
                }
                else if (root.TryGetProperty("messages", out var messagesArray))
                {
                    foreach (var m in messagesArray.EnumerateArray())
                    {
                        messages.Add(ParseSingleMessage(m, index++));
                    }
                }

                return messages;
            }
            catch
            {
                return new List<Message>();
            }
        }

        private Message ParseSingleMessage(JsonElement element, int index)
        {
            var message = new Message
            {
                Id = $"msg{index}",
                ParticipantId = element.TryGetProperty("participantIdentifier", out var pid) 
                    ? pid.GetString() ?? "Unknown" : "Unknown",
                Content = element.TryGetProperty("content", out var content) 
                    ? content.GetString() ?? "" : "",
                Timestamp = DateTime.UtcNow.AddMinutes(index - 1)
            };

            if (element.TryGetProperty("timestamp", out var ts) && 
                DateTime.TryParse(ts.GetString(), out var timestamp))
            {
                message.Timestamp = timestamp;
            }

            return message;
        }

        private LinguisticFeatures ParseLinguisticFeatures(string response, string originalContent)
        {
            try
            {
                var json = ExtractJsonFromResponse(response);
                using var doc = JsonDocument.Parse(JsonHelper.CleanJsonResponse(json));
                var root = doc.RootElement;

                var features = new LinguisticFeatures
                {
                    Questions = new List<string>(),
                    ContainsQuestion = originalContent.Contains("?"),
                    WordCount = originalContent.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                    SentenceCount = 1
                };

                if (root.TryGetProperty("questions", out var questions))
                {
                    foreach (var q in questions.EnumerateArray())
                    {
                        var question = q.GetString();
                        if (!string.IsNullOrEmpty(question))
                        {
                            features.Questions.Add(question);
                        }
                    }
                }

                if (root.TryGetProperty("containsQuestion", out var containsQ))
                {
                    features.ContainsQuestion = containsQ.GetBoolean();
                }

                if (root.TryGetProperty("wordCount", out var wc))
                {
                    features.WordCount = wc.GetInt32();
                }

                if (root.TryGetProperty("sentenceCount", out var sc))
                {
                    features.SentenceCount = sc.GetInt32();
                }

                if (root.TryGetProperty("sentiment", out var sentiment))
                {
                    features.Sentiment = sentiment.GetString() ?? "Neutral";
                }

                if (root.TryGetProperty("urgency", out var urgency))
                {
                    features.Urgency = urgency.GetString() ?? "Low";
                }

                if (root.TryGetProperty("politenessScore", out var politeness))
                {
                    features.Politeness = politeness.GetDouble();
                }

                return features;
            }
            catch
            {
                return new LinguisticFeatures
                {
                    Questions = new List<string>(),
                    ContainsQuestion = originalContent.Contains("?"),
                    WordCount = originalContent.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                    SentenceCount = 1,
                    Sentiment = "Neutral",
                    Urgency = "Low",
                    Politeness = 0.5
                };
            }
        }

        private string ExtractJsonFromResponse(string response)
        {
            response = response.Trim();
            
            if (response.StartsWith("```json"))
            {
                response = response.Substring(7);
            }
            else if (response.StartsWith("```"))
            {
                response = response.Substring(3);
            }

            if (response.EndsWith("```"))
            {
                response = response.Substring(0, response.Length - 3);
            }

            return response.Trim();
        }

        #endregion

        #region Prompt Building

        private string BuildParticipantExtractionPrompt(string conversationText, string sourceType)
        {
            return $@"Extract all participants from the following {sourceType} conversation.

Conversation:
{conversationText}

Return a JSON array: [{{""name"": ""..."", ""email"": ""...""}}]
Return ONLY the JSON array.";
        }

        private string BuildMessageExtractionPrompt(string conversationText, string sourceType)
        {
            return $@"Extract all messages from the following {sourceType} conversation.

Conversation:
{conversationText}

Return a JSON array: [{{""participantIdentifier"": ""..."", ""timestamp"": ""..."", ""content"": ""...""}}]
Return ONLY the JSON array.";
        }

        #endregion

        #region Helper Methods

        private void LinkMessagesToParticipants(ThreadCapsule capsule)
        {
            foreach (var message in capsule.Messages)
            {
                var participant = capsule.Participants.FirstOrDefault(p =>
                    string.Equals(p.Name, message.ParticipantId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Email, message.ParticipantId, StringComparison.OrdinalIgnoreCase) ||
                    p.Id == message.ParticipantId);

                if (participant != null)
                {
                    message.ParticipantId = participant.Id;
                }
            }
        }

        private string ExtractEmail(string text)
        {
            var match = EmailAddressPattern.Match(text);
            return match.Success ? match.Value : text.Trim();
        }

        private string? ExtractEmailFromName(string name, string conversationText)
        {
            if (EmailAddressPattern.IsMatch(name))
                return name;

            var pattern = new Regex($@"\b{Regex.Escape(name)}\b.*?({EmailAddressPattern})", RegexOptions.IgnoreCase);
            var match = pattern.Match(conversationText);
            return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value : null;
        }

        private DateTime ParseEmailDate(string dateStr)
        {
            if (DateTime.TryParse(dateStr, out var result))
                return result;

            return DateTime.UtcNow;
        }

        private DateTime ParseChatTimestamp(string timeStr)
        {
            var today = DateTime.Today;
            if (DateTime.TryParse(timeStr, out var time))
            {
                return today.Add(time.TimeOfDay);
            }

            return DateTime.UtcNow;
        }

        private string CleanEmailBody(string body)
        {
            var lines = body.Split('\n');
            var cleanLines = new List<string>();

            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith(">"))
                    continue;

                if (line.Contains("--") && cleanLines.Count > 0)
                    break;

                cleanLines.Add(line);
            }

            return string.Join("\n", cleanLines).Trim();
        }

        #endregion
    }
}
