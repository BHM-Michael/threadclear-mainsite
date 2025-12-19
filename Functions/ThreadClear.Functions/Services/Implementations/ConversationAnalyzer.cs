using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadClear.Functions.Helpers;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    /// <summary>
    /// ⭐ HERO FEATURES - AI-powered conversation analysis
    /// Leverages Claude API for intelligent, evolving analysis without hardcoded patterns
    /// </summary>
    public class ConversationAnalyzer : IConversationAnalyzer
    {
        private readonly IAIService _aiService;
        private readonly ILogger<ConversationAnalyzer> _logger;

        public ConversationAnalyzer(IAIService aiService,
    ILogger<ConversationAnalyzer> logger)
        {
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _logger = logger; 
        }

        public async Task AnalyzeConversation(ThreadCapsule capsule)
        {
            if (capsule.Analysis == null)
            {
                capsule.Analysis = new ConversationAnalysis();
            }

            // Run all AI calls in parallel
            var unansweredTask = DetectUnansweredQuestions(capsule);
            var tensionTask = IdentifyTensionPoints(capsule);
            var misalignmentsTask = DetectMisalignments(capsule);
            var healthTask = AssessConversationHealth(capsule);
            var decisionsTask = TrackDecisions(capsule);
            var actionItemsTask = IdentifyActionItems(capsule);
            var suggestionsTask = _aiService.GenerateSuggestedActions(capsule);

            // Wait for all to complete
            await Task.WhenAll(
                unansweredTask,
                tensionTask,
                misalignmentsTask,
                healthTask,
                decisionsTask,
                actionItemsTask,
                suggestionsTask
            );

            // Assign results
            capsule.Analysis.UnansweredQuestions = await unansweredTask;
            capsule.Analysis.TensionPoints = await tensionTask;
            capsule.Analysis.Misalignments = await misalignmentsTask;
            capsule.Analysis.ConversationHealth = await healthTask;
            capsule.Analysis.Decisions = await decisionsTask;
            capsule.Analysis.ActionItems = await actionItemsTask;
            capsule.SuggestedActions = await suggestionsTask;
        }

        /// <summary>
        /// ⭐ HERO: Detect questions that haven't been answered using AI
        /// </summary>
        public async Task<List<UnansweredQuestion>> DetectUnansweredQuestions(ThreadCapsule capsule)
        {
            var prompt = BuildUnansweredQuestionsPrompt(capsule);
            var result = await _aiService.AnalyzeConversation(prompt, capsule);

            // Parse AI response into structured data
            return ParseUnansweredQuestionsResponse(result, capsule);
        }

        /// <summary>
        /// ⭐ HERO: Identify tension points using AI natural language understanding
        /// </summary>
        public async Task<List<TensionPoint>> IdentifyTensionPoints(ThreadCapsule capsule)
        {
            var prompt = BuildTensionPointsPrompt(capsule);
            var result = await _aiService.AnalyzeConversation(prompt, capsule);

            return ParseTensionPointsResponse(result, capsule);
        }

        /// <summary>
        /// ⭐ HERO: Detect misalignments using AI
        /// </summary>
        public async Task<List<Misalignment>> DetectMisalignments(ThreadCapsule capsule)
        {
            var prompt = BuildMisalignmentsPrompt(capsule);
            var result = await _aiService.AnalyzeConversation(prompt, capsule);

            return ParseMisalignmentsResponse(result, capsule);
        }

        /// <summary>
        /// Track decisions made in the conversation using AI
        /// </summary>
        public async Task<List<DecisionPoint>> TrackDecisions(ThreadCapsule capsule)
        {
            var prompt = BuildDecisionsPrompt(capsule);
            var result = await _aiService.AnalyzeConversation(prompt, capsule);

            return ParseDecisionsResponse(result, capsule);
        }

        /// <summary>
        /// Identify action items using AI
        /// </summary>
        public async Task<List<ActionItem>> IdentifyActionItems(ThreadCapsule capsule)
        {
            var prompt = BuildActionItemsPrompt(capsule);
            var result = await _aiService.AnalyzeConversation(prompt, capsule);

            return ParseActionItemsResponse(result, capsule);
        }

        /// <summary>
        /// Assess overall conversation health using AI
        /// </summary>
        public async Task<ConversationHealth> AssessConversationHealth(ThreadCapsule capsule)
        {
            var prompt = BuildHealthAssessmentPrompt(capsule);
            var result = await _aiService.AnalyzeConversation(prompt, capsule);

            return ParseHealthAssessmentResponse(result);
        }

        #region Prompt Builders

        private string BuildUnansweredQuestionsPrompt(ThreadCapsule capsule)
        {
            return @$"Analyze this conversation thread and identify any questions that were asked but never answered.

For each unanswered question, provide:
1. The exact question text
2. Who asked it
3. When it was asked (message timestamp)
4. Whether it was asked multiple times (indicating urgency/frustration)
5. The message ID where it appears

Consider semantic meaning - a question might be answered indirectly or partially.

Return your analysis as JSON in this format:
{{
  ""unansweredQuestions"": [
    {{
      ""question"": ""string"",
      ""askedBy"": ""string"",
      ""askedAt"": ""ISO8601 timestamp"",
      ""timesAsked"": number,
      ""messageId"": ""string""
    }}
  ]
}}

Conversation context:
{FormatConversationForAI(capsule)}";
        }

        private string BuildTensionPointsPrompt(ThreadCapsule capsule)
        {
            return @$"Analyze this conversation for signs of tension, frustration, or communication breakdown.

Identify:
1. Dismissive or passive-aggressive language
2. Repeated questions (indicates frustration)
3. Urgent language or escalating tone
4. Long delays in response to important questions
5. Emotional language or heated exchanges
6. Misunderstandings or talking past each other

For each tension point, classify:
- Type: (Dismissive, RepeatedQuestion, Urgent, DelayedResponse, Emotional, Escalation, etc.)
- Severity: (Low, Moderate, High)
- Description: Brief explanation
- MessageId: Where it occurs
- Timestamp

Return as JSON:
{{
  ""tensionPoints"": [
    {{
      ""type"": ""string"",
      ""severity"": ""string"",
      ""description"": ""string"",
      ""messageId"": ""string"",
      ""timestamp"": ""ISO8601""
    }}
  ]
}}

Conversation context:
{FormatConversationForAI(capsule)}";
        }

        private string BuildMisalignmentsPrompt(ThreadCapsule capsule)
        {
            return @$"Identify misalignments in this conversation where participants have different expectations, understandings, or goals.

Look for:
1. Conflicting priorities or values
2. Different interpretations of the same information
3. Unspoken assumptions causing confusion
4. Goal misalignment between participants
5. Communication style clashes

For each misalignment:
- Type: (Priority, Understanding, Goal, Assumption, Style)
- Severity: (Low, Moderate, High)
- Description: What's misaligned
- ParticipantsInvolved: Who's affected
- SuggestedResolution: How to address it

Return as JSON:
{{
  ""misalignments"": [
    {{
      ""type"": ""string"",
      ""severity"": ""string"",
      ""description"": ""string"",
      ""participantsInvolved"": [""string""],
      ""suggestedResolution"": ""string""
    }}
  ]
}}

Conversation context:
{FormatConversationForAI(capsule)}";
        }

        private string BuildDecisionsPrompt(ThreadCapsule capsule)
        {
            return @$"Extract all decisions made in this conversation.

A decision includes:
- Agreements reached
- Plans approved
- Actions confirmed
- Commitments made

For each decision:
- DecisionText: What was decided
- DecidedBy: Who made/approved it
- Timestamp: When
- MessageId: Where it appears

Return as JSON:
{{
  ""decisions"": [
    {{
      ""decisionText"": ""string"",
      ""decidedBy"": ""string"",
      ""timestamp"": ""ISO8601"",
      ""messageId"": ""string""
    }}
  ]
}}

Conversation context:
{FormatConversationForAI(capsule)}";
        }

        private string BuildActionItemsPrompt(ThreadCapsule capsule)
        {
            return @$"Identify all action items from this conversation.

An action item is something someone needs to do, including:
- Explicit requests (""Can you..."", ""Please..."")
- Commitments (""I will..."")
- Assignments (""You should..."")
- TODO items

For each action item:
- Action: What needs to be done
- AssignedTo: Who should do it (or ""Unassigned"")
- RequestedBy: Who requested it
- Timestamp: When it was mentioned
- MessageId: Where it appears
- Priority: (Low, Medium, High) based on urgency
- Status: ""Pending"" (default)

Return as JSON:
{{
  ""actionItems"": [
    {{
      ""action"": ""string"",
      ""assignedTo"": ""string"",
      ""requestedBy"": ""string"",
      ""timestamp"": ""ISO8601"",
      ""messageId"": ""string"",
      ""priority"": ""string"",
      ""status"": ""Pending""
    }}
  ]
}}

Conversation context:
{FormatConversationForAI(capsule)}";
        }

        private string BuildHealthAssessmentPrompt(ThreadCapsule capsule)
        {
            return @$"Assess the overall health of this conversation.

Consider:
1. Response rates and timing
2. Engagement levels
3. Emotional tone
4. Question answering rate
5. Clarity of communication
6. Presence of tension or misalignment

Provide scores from 0-100 where 100 is best:
- overallScore: Overall conversation health
- clarityScore: How clear is the communication
- responsivenessScore: How well are questions being answered
- alignmentScore: How aligned are participants on goals/expectations
- riskLevel: (Low, Medium, High) - risk of communication breakdown
- issues: List of concerning patterns
- strengths: What's working well
- recommendations: How to improve

Return ONLY valid JSON with no markdown formatting:
{{
  ""overallScore"": number,
  ""clarityScore"": number,
  ""responsivenessScore"": number,
  ""alignmentScore"": number,
  ""riskLevel"": ""string"",
  ""issues"": [""string""],
  ""strengths"": [""string""],
  ""recommendations"": [""string""]
}}

Conversation context:
{FormatConversationForAI(capsule)}";
        }

        private string FormatConversationForAI(ThreadCapsule capsule)
        {
            var formatted = new System.Text.StringBuilder();

            formatted.AppendLine($"Thread: {capsule.ThreadMetadata.Subject}");
            formatted.AppendLine($"Platform: {capsule.ThreadMetadata.Platform}");
            formatted.AppendLine($"Participants: {string.Join(", ", capsule.Participants.Select(p => p.Name))}");
            formatted.AppendLine($"Started: {capsule.ThreadMetadata.StartDate}");
            formatted.AppendLine($"Total Messages: {capsule.Messages.Count}");
            formatted.AppendLine();
            formatted.AppendLine("Messages:");
            formatted.AppendLine("---");

            foreach (var message in capsule.Messages.OrderBy(m => m.Timestamp))
            {
                var participant = capsule.Participants.FirstOrDefault(p => p.Id == message.ParticipantId);
                formatted.AppendLine($"[{message.Timestamp:yyyy-MM-dd HH:mm}] {participant?.Name ?? message.ParticipantId} (ID: {message.Id}):");
                formatted.AppendLine(message.Content);
                formatted.AppendLine();
            }

            return formatted.ToString();
        }

        #endregion

        #region Response Parsers

        private List<UnansweredQuestion> ParseUnansweredQuestionsResponse(string response, ThreadCapsule capsule)
        {
            try
            {
                var cleanJson = JsonHelper.CleanJsonResponse(response);
                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                var questions = new List<UnansweredQuestion>();

                if (root.TryGetProperty("unansweredQuestions", out var questionsArray))
                {
                    foreach (var q in questionsArray.EnumerateArray())
                    {
                        questions.Add(new UnansweredQuestion
                        {
                            Question = q.GetStringSafe("question"),
                            AskedBy = q.GetStringSafe("askedBy"),
                            AskedAt = q.GetDateTimeSafe("askedAt"),
                            TimesAsked = q.GetInt32Safe("timesAsked", 1),
                            MessageId = q.GetStringSafe("messageId", null)
                        });
                    }
                }

                return questions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing unanswered questions: {Response}", response);
                return new List<UnansweredQuestion>();
            }
        }

        private List<TensionPoint> ParseTensionPointsResponse(string response, ThreadCapsule capsule)
        {
            try
            {
                var cleanJson = JsonHelper.CleanJsonResponse(response);
                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                var tensions = new List<TensionPoint>();

                if (root.TryGetProperty("tensionPoints", out var tensionArray))
                {
                    foreach (var t in tensionArray.EnumerateArray())
                    {
                        tensions.Add(new TensionPoint
                        {
                            Type = t.GetStringSafe("type", "Conflict"),
                            Description = t.GetStringSafe("description"),
                            Severity = t.GetStringSafe("severity", "Medium"),
                            Timestamp = t.GetDateTimeSafe("timestamp"),
                            DetectedAt = t.GetDateTimeSafe("detectedAt"),
                            MessageId = t.GetStringSafe("messageId", null),
                            Participants = t.ParseStringArray("participants")
                        });
                    }
                }

                return tensions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing tension points: {Response}", response);
                return new List<TensionPoint>();
            }
        }

        private List<Misalignment> ParseMisalignmentsResponse(string aiResponse, ThreadCapsule capsule)
        {
            try
            {
                var cleanJson = JsonHelper.CleanJsonResponse(aiResponse);
                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                var misalignments = new List<Misalignment>();

                if (root.TryGetProperty("misalignments", out var misalignArray))
                {
                    foreach (var m in misalignArray.EnumerateArray())
                    {
                        misalignments.Add(new Misalignment
                        {
                            Type = m.GetStringSafe("type"),
                            Description = m.GetStringSafe("description"),
                            ParticipantsInvolved = m.ParseStringArray("participants"),
                            Severity = m.GetStringSafe("severity", "Medium"),
                            SuggestedResolution = m.GetStringSafe("suggestedResolution")
                        });
                    }
                }

                return misalignments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing misalignments: {Response}", aiResponse);
                return new List<Misalignment>();
            }
        }

        private List<DecisionPoint> ParseDecisionsResponse(string aiResponse, ThreadCapsule capsule)
        {
            try
            {
                var cleanJson = JsonHelper.CleanJsonResponse(aiResponse);
                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                var decisions = new List<DecisionPoint>();

                if (root.TryGetProperty("decisions", out var decisionArray))
                {
                    foreach (var d in decisionArray.EnumerateArray())
                    {
                        decisions.Add(new DecisionPoint
                        {
                            Decision = d.GetStringSafe("decision"),
                            DecidedBy = d.GetStringSafe("decidedBy"),
                            Timestamp = d.GetDateTimeSafe("timestamp"),
                            MessageId = d.GetStringSafe("messageId")
                        });
                    }
                }

                return decisions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing decisions: {Response}", aiResponse);
                return new List<DecisionPoint>();
            }
        }

        private List<ActionItem> ParseActionItemsResponse(string aiResponse, ThreadCapsule capsule)
        {
            try
            {
                var parsed = JsonDocument.Parse(JsonHelper.CleanJsonResponse(aiResponse));
                var actionItems = new List<ActionItem>();

                if (parsed.RootElement.TryGetProperty("actionItems", out var itemsArray))
                {
                    foreach (var a in itemsArray.EnumerateArray())
                    {
                        actionItems.Add(new ActionItem
                        {
                            Action = a.GetProperty("action").GetString(),
                            AssignedTo = a.GetProperty("assignedTo").GetString(),
                            RequestedBy = a.GetProperty("requestedBy").GetString(),
                            Timestamp = DateTime.Parse(a.GetProperty("timestamp").GetString()),
                            MessageId = a.GetProperty("messageId").GetString(),
                            Priority = a.TryGetProperty("priority", out var priority) ? priority.GetString() : "Medium",
                            Status = "Pending"
                        });
                    }
                }

                return actionItems;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing action items: {ex.Message}");
                return new List<ActionItem>();
            }
        }

        private ConversationHealth ParseHealthAssessmentResponse(string aiResponse)
        {
            try
            {
                var cleanJson = JsonHelper.CleanJsonResponse(aiResponse);
                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                return new ConversationHealth
                {
                    HealthScore = root.TryGetProperty("overallScore", out var os) ? os.GetDouble() / 100.0 : 0.5,
                    ClarityScore = root.TryGetProperty("clarityScore", out var cs) ? cs.GetDouble() / 100.0 : 0.5,
                    ResponsivenessScore = root.TryGetProperty("responsivenessScore", out var rs) ? rs.GetDouble() / 100.0 : 0.5,
                    AlignmentScore = root.TryGetProperty("alignmentScore", out var as_) ? as_.GetDouble() / 100.0 : 0.5,
                    RiskLevel = root.GetStringSafe("riskLevel", "Medium"),
                    Issues = root.ParseStringArray("issues").Cast<string?>().ToList(),
                    Strengths = root.ParseStringArray("strengths").Cast<string?>().ToList(),
                    Recommendations = root.ParseStringArray("recommendations").Cast<string?>().ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing health assessment: {Response}", aiResponse);
                return new ConversationHealth
                {
                    HealthScore = 0.5,
                    RiskLevel = "Unknown"
                };
            }
        }

        #endregion
    }
}
