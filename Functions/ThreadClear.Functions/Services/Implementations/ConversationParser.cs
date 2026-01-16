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
    /// AI-powered conversation parser - single call for parsing + analysis
    /// </summary>
    public class ConversationParser : IConversationParser
    {
        private readonly IAIService _aiService;

        public ConversationParser(IAIService aiService)
        {
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
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

            var detectedSourceType = DetectSourceType(conversationText, sourceType);

            var capsule = new ThreadCapsule
            {
                CapsuleId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                SourceType = detectedSourceType,
                RawText = conversationText,
            };

            capsule.Metadata["ParsingMode"] = "Advanced";
            capsule.Metadata["DetectedSourceType"] = detectedSourceType;
            capsule.Metadata["ProvidedSourceType"] = sourceType;

            await ParseAndAnalyzeWithAI(capsule, conversationText, detectedSourceType);

            return capsule;
        }

        public async Task<List<Participant>> ExtractParticipants(string conversationText, string sourceType, ParsingMode? mode = null)
        {
            var capsule = await ParseConversation(conversationText, sourceType, mode);
            return capsule.Participants;
        }

        public async Task<List<Message>> ExtractMessages(string conversationText, string sourceType, ParsingMode? mode = null)
        {
            var capsule = await ParseConversation(conversationText, sourceType, mode);
            return capsule.Messages;
        }

        private string DetectSourceType(string conversationText, string providedSourceType)
        {
            if (!string.IsNullOrEmpty(providedSourceType) && providedSourceType.ToLower() != "simple")
                return providedSourceType;

            var hasFrom = Regex.IsMatch(conversationText, @"^From:\s*.+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            var hasDate = Regex.IsMatch(conversationText, @"^Date:\s*.+\d{4}", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            var hasSubject = Regex.IsMatch(conversationText, @"^Subject:\s*.+", RegexOptions.Multiline | RegexOptions.IgnoreCase);

            if (hasFrom && (hasDate || hasSubject))
                return "email";

            var outlookPattern = new Regex(@"^(You|\w+\s+\w+)\s*\n\w{3}\s+\d{1,2}/\d{1,2}/\d{4}\s+\d{1,2}:\d{2}\s*(AM|PM)",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (outlookPattern.Matches(conversationText).Count >= 2)
                return "email";

            var slackPattern = new Regex(@"^(\w+)\s+\[(\d{1,2}:\d{2}\s*(?:AM|PM)?)\]:\s*(.+)$", RegexOptions.Multiline);
            if (slackPattern.Matches(conversationText).Count >= 2)
                return "slack";

            return "conversation";
        }

        private async Task ParseAndAnalyzeWithAI(ThreadCapsule capsule, string conversationText, string sourceType)
        {
            var prompt = $@"Analyze this {sourceType} conversation completely. Return a single JSON object.

CONVERSATION:
{conversationText}

Return this exact JSON structure:
{{
  ""participants"": [
    {{""name"": ""Full Name"", ""email"": ""email@example.com or null""}}
  ],
  ""messages"": [
    {{""sender"": ""Full Name"", ""timestamp"": ""2026-01-12T17:27:00"", ""content"": ""actual message text only""}}
  ],
  ""summary"": ""2-3 sentence summary"",
  ""unansweredQuestions"": [
    {{""question"": ""exact question ending with ?"", ""askedBy"": ""name""}}
  ],
  ""tensionPoints"": [
    {{""description"": ""what tension exists"", ""severity"": ""Low|Medium|High"", ""participants"": [""names""]}}
  ],
  ""misalignments"": [
    {{""topic"": ""what they disagree about"", ""severity"": ""Low|Medium|High"", ""participantsInvolved"": [""names""]}}
  ],
  ""conversationHealth"": {{
    ""overallScore"": 75,
    ""riskLevel"": ""Low|Medium|High"",
    ""issues"": [],
    ""strengths"": []
  }},
  ""suggestedActions"": [
    {{""action"": ""specific next step"", ""priority"": ""Low|Medium|High"", ""reasoning"": ""why""}}
  ]
}}

RULES:
- participants: Extract real names, not email headers like 'From' or 'Date'
- messages: Include ONLY the actual message text. EXCLUDE signatures, job titles, phone numbers, company names, disclaimers, email headers, legal notices
- messages: Return in chronological order (oldest first)
- unansweredQuestions: Only DIRECT questions with '?' that received no clear response. NOT statements like 'let me know if...'
- tensionPoints: Identify frustration, urgency, repeated requests, or conflict
- misalignments: Different expectations, disagreements, or miscommunication
- suggestedActions: Specific, actionable next steps based on the analysis

Return ONLY valid JSON, no markdown or explanation.";

            var response = await _aiService.GenerateStructuredResponseAsync(prompt);
            ParseCompleteResponse(capsule, response);
        }

        private void ParseCompleteResponse(ThreadCapsule capsule, string response)
        {
            try
            {
                var cleanJson = JsonHelper.CleanJsonResponse(response);
                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                // Participants
                capsule.Participants = new List<Participant>();
                if (root.TryGetProperty("participants", out var participants))
                {
                    int pIndex = 1;
                    foreach (var p in participants.EnumerateArray())
                    {
                        var name = p.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        if (string.IsNullOrWhiteSpace(name) || IsEmailHeader(name)) continue;

                        capsule.Participants.Add(new Participant
                        {
                            Id = $"p{pIndex++}",
                            Name = name,
                            Email = p.TryGetProperty("email", out var e) ? e.GetString() : null
                        });
                    }
                }

                // Messages
                capsule.Messages = new List<Message>();
                if (root.TryGetProperty("messages", out var messages))
                {
                    int mIndex = 1;
                    foreach (var m in messages.EnumerateArray())
                    {
                        var content = m.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                        if (string.IsNullOrWhiteSpace(content)) continue;

                        var sender = m.TryGetProperty("sender", out var s) ? s.GetString() ?? "Unknown" : "Unknown";

                        var message = new Message
                        {
                            Id = $"msg{mIndex++}",
                            ParticipantId = sender,
                            Content = content,
                            Timestamp = DateTime.UtcNow
                        };

                        if (m.TryGetProperty("timestamp", out var ts) && DateTime.TryParse(ts.GetString(), out var parsed))
                            message.Timestamp = parsed;

                        capsule.Messages.Add(message);
                    }
                }

                LinkMessagesToParticipants(capsule);

                // Summary
                if (root.TryGetProperty("summary", out var summary))
                    capsule.Summary = summary.GetString() ?? "";

                // Analysis
                capsule.Analysis = new ConversationAnalysis();

                // Unanswered Questions
                if (root.TryGetProperty("unansweredQuestions", out var uq))
                {
                    capsule.Analysis.UnansweredQuestions = uq.EnumerateArray()
                        .Select(q => new UnansweredQuestion
                        {
                            Question = q.TryGetProperty("question", out var qt) ? qt.GetString() ?? "" : "",
                            AskedBy = q.TryGetProperty("askedBy", out var ab) ? ab.GetString() ?? "" : "",
                            TimesAsked = 1,
                            AskedAt = DateTime.UtcNow
                        })
                        .Where(q => !string.IsNullOrWhiteSpace(q.Question) && q.Question.Contains("?"))
                        .ToList();
                }
                else
                {
                    capsule.Analysis.UnansweredQuestions = new List<UnansweredQuestion>();
                }

                // Tension Points
                if (root.TryGetProperty("tensionPoints", out var tp))
                {
                    capsule.Analysis.TensionPoints = tp.EnumerateArray()
                        .Select(t => new TensionPoint
                        {
                            Description = t.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                            Severity = t.TryGetProperty("severity", out var sv) ? sv.GetString() ?? "Low" : "Low",
                            Type = "Detected",
                            Timestamp = DateTime.UtcNow,
                            DetectedAt = DateTime.UtcNow,
                            Participants = t.TryGetProperty("participants", out var ps)
                                ? ps.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                                : new List<string>()
                        })
                        .Where(t => !string.IsNullOrWhiteSpace(t.Description))
                        .ToList();
                }
                else
                {
                    capsule.Analysis.TensionPoints = new List<TensionPoint>();
                }

                // Misalignments
                if (root.TryGetProperty("misalignments", out var ma))
                {
                    capsule.Analysis.Misalignments = ma.EnumerateArray()
                        .Select(m => new Misalignment
                        {
                            Type = m.TryGetProperty("topic", out var t) ? t.GetString() ?? "" : "",
                            Severity = m.TryGetProperty("severity", out var sv) ? sv.GetString() ?? "Low" : "Low",
                            ParticipantsInvolved = m.TryGetProperty("participantsInvolved", out var pi)
                                ? pi.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                                : new List<string>()
                        })
                        .Where(m => !string.IsNullOrWhiteSpace(m.Type))
                        .ToList();
                }
                else
                {
                    capsule.Analysis.Misalignments = new List<Misalignment>();
                }

                // Conversation Health
                if (root.TryGetProperty("conversationHealth", out var ch))
                {
                    capsule.Analysis.ConversationHealth = new ConversationHealth
                    {
                        HealthScore = ch.TryGetProperty("overallScore", out var os) ? os.GetInt32() / 100.0 : 0.5,
                        RiskLevel = ch.TryGetProperty("riskLevel", out var rl) ? rl.GetString() ?? "Low" : "Low",
                        ClarityScore = 0.5,
                        ResponsivenessScore = 0.5,
                        AlignmentScore = 0.5,
                        Issues = ch.TryGetProperty("issues", out var iss)
                            ? iss.EnumerateArray().Select(x => x.GetString()).ToList()
                            : new List<string?>(),
                        Strengths = ch.TryGetProperty("strengths", out var str)
                            ? str.EnumerateArray().Select(x => x.GetString()).ToList()
                            : new List<string?>(),
                        Recommendations = new List<string?>()
                    };
                }
                else
                {
                    capsule.Analysis.ConversationHealth = new ConversationHealth
                    {
                        HealthScore = 0.5,
                        RiskLevel = "Unknown"
                    };
                }

                // Suggested Actions
                if (root.TryGetProperty("suggestedActions", out var sa))
                {
                    capsule.SuggestedActions = sa.EnumerateArray()
                        .Select(a => new SuggestedActionItem
                        {
                            Action = a.TryGetProperty("action", out var act) ? act.GetString() ?? "" : "",
                            Priority = a.TryGetProperty("priority", out var pr) ? pr.GetString() ?? "Medium" : "Medium",
                            Reasoning = a.TryGetProperty("reasoning", out var r) ? r.GetString() : null
                        })
                        .Where(a => !string.IsNullOrWhiteSpace(a.Action))
                        .ToList();
                }
                else
                {
                    capsule.SuggestedActions = new List<SuggestedActionItem>();
                }

                // Initialize empty collections
                capsule.Analysis.Decisions = new List<DecisionPoint>();
                capsule.Analysis.ActionItems = new List<ActionItem>();
            }
            catch (Exception)
            {
                // Fallback on parse error
                capsule.Participants = new List<Participant>();
                capsule.Messages = new List<Message>();
                capsule.Analysis = new ConversationAnalysis
                {
                    UnansweredQuestions = new List<UnansweredQuestion>(),
                    TensionPoints = new List<TensionPoint>(),
                    Misalignments = new List<Misalignment>(),
                    ConversationHealth = new ConversationHealth { HealthScore = 0.5, RiskLevel = "Unknown" },
                    Decisions = new List<DecisionPoint>(),
                    ActionItems = new List<ActionItem>()
                };
                capsule.SuggestedActions = new List<SuggestedActionItem>();
                capsule.Summary = "Unable to analyze conversation.";
            }
        }

        private void LinkMessagesToParticipants(ThreadCapsule capsule)
        {
            foreach (var message in capsule.Messages)
            {
                var participant = capsule.Participants.FirstOrDefault(p =>
                    string.Equals(p.Name, message.ParticipantId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Email, message.ParticipantId, StringComparison.OrdinalIgnoreCase));

                if (participant != null)
                {
                    message.ParticipantId = participant.Id;
                }
            }
        }

        private bool IsEmailHeader(string name)
        {
            var headers = new[] { "From", "To", "Cc", "Bcc", "Subject", "Date", "Sent", "Re", "Fw", "Fwd", "Mobile" };
            return headers.Contains(name, StringComparer.OrdinalIgnoreCase);
        }
    }
}