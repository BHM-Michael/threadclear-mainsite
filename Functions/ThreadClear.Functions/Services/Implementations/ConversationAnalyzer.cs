using Microsoft.Extensions.Logging;
using System;
using System.Text;
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
    /// ⭐ HERO FEATURES - Hybrid conversation analysis
    /// Supports both AI-powered (Advanced) and pattern-based (Basic) analysis
    /// Patterns are loaded from XML configuration for easy customization
    /// </summary>
    public class ConversationAnalyzer : IConversationAnalyzer
    {
        private readonly IAIService _aiService;
        private readonly ILogger<ConversationAnalyzer> _logger;
        private readonly AnalysisPatternsLoader _patterns;

        // Only keep regex patterns that need complex matching (not just word lists)
        private static readonly Regex QuestionPattern = new Regex(
            @"[^.!?]*\?",
            RegexOptions.Compiled);

        private static readonly Regex MultipleExclamationPattern = new Regex(
            @"!{2,}",
            RegexOptions.Compiled);

        public ConversationAnalyzer(
            IAIService aiService,
            ILogger<ConversationAnalyzer> logger,
            AnalysisPatternsLoader patterns)
        {
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _logger = logger;
            _patterns = patterns ?? throw new ArgumentNullException(nameof(patterns));
        }

        /// <summary>
        /// Alternate constructor that creates pattern loader with default path
        /// </summary>
        public ConversationAnalyzer(IAIService aiService, ILogger<ConversationAnalyzer> logger)
            : this(aiService, logger, new AnalysisPatternsLoader("AnalysisPatterns.xml"))
        {
        }

        public async Task AnalyzeConversation(ThreadCapsule capsule, AnalysisOptions? options = null)
        {
            if (capsule.Analysis == null)
            {
                capsule.Analysis = new ConversationAnalysis();
            }

            // Check parsing mode to determine analysis approach
            var useAI = ShouldUseAIAnalysis(capsule);

            if (options != null)
            {
                if (useAI)
                {
                    await AnalyzeConversationCombined(capsule, options);
                }
                else
                {
                    await AnalyzeConversationWithPatterns(capsule, options);
                }
            }
            else
            {
                if (useAI)
                {
                    await AnalyzeConversationFull(capsule);
                }
                else
                {
                    await AnalyzeConversationFullWithPatterns(capsule);
                }
            }
        }

        /// <summary>
        /// Determine if AI analysis should be used based on parsing mode
        /// </summary>
        private bool ShouldUseAIAnalysis(ThreadCapsule capsule)
        {
            // If ParsingMode is explicitly Basic, use pattern-based analysis
            if (capsule.Metadata.TryGetValue("ParsingMode", out var mode))
            {
                if (mode == "Basic")
                {
                    _logger.LogInformation("Using pattern-based analysis (Basic mode)");
                    return false;
                }
            }

            // Default to AI analysis
            _logger.LogInformation("Using AI-powered analysis (Advanced mode)");
            return true;
        }

        #region Full Analysis - AI (Admin / All Features)

        private async Task AnalyzeConversationFull(ThreadCapsule capsule)
        {
            // Run all AI calls in parallel
            var unansweredTask = DetectUnansweredQuestions(capsule);
            var tensionTask = IdentifyTensionPoints(capsule);
            var misalignmentsTask = DetectMisalignments(capsule);
            var healthTask = AssessConversationHealth(capsule);
            var decisionsTask = TrackDecisions(capsule);
            var actionItemsTask = IdentifyActionItems(capsule);
            var suggestionsTask = _aiService.GenerateSuggestedActions(capsule);

            await Task.WhenAll(
                unansweredTask,
                tensionTask,
                misalignmentsTask,
                healthTask,
                decisionsTask,
                actionItemsTask,
                suggestionsTask
            );

            capsule.Analysis.UnansweredQuestions = await unansweredTask;
            capsule.Analysis.TensionPoints = await tensionTask;
            capsule.Analysis.Misalignments = await misalignmentsTask;
            capsule.Analysis.ConversationHealth = await healthTask;
            capsule.Analysis.Decisions = await decisionsTask;
            capsule.Analysis.ActionItems = await actionItemsTask;
            capsule.SuggestedActions = await suggestionsTask;
        }

        #endregion

        #region Full Analysis - Pattern-Based (Basic Mode - FREE)

        /// <summary>
        /// Full analysis using XML patterns (no AI calls) - FREE
        /// </summary>
        private async Task AnalyzeConversationFullWithPatterns(ThreadCapsule capsule)
        {
            _logger.LogInformation("Running full pattern-based analysis (no AI costs)");

            capsule.Analysis.UnansweredQuestions = DetectUnansweredQuestionsWithPatterns(capsule);
            capsule.Analysis.TensionPoints = IdentifyTensionPointsWithPatterns(capsule);
            capsule.Analysis.Misalignments = DetectMisalignmentsWithPatterns(capsule);
            capsule.Analysis.ConversationHealth = AssessConversationHealthWithPatterns(capsule);
            capsule.Analysis.Decisions = TrackDecisionsWithPatterns(capsule);
            capsule.Analysis.ActionItems = IdentifyActionItemsWithPatterns(capsule);
            capsule.SuggestedActions = GenerateSuggestedActionsWithPatterns(capsule);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Combined analysis using XML patterns with options (no AI calls) - FREE
        /// </summary>
        private async Task AnalyzeConversationWithPatterns(ThreadCapsule capsule, AnalysisOptions options)
        {
            _logger.LogInformation("Running pattern-based analysis with options (no AI costs)");

            capsule.Analysis.UnansweredQuestions = options.EnableUnansweredQuestions
                ? DetectUnansweredQuestionsWithPatterns(capsule)
                : new List<UnansweredQuestion>();

            capsule.Analysis.TensionPoints = options.EnableTensionPoints
                ? IdentifyTensionPointsWithPatterns(capsule)
                : new List<TensionPoint>();

            capsule.Analysis.Misalignments = options.EnableMisalignments
                ? DetectMisalignmentsWithPatterns(capsule)
                : new List<Misalignment>();

            capsule.Analysis.ConversationHealth = options.EnableConversationHealth
                ? AssessConversationHealthWithPatterns(capsule)
                : null;

            capsule.SuggestedActions = options.EnableSuggestedActions
                ? GenerateSuggestedActionsWithPatterns(capsule)
                : new List<SuggestedActionItem>();

            capsule.Analysis.Decisions = new List<DecisionPoint>();
            capsule.Analysis.ActionItems = new List<ActionItem>();

            await Task.CompletedTask;
        }

        #endregion

        #region Pattern-Based Analysis Methods (Basic Mode - FREE)

        /// <summary>
        /// Detect unanswered questions using pattern matching
        /// Key insight: In a conversation, the NEXT response from a DIFFERENT person 
        /// after a question is typically the answer, unless it's another question.
        /// </summary>
        private List<UnansweredQuestion> DetectUnansweredQuestionsWithPatterns(ThreadCapsule capsule)
        {
            var unanswered = new List<UnansweredQuestion>();
            var orderedMessages = capsule.Messages.OrderBy(m => m.Timestamp).ToList();

            for (int i = 0; i < orderedMessages.Count; i++)
            {
                var message = orderedMessages[i];
                var questions = ExtractQuestionsFromText(message.Content);

                if (!questions.Any())
                    continue;

                var participant = capsule.Participants.FirstOrDefault(p => p.Id == message.ParticipantId);
                var askerName = participant?.Name ?? message.ParticipantId;

                foreach (var question in questions)
                {
                    var wasAnswered = false;

                    // Look at subsequent messages for an answer
                    for (int j = i + 1; j < orderedMessages.Count; j++)
                    {
                        var nextMessage = orderedMessages[j];
                        var nextParticipant = capsule.Participants.FirstOrDefault(p => p.Id == nextMessage.ParticipantId);
                        var responderName = nextParticipant?.Name ?? nextMessage.ParticipantId;

                        // Skip if same person (they might be adding more context, not answering)
                        if (responderName == askerName)
                            continue;

                        var responseContent = nextMessage.Content.Trim();

                        // Check if this response looks like an answer:
                        // 1. It's from a different person
                        // 2. It's not purely a question itself
                        // 3. It has some substance (not just "ok" or empty)

                        var isJustAQuestion = responseContent.EndsWith("?") &&
                                              !responseContent.Any(c => c == '.' || c == '!');
                        var hasSubstance = responseContent.Length > 10;

                        if (!isJustAQuestion && hasSubstance)
                        {
                            // This looks like an answer - different person gave a substantive non-question response
                            wasAnswered = true;
                            break;
                        }

                        // If the responder asked another question, the original might still be unanswered
                        // But if they gave ANY statement, consider it addressed
                        if (!isJustAQuestion)
                        {
                            wasAnswered = true;
                            break;
                        }
                    }

                    if (!wasAnswered)
                    {
                        // Check if this question was asked multiple times (indicates frustration)
                        var normalizedQ = NormalizeQuestion(question);
                        var timesAsked = orderedMessages
                            .Where(m =>
                            {
                                var p = capsule.Participants.FirstOrDefault(x => x.Id == m.ParticipantId);
                                return (p?.Name ?? m.ParticipantId) == askerName;
                            })
                            .Sum(m => ExtractQuestionsFromText(m.Content)
                                .Count(q => NormalizeQuestion(q) == normalizedQ));

                        unanswered.Add(new UnansweredQuestion
                        {
                            Question = question,
                            AskedBy = askerName,
                            AskedAt = message.Timestamp,
                            TimesAsked = Math.Max(1, timesAsked),
                            MessageId = message.Id
                        });
                    }
                }
            }

            // Deduplicate - same question text should only appear once
            return unanswered
                .GroupBy(q => NormalizeQuestion(q.Question))
                .Select(g => g.First())
                .ToList();
        }

        /// <summary>
        /// Identify tension points using XML pattern matching
        /// </summary>
        private List<TensionPoint> IdentifyTensionPointsWithPatterns(ThreadCapsule capsule)
        {
            var tensions = new List<TensionPoint>();

            foreach (var message in capsule.Messages)
            {
                var content = message.Content;
                var participant = capsule.Participants.FirstOrDefault(p => p.Id == message.ParticipantId);
                var senderName = participant?.Name ?? message.ParticipantId;

                // Check for frustration using XML patterns
                var frustrationMatches = _patterns.FindMatchingPatterns(content, "FrustrationIndicators");
                if (frustrationMatches.Any())
                {
                    var keywordLabel = frustrationMatches.Count == 1 ? "keyword" : "keywords";
                    tensions.Add(new TensionPoint
                    {
                        Type = "Frustration",
                        Description = $"{senderName} expressed frustration ({keywordLabel}: \"{string.Join("\", \"", frustrationMatches.Take(3))}\")",
                        Severity = content.Contains("!") ? "High" : "Medium",
                        Timestamp = message.Timestamp,
                        DetectedAt = DateTime.UtcNow,
                        MessageId = message.Id,
                        Participants = new List<string> { senderName }
                    });
                }

                // Check for urgency using XML patterns
                var urgencyMatches = _patterns.FindMatchingPatterns(content, "UrgencyIndicators");
                if (urgencyMatches.Any())
                {
                    var isHighUrgency = urgencyMatches.Any(m =>
                        m.Equals("asap", StringComparison.OrdinalIgnoreCase) ||
                        m.Equals("emergency", StringComparison.OrdinalIgnoreCase) ||
                        m.Equals("critical", StringComparison.OrdinalIgnoreCase));

                    var keywordLabel = urgencyMatches.Count == 1 ? "keyword" : "keywords";
                    tensions.Add(new TensionPoint
                    {
                        Type = "Urgency",
                        Description = $"{senderName} expressed urgency ({keywordLabel}: \"{string.Join("\", \"", urgencyMatches.Take(3))}\")",
                        Severity = isHighUrgency ? "High" : "Medium",
                        Timestamp = message.Timestamp,
                        DetectedAt = DateTime.UtcNow,
                        MessageId = message.Id,
                        Participants = new List<string> { senderName }
                    });
                }

                // Check for repetition using XML patterns
                var repetitionMatches = _patterns.FindMatchingPatterns(content, "RepetitionIndicators");
                if (repetitionMatches.Any())
                {
                    var isHighRepetition = repetitionMatches.Any(m =>
                        m.Contains("third", StringComparison.OrdinalIgnoreCase));

                    var keywordLabel = repetitionMatches.Count == 1 ? "keyword" : "keywords";
                    tensions.Add(new TensionPoint
                    {
                        Type = "RepeatedRequest",
                        Description = $"{senderName} is following up ({keywordLabel}: \"{string.Join("\", \"", repetitionMatches.Take(3))}\")",
                        Severity = isHighRepetition ? "High" : "Medium",
                        Timestamp = message.Timestamp,
                        DetectedAt = DateTime.UtcNow,
                        MessageId = message.Id,
                        Participants = new List<string> { senderName }
                    });
                }

                // Check for escalation using XML patterns
                if (_patterns.ContainsPattern(content, "EscalationIndicators"))
                {
                    var escalationMatches = _patterns.FindMatchingPatterns(content, "EscalationIndicators");
                    var keywordLabel = escalationMatches.Count == 1 ? "keyword" : "keywords";
                    tensions.Add(new TensionPoint
                    {
                        Type = "Escalation",
                        Description = $"{senderName} mentioned escalation ({keywordLabel}: \"{string.Join("\", \"", escalationMatches.Take(3))}\")",
                        Severity = "High",
                        Timestamp = message.Timestamp,
                        DetectedAt = DateTime.UtcNow,
                        MessageId = message.Id,
                        Participants = new List<string> { senderName }
                    });
                }

                // Check for dismissive language using XML patterns
                if (_patterns.ContainsPattern(content, "DismissiveIndicators"))
                {
                    var dismissiveMatches = _patterns.FindMatchingPatterns(content, "DismissiveIndicators");
                    var keywordLabel = dismissiveMatches.Count == 1 ? "keyword" : "keywords";
                    tensions.Add(new TensionPoint
                    {
                        Type = "Dismissive",
                        Description = $"{senderName} used dismissive language ({keywordLabel}: \"{string.Join("\", \"", dismissiveMatches.Take(3))}\")",
                        Severity = "Medium",
                        Timestamp = message.Timestamp,
                        DetectedAt = DateTime.UtcNow,
                        MessageId = message.Id,
                        Participants = new List<string> { senderName }
                    });
                }

                // Check for negative tone (but not if also positive) using XML patterns
                if (_patterns.ContainsPattern(content, "NegativeToneIndicators") &&
                    !_patterns.ContainsPattern(content, "PositiveIndicators"))
                {
                    var negativeMatches = _patterns.FindMatchingPatterns(content, "NegativeToneIndicators");
                    var keywordLabel = negativeMatches.Count == 1 ? "keyword" : "keywords";
                    tensions.Add(new TensionPoint
                    {
                        Type = "NegativeTone",
                        Description = $"{senderName} used strongly negative language ({keywordLabel}: \"{string.Join("\", \"", negativeMatches.Take(3))}\")",
                        Severity = "Medium",
                        Timestamp = message.Timestamp,
                        DetectedAt = DateTime.UtcNow,
                        MessageId = message.Id,
                        Participants = new List<string> { senderName }
                    });
                }

                // Check for multiple exclamation marks
                if (MultipleExclamationPattern.IsMatch(content))
                {
                    tensions.Add(new TensionPoint
                    {
                        Type = "EmotionalTone",
                        Description = $"{senderName} used emphatic punctuation (multiple exclamation marks)",
                        Severity = "Low",
                        Timestamp = message.Timestamp,
                        DetectedAt = DateTime.UtcNow,
                        MessageId = message.Id,
                        Participants = new List<string> { senderName }
                    });
                }
            }

            return tensions
                .GroupBy(t => new { t.MessageId, t.Type })
                .Select(g => g.First())
                .ToList();
        }

        /// <summary>
        /// Detect misalignments using XML pattern matching
        /// </summary>
        private List<Misalignment> DetectMisalignmentsWithPatterns(ThreadCapsule capsule)
        {
            var misalignments = new List<Misalignment>();

            foreach (var message in capsule.Messages)
            {
                var content = message.Content;
                var participant = capsule.Participants.FirstOrDefault(p => p.Id == message.ParticipantId);
                var senderName = participant?.Name ?? message.ParticipantId;

                // Check for disagreement using XML patterns
                if (_patterns.ContainsPattern(content, "DisagreementIndicators"))
                {
                    var matches = _patterns.FindMatchingPatterns(content, "DisagreementIndicators");
                    var keywordLabel = matches.Count == 1 ? "keyword" : "keywords";
                    misalignments.Add(new Misalignment
                    {
                        Type = "Disagreement",
                        Description = $"{senderName} expressed disagreement ({keywordLabel}: \"{string.Join("\", \"", matches.Take(2))}\")",
                        Severity = "Medium",
                        ParticipantsInvolved = new List<string> { senderName },
                        SuggestedResolution = "Clarify the different perspectives and find common ground"
                    });
                }

                // Check for confusion using XML patterns
                if (_patterns.ContainsPattern(content, "ConfusionIndicators"))
                {
                    var matches = _patterns.FindMatchingPatterns(content, "ConfusionIndicators");
                    var keywordLabel = matches.Count == 1 ? "keyword" : "keywords";
                    misalignments.Add(new Misalignment
                    {
                        Type = "Confusion",
                        Description = $"{senderName} expressed confusion ({keywordLabel}: \"{string.Join("\", \"", matches.Take(2))}\")",
                        Severity = "Low",
                        ParticipantsInvolved = new List<string> { senderName },
                        SuggestedResolution = "Provide clearer explanation or additional context"
                    });
                }

                // Check for assumption mismatch using XML patterns
                if (_patterns.ContainsPattern(content, "AssumptionIndicators"))
                {
                    var matches = _patterns.FindMatchingPatterns(content, "AssumptionIndicators");
                    var keywordLabel = matches.Count == 1 ? "keyword" : "keywords";
                    misalignments.Add(new Misalignment
                    {
                        Type = "Assumption",
                        Description = $"{senderName} had a different assumption ({keywordLabel}: \"{string.Join("\", \"", matches.Take(2))}\")",
                        Severity = "Medium",
                        ParticipantsInvolved = new List<string> { senderName },
                        SuggestedResolution = "Explicitly confirm shared understanding of key points"
                    });
                }
            }

            return misalignments;
        }

        /// <summary>
        /// Assess conversation health using pattern-based heuristics
        /// </summary>
        private ConversationHealth AssessConversationHealthWithPatterns(ThreadCapsule capsule)
        {
            var health = new ConversationHealth
            {
                Issues = new List<string?>(),
                Strengths = new List<string?>(),
                Recommendations = new List<string?>()
            };

            var totalMessages = capsule.Messages.Count;
            var questionsAsked = capsule.Messages.Sum(m => ExtractQuestionsFromText(m.Content).Count);
            var unansweredCount = capsule.Analysis?.UnansweredQuestions?.Count ?? 0;
            var tensionCount = capsule.Analysis?.TensionPoints?.Count ?? 0;
            var misalignmentCount = capsule.Analysis?.Misalignments?.Count ?? 0;

            double responsivenessScore = questionsAsked > 0
                ? Math.Max(0, 1.0 - ((double)unansweredCount / questionsAsked))
                : 1.0;

            // Count using XML patterns
            var positiveCount = capsule.Messages.Count(m => _patterns.ContainsPattern(m.Content, "PositiveIndicators"));
            var negativeCount = capsule.Messages.Count(m =>
                _patterns.ContainsPattern(m.Content, "FrustrationIndicators") ||
                _patterns.ContainsPattern(m.Content, "NegativeToneIndicators"));

            var confusionCount = capsule.Messages.Count(m => _patterns.ContainsPattern(m.Content, "ConfusionIndicators"));

            double clarityScore = totalMessages > 0
                ? Math.Max(0, 1.0 - ((double)confusionCount / totalMessages * 2))
                : 0.5;

            double alignmentScore = totalMessages > 0
                ? Math.Max(0, 1.0 - ((double)misalignmentCount / totalMessages * 2))
                : 0.5;

            double healthScore =
                (responsivenessScore * 0.35) +
                (clarityScore * 0.25) +
                (alignmentScore * 0.25) +
                (positiveCount > negativeCount ? 0.15 : (positiveCount == negativeCount ? 0.075 : 0));

            healthScore = Math.Max(0, healthScore - (tensionCount * 0.05));

            health.HealthScore = healthScore;
            health.ResponsivenessScore = responsivenessScore;
            health.ClarityScore = clarityScore;
            health.AlignmentScore = alignmentScore;

            if (healthScore < 0.4 || tensionCount >= 3)
                health.RiskLevel = "High";
            else if (healthScore < 0.7 || tensionCount >= 1)
                health.RiskLevel = "Medium";
            else
                health.RiskLevel = "Low";

            if (unansweredCount > 0)
                health.Issues.Add($"{unansweredCount} unanswered question(s)");
            if (tensionCount > 0)
                health.Issues.Add($"{tensionCount} tension point(s) detected");
            if (misalignmentCount > 0)
                health.Issues.Add($"{misalignmentCount} potential misalignment(s)");

            if (positiveCount > 0)
                health.Strengths.Add("Positive tone detected in conversation");
            if (unansweredCount == 0 && questionsAsked > 0)
                health.Strengths.Add("All questions have been addressed");
            if (tensionCount == 0)
                health.Strengths.Add("No significant tension detected");

            if (unansweredCount > 0)
                health.Recommendations.Add("Address the unanswered questions");
            if (tensionCount > 0)
                health.Recommendations.Add("Consider addressing the tension points directly");

            return health;
        }

        /// <summary>
        /// Track decisions using XML pattern matching
        /// </summary>
        private List<DecisionPoint> TrackDecisionsWithPatterns(ThreadCapsule capsule)
        {
            var decisions = new List<DecisionPoint>();

            foreach (var message in capsule.Messages)
            {
                if (_patterns.ContainsPattern(message.Content, "DecisionIndicators"))
                {
                    var participant = capsule.Participants.FirstOrDefault(p => p.Id == message.ParticipantId);
                    var matches = _patterns.FindMatchingPatterns(message.Content, "DecisionIndicators");

                    decisions.Add(new DecisionPoint
                    {
                        Decision = GetSentenceContainingPattern(message.Content, matches.FirstOrDefault() ?? ""),
                        DecidedBy = participant?.Name ?? message.ParticipantId,
                        Timestamp = message.Timestamp,
                        MessageId = message.Id
                    });
                }
            }

            return decisions;
        }

        /// <summary>
        /// Identify action items using XML pattern matching
        /// </summary>
        private List<ActionItem> IdentifyActionItemsWithPatterns(ThreadCapsule capsule)
        {
            var actionItems = new List<ActionItem>();

            foreach (var message in capsule.Messages)
            {
                var participant = capsule.Participants.FirstOrDefault(p => p.Id == message.ParticipantId);
                var senderName = participant?.Name ?? message.ParticipantId;

                if (_patterns.ContainsPattern(message.Content, "ActionRequestIndicators"))
                {
                    var matches = _patterns.FindMatchingPatterns(message.Content, "ActionRequestIndicators");
                    var isUrgent = _patterns.ContainsPattern(message.Content, "UrgencyIndicators");

                    actionItems.Add(new ActionItem
                    {
                        Action = GetSentenceContainingPattern(message.Content, matches.FirstOrDefault() ?? ""),
                        RequestedBy = senderName,
                        AssignedTo = "Unassigned",
                        Timestamp = message.Timestamp,
                        MessageId = message.Id,
                        Priority = isUrgent ? "High" : "Medium",
                        Status = "Pending"
                    });
                }

                if (_patterns.ContainsPattern(message.Content, "CommitmentIndicators"))
                {
                    var matches = _patterns.FindMatchingPatterns(message.Content, "CommitmentIndicators");

                    actionItems.Add(new ActionItem
                    {
                        Action = GetSentenceContainingPattern(message.Content, matches.FirstOrDefault() ?? ""),
                        RequestedBy = "Self-assigned",
                        AssignedTo = senderName,
                        Timestamp = message.Timestamp,
                        MessageId = message.Id,
                        Priority = "Medium",
                        Status = "Committed"
                    });
                }
            }

            return actionItems;
        }

        /// <summary>
        /// Generate suggested actions based on pattern analysis results
        /// </summary>
        private List<SuggestedActionItem> GenerateSuggestedActionsWithPatterns(ThreadCapsule capsule)
        {
            var suggestions = new List<SuggestedActionItem>();

            var unanswered = capsule.Analysis?.UnansweredQuestions ?? new List<UnansweredQuestion>();
            foreach (var question in unanswered.Take(3))
            {
                suggestions.Add(new SuggestedActionItem
                {
                    Action = $"Respond to {question.AskedBy}'s question: \"{TruncateText(question.Question, 50)}\"",
                    Priority = question.TimesAsked > 1 ? "High" : "Medium",
                    Reasoning = question.TimesAsked > 1
                        ? $"This question has been asked {question.TimesAsked} times"
                        : "This question remains unanswered"
                });
            }

            var tensions = capsule.Analysis?.TensionPoints ?? new List<TensionPoint>();
            foreach (var tension in tensions.Where(t => t.Severity == "High").Take(2))
            {
                suggestions.Add(new SuggestedActionItem
                {
                    Action = $"Address the {tension.Type.ToLower()} concern",
                    Priority = "High",
                    Reasoning = tension.Description
                });
            }

            var misalignments = capsule.Analysis?.Misalignments ?? new List<Misalignment>();
            foreach (var misalignment in misalignments.Take(2))
            {
                suggestions.Add(new SuggestedActionItem
                {
                    Action = misalignment.SuggestedResolution ?? "Clarify the misunderstanding",
                    Priority = misalignment.Severity == "High" ? "High" : "Medium",
                    Reasoning = misalignment.Description
                });
            }

            return suggestions.Take(5).ToList();
        }

        #endregion

        #region Helper Methods

        private List<string> ExtractQuestionsFromText(string text)
        {
            var questions = new List<string>();

            var directMatches = QuestionPattern.Matches(text);
            foreach (Match match in directMatches)
            {
                var q = match.Value.Trim();
                if (q.Length > 5)
                    questions.Add(q);
            }

            var sentences = text.Split(new[] { '.', '!', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var sentence in sentences)
            {
                var trimmed = sentence.Trim();
                if (_patterns.StartsWithQuestionWord(trimmed) && !trimmed.EndsWith("?"))
                {
                    if (trimmed.Length > 10)
                        questions.Add(trimmed + "?");
                }
            }

            return questions.Distinct().ToList();
        }

        private string NormalizeQuestion(string question)
        {
            return Regex.Replace(question.ToLower().Trim(), @"[^\w\s]", "").Trim();
        }

        private List<string> ExtractKeywords(string text)
        {
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
                "have", "has", "had", "do", "does", "did", "will", "would", "could",
                "should", "may", "might", "must", "can", "this", "that", "these",
                "those", "i", "you", "he", "she", "it", "we", "they", "what", "when",
                "where", "who", "why", "how", "which", "there", "here", "and", "or",
                "but", "if", "then", "so", "as", "of", "for", "to", "from", "in", "on",
                "at", "by", "with", "about", "your", "my", "our", "their", "its"
            };

            return Regex.Split(text.ToLower(), @"\W+")
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .Distinct()
                .ToList();
        }

        private string GetSentenceContainingPattern(string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return text.Length > 100 ? text.Substring(0, 100) + "..." : text;

            var index = text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return text.Length > 100 ? text.Substring(0, 100) + "..." : text;

            var sentences = Regex.Split(text, @"(?<=[.!?])\s+");
            var currentPos = 0;

            foreach (var sentence in sentences)
            {
                if (currentPos + sentence.Length >= index)
                    return sentence.Trim();
                currentPos += sentence.Length + 1;
            }

            var start = Math.Max(0, index - 30);
            var end = Math.Min(text.Length, index + pattern.Length + 50);
            return text.Substring(start, end - start).Trim();
        }

        private string TruncateText(string text, int maxLength)
        {
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        #endregion

        #region Combined Analysis - AI (User Permissions)

        private async Task AnalyzeConversationCombined(ThreadCapsule capsule, AnalysisOptions options)
        {
            var prompt = BuildCombinedPrompt(capsule, options);
            var response = await _aiService.GenerateResponseAsync(prompt);
            ParseCombinedResponse(capsule, response, options);
        }

        private string BuildCombinedPrompt(ThreadCapsule capsule, AnalysisOptions options)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Analyze the following conversation and provide a JSON response.");
            sb.AppendLine();
            sb.AppendLine("CONVERSATION:");
            sb.AppendLine(capsule.RawText);
            sb.AppendLine();
            sb.AppendLine("Provide JSON with these sections (only include requested ones):");
            sb.AppendLine("{");

            var sections = new List<string>();

            if (options.EnableUnansweredQuestions)
                sections.Add(@"  ""unansweredQuestions"": [{ ""question"": ""text"", ""askedBy"": ""name"" }]");
            if (options.EnableTensionPoints)
                sections.Add(@"  ""tensionPoints"": [{ ""description"": ""text"", ""severity"": ""Low|Medium|High"", ""participants"": [""name""] }]");
            if (options.EnableMisalignments)
                sections.Add(@"  ""misalignments"": [{ ""topic"": ""text"", ""severity"": ""Low|Medium|High"", ""participantsInvolved"": [""name""] }]");
            if (options.EnableConversationHealth)
                sections.Add(@"  ""conversationHealth"": { ""overallScore"": 75, ""riskLevel"": ""Low|Medium|High"", ""issues"": [], ""strengths"": [] }");
            if (options.EnableSuggestedActions)
                sections.Add(@"  ""suggestedActions"": [{ ""action"": ""text"", ""priority"": ""Low|Medium|High"" }]");

            sb.AppendLine(string.Join(",\n", sections));
            sb.AppendLine("}");
            sb.AppendLine("Return ONLY valid JSON.");

            return sb.ToString();
        }

        private void ParseCombinedResponse(ThreadCapsule capsule, string response, AnalysisOptions options)
        {
            try
            {
                var cleanResponse = JsonHelper.CleanJsonResponse(response);
                using var doc = JsonDocument.Parse(cleanResponse);
                var root = doc.RootElement;

                capsule.Analysis.UnansweredQuestions = options.EnableUnansweredQuestions && root.TryGetProperty("unansweredQuestions", out var uq)
                    ? ParseUnansweredQuestionsCombined(uq) : new List<UnansweredQuestion>();

                capsule.Analysis.TensionPoints = options.EnableTensionPoints && root.TryGetProperty("tensionPoints", out var tp)
                    ? ParseTensionPointsCombined(tp) : new List<TensionPoint>();

                capsule.Analysis.Misalignments = options.EnableMisalignments && root.TryGetProperty("misalignments", out var ma)
                    ? ParseMisalignmentsCombined(ma) : new List<Misalignment>();

                capsule.Analysis.ConversationHealth = options.EnableConversationHealth && root.TryGetProperty("conversationHealth", out var ch)
                    ? ParseConversationHealthCombined(ch) : null;

                capsule.SuggestedActions = options.EnableSuggestedActions && root.TryGetProperty("suggestedActions", out var sa)
                    ? ParseSuggestedActionsCombined(sa) : new List<SuggestedActionItem>();

                capsule.Analysis.Decisions = new List<DecisionPoint>();
                capsule.Analysis.ActionItems = new List<ActionItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing combined response");
                capsule.Analysis.UnansweredQuestions = new List<UnansweredQuestion>();
                capsule.Analysis.TensionPoints = new List<TensionPoint>();
                capsule.Analysis.Misalignments = new List<Misalignment>();
                capsule.Analysis.ConversationHealth = null;
                capsule.SuggestedActions = new List<SuggestedActionItem>();
                capsule.Analysis.Decisions = new List<DecisionPoint>();
                capsule.Analysis.ActionItems = new List<ActionItem>();
            }
        }

        #endregion

        #region Combined Response Parsers

        private List<UnansweredQuestion> ParseUnansweredQuestionsCombined(JsonElement element)
        {
            var result = new List<UnansweredQuestion>();
            foreach (var item in element.EnumerateArray())
            {
                result.Add(new UnansweredQuestion
                {
                    Question = item.TryGetProperty("question", out var q) ? q.GetString() ?? "" : "",
                    AskedBy = item.TryGetProperty("askedBy", out var a) ? a.GetString() ?? "" : "",
                    AskedAt = DateTime.UtcNow,
                    TimesAsked = 1
                });
            }
            return result;
        }

        private List<TensionPoint> ParseTensionPointsCombined(JsonElement element)
        {
            var result = new List<TensionPoint>();
            foreach (var item in element.EnumerateArray())
            {
                var tp = new TensionPoint
                {
                    Description = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                    Severity = item.TryGetProperty("severity", out var s) ? s.GetString() ?? "Low" : "Low",
                    Type = "Conflict",
                    Timestamp = DateTime.UtcNow,
                    DetectedAt = DateTime.UtcNow
                };
                if (item.TryGetProperty("participants", out var p))
                    tp.Participants = p.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
                result.Add(tp);
            }
            return result;
        }

        private List<Misalignment> ParseMisalignmentsCombined(JsonElement element)
        {
            var result = new List<Misalignment>();
            foreach (var item in element.EnumerateArray())
            {
                var ma = new Misalignment
                {
                    Type = item.TryGetProperty("topic", out var t) ? t.GetString() ?? "" : "",
                    Severity = item.TryGetProperty("severity", out var s) ? s.GetString() ?? "Low" : "Low"
                };
                if (item.TryGetProperty("participantsInvolved", out var p))
                    ma.ParticipantsInvolved = p.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
                result.Add(ma);
            }
            return result;
        }

        private ConversationHealth ParseConversationHealthCombined(JsonElement element)
        {
            return new ConversationHealth
            {
                HealthScore = element.TryGetProperty("overallScore", out var os) ? os.GetInt32() / 100.0 : 0.5,
                RiskLevel = element.TryGetProperty("riskLevel", out var rl) ? rl.GetString() ?? "Low" : "Low",
                Issues = element.TryGetProperty("issues", out var i) ? i.EnumerateArray().Select(x => x.GetString()).ToList() : new List<string?>(),
                Strengths = element.TryGetProperty("strengths", out var st) ? st.EnumerateArray().Select(x => x.GetString()).ToList() : new List<string?>(),
                Recommendations = new List<string?>()
            };
        }

        private List<SuggestedActionItem> ParseSuggestedActionsCombined(JsonElement element)
        {
            var result = new List<SuggestedActionItem>();
            foreach (var item in element.EnumerateArray())
            {
                result.Add(new SuggestedActionItem
                {
                    Action = item.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "",
                    Priority = item.TryGetProperty("priority", out var p) ? p.GetString() : "Medium"
                });
            }
            return result;
        }

        #endregion

        #region AI-Powered Analysis Methods

        public async Task<List<UnansweredQuestion>> DetectUnansweredQuestions(ThreadCapsule capsule)
        {
            var prompt = BuildUnansweredQuestionsPrompt(capsule);
            var result = await _aiService.AnalyzeConversation(prompt, capsule);
            return ParseUnansweredQuestionsResponse(result, capsule);
        }

        public async Task<List<TensionPoint>> IdentifyTensionPoints(ThreadCapsule capsule)
        {
            var prompt = BuildTensionPointsPrompt(capsule);
            var result = await _aiService.AnalyzeConversation(prompt, capsule);
            return ParseTensionPointsResponse(result, capsule);
        }

        public async Task<List<Misalignment>> DetectMisalignments(ThreadCapsule capsule)
        {
            var prompt = BuildMisalignmentsPrompt(capsule);
            var result = await _aiService.AnalyzeConversation(prompt, capsule);
            return ParseMisalignmentsResponse(result, capsule);
        }

        public async Task<List<DecisionPoint>> TrackDecisions(ThreadCapsule capsule)
        {
            var prompt = BuildDecisionsPrompt(capsule);
            var result = await _aiService.AnalyzeConversation(prompt, capsule);
            return ParseDecisionsResponse(result, capsule);
        }

        public async Task<List<ActionItem>> IdentifyActionItems(ThreadCapsule capsule)
        {
            var prompt = BuildActionItemsPrompt(capsule);
            var result = await _aiService.AnalyzeConversation(prompt, capsule);
            return ParseActionItemsResponse(result, capsule);
        }

        public async Task<ConversationHealth> AssessConversationHealth(ThreadCapsule capsule)
        {
            var prompt = BuildHealthAssessmentPrompt(capsule);
            var result = await _aiService.AnalyzeConversation(prompt, capsule);
            return ParseHealthAssessmentResponse(result);
        }

        public async Task<DraftAnalysis> AnalyzeDraft(ThreadCapsule capsule, string draftMessage)
        {
            var prompt = BuildDraftAnalysisPrompt(capsule, draftMessage);
            var result = await _aiService.AnalyzeConversation(prompt, capsule);
            return ParseDraftAnalysisResponse(result);
        }

        #endregion

        #region AI Prompt Builders

        private string BuildUnansweredQuestionsPrompt(ThreadCapsule capsule)
        {
            return $@"Analyze this conversation and identify unanswered questions.
Return JSON: {{ ""unansweredQuestions"": [{{ ""question"": ""text"", ""askedBy"": ""name"", ""askedAt"": ""ISO8601"", ""timesAsked"": 1, ""messageId"": ""id"" }}] }}

{FormatConversationForAI(capsule)}";
        }

        private string BuildTensionPointsPrompt(ThreadCapsule capsule)
        {
            return $@"Analyze this conversation for tension, frustration, or communication breakdown.
Return JSON: {{ ""tensionPoints"": [{{ ""type"": ""string"", ""severity"": ""Low|Medium|High"", ""description"": ""text"", ""messageId"": ""id"", ""timestamp"": ""ISO8601"" }}] }}

{FormatConversationForAI(capsule)}";
        }

        private string BuildMisalignmentsPrompt(ThreadCapsule capsule)
        {
            return $@"Identify misalignments where participants have different expectations or understandings.
Return JSON: {{ ""misalignments"": [{{ ""type"": ""string"", ""severity"": ""Low|Medium|High"", ""description"": ""text"", ""participantsInvolved"": [""name""], ""suggestedResolution"": ""text"" }}] }}

{FormatConversationForAI(capsule)}";
        }

        private string BuildDecisionsPrompt(ThreadCapsule capsule)
        {
            return $@"Extract all decisions made in this conversation.
Return JSON: {{ ""decisions"": [{{ ""decisionText"": ""text"", ""decidedBy"": ""name"", ""timestamp"": ""ISO8601"", ""messageId"": ""id"" }}] }}

{FormatConversationForAI(capsule)}";
        }

        private string BuildActionItemsPrompt(ThreadCapsule capsule)
        {
            return $@"Identify all action items from this conversation.
Return JSON: {{ ""actionItems"": [{{ ""action"": ""text"", ""assignedTo"": ""name"", ""requestedBy"": ""name"", ""timestamp"": ""ISO8601"", ""messageId"": ""id"", ""priority"": ""Low|Medium|High"", ""status"": ""Pending"" }}] }}

{FormatConversationForAI(capsule)}";
        }

        private string BuildHealthAssessmentPrompt(ThreadCapsule capsule)
        {
            return $@"Assess the overall health of this conversation (scores 0-100).
Return JSON: {{ ""overallScore"": 75, ""clarityScore"": 80, ""responsivenessScore"": 70, ""alignmentScore"": 75, ""riskLevel"": ""Low|Medium|High"", ""issues"": [""text""], ""strengths"": [""text""], ""recommendations"": [""text""] }}

{FormatConversationForAI(capsule)}";
        }

        private string BuildDraftAnalysisPrompt(ThreadCapsule capsule, string draftMessage)
        {
            var unansweredQuestions = capsule.Analysis?.UnansweredQuestions ?? new List<UnansweredQuestion>();
            var questionsSection = unansweredQuestions.Any()
                ? "UNANSWERED QUESTIONS:\n" + string.Join("\n", unansweredQuestions.Select(q => $"- {q.Question}"))
                : "";

            return $@"Analyze this draft reply in context of the conversation.

{FormatConversationForAI(capsule)}

{questionsSection}

DRAFT REPLY:
{draftMessage}

Return JSON: {{
  ""tone"": {{ ""tone"": ""friendly|neutral|formal|defensive"", ""escalationRisk"": ""none|low|medium|high"" }},
  ""questionsCovered"": [{{ ""question"": ""text"", ""addressed"": true }}],
  ""riskFlags"": [{{ ""type"": ""text"", ""description"": ""text"", ""severity"": ""low|medium|high"" }}],
  ""completenessScore"": 7,
  ""suggestions"": [""text""],
  ""overallAssessment"": ""text"",
  ""readyToSend"": true
}}";
        }

        private string FormatConversationForAI(ThreadCapsule capsule)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Thread: {capsule.ThreadMetadata.Subject}");
            sb.AppendLine($"Participants: {string.Join(", ", capsule.Participants.Select(p => p.Name))}");
            sb.AppendLine("Messages:");

            foreach (var message in capsule.Messages.OrderBy(m => m.Timestamp))
            {
                var participant = capsule.Participants.FirstOrDefault(p => p.Id == message.ParticipantId);
                sb.AppendLine($"[{message.Timestamp:yyyy-MM-dd HH:mm}] {participant?.Name ?? message.ParticipantId}: {message.Content}");
            }

            return sb.ToString();
        }

        #endregion

        #region AI Response Parsers

        private List<UnansweredQuestion> ParseUnansweredQuestionsResponse(string response, ThreadCapsule capsule)
        {
            try
            {
                var cleanJson = JsonHelper.CleanJsonResponse(response);
                using var doc = JsonDocument.Parse(cleanJson);
                var questions = new List<UnansweredQuestion>();

                if (doc.RootElement.TryGetProperty("unansweredQuestions", out var arr))
                {
                    foreach (var q in arr.EnumerateArray())
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
                _logger.LogError(ex, "Error parsing unanswered questions");
                return new List<UnansweredQuestion>();
            }
        }

        private List<TensionPoint> ParseTensionPointsResponse(string response, ThreadCapsule capsule)
        {
            try
            {
                var cleanJson = JsonHelper.CleanJsonResponse(response);
                using var doc = JsonDocument.Parse(cleanJson);
                var tensions = new List<TensionPoint>();

                if (doc.RootElement.TryGetProperty("tensionPoints", out var arr))
                {
                    foreach (var t in arr.EnumerateArray())
                    {
                        tensions.Add(new TensionPoint
                        {
                            Type = t.GetStringSafe("type", "Conflict"),
                            Description = t.GetStringSafe("description"),
                            Severity = t.GetStringSafe("severity", "Medium"),
                            Timestamp = t.GetDateTimeSafe("timestamp"),
                            MessageId = t.GetStringSafe("messageId", null),
                            Participants = t.ParseStringArray("participants")
                        });
                    }
                }
                return tensions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing tension points");
                return new List<TensionPoint>();
            }
        }

        private List<Misalignment> ParseMisalignmentsResponse(string response, ThreadCapsule capsule)
        {
            try
            {
                var cleanJson = JsonHelper.CleanJsonResponse(response);
                using var doc = JsonDocument.Parse(cleanJson);
                var misalignments = new List<Misalignment>();

                if (doc.RootElement.TryGetProperty("misalignments", out var arr))
                {
                    foreach (var m in arr.EnumerateArray())
                    {
                        misalignments.Add(new Misalignment
                        {
                            Type = m.GetStringSafe("type"),
                            Description = m.GetStringSafe("description"),
                            ParticipantsInvolved = m.ParseStringArray("participantsInvolved"),
                            Severity = m.GetStringSafe("severity", "Medium"),
                            SuggestedResolution = m.GetStringSafe("suggestedResolution")
                        });
                    }
                }
                return misalignments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing misalignments");
                return new List<Misalignment>();
            }
        }

        private List<DecisionPoint> ParseDecisionsResponse(string response, ThreadCapsule capsule)
        {
            try
            {
                var cleanJson = JsonHelper.CleanJsonResponse(response);
                using var doc = JsonDocument.Parse(cleanJson);
                var decisions = new List<DecisionPoint>();

                if (doc.RootElement.TryGetProperty("decisions", out var arr))
                {
                    foreach (var d in arr.EnumerateArray())
                    {
                        decisions.Add(new DecisionPoint
                        {
                            Decision = d.GetStringSafe("decisionText"),
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
                _logger.LogError(ex, "Error parsing decisions");
                return new List<DecisionPoint>();
            }
        }

        private List<ActionItem> ParseActionItemsResponse(string response, ThreadCapsule capsule)
        {
            try
            {
                var cleanJson = JsonHelper.CleanJsonResponse(response);
                using var doc = JsonDocument.Parse(cleanJson);
                var actionItems = new List<ActionItem>();

                if (doc.RootElement.TryGetProperty("actionItems", out var arr))
                {
                    foreach (var a in arr.EnumerateArray())
                    {
                        actionItems.Add(new ActionItem
                        {
                            Action = a.GetStringSafe("action"),
                            AssignedTo = a.GetStringSafe("assignedTo"),
                            RequestedBy = a.GetStringSafe("requestedBy"),
                            Timestamp = a.GetDateTimeSafe("timestamp"),
                            MessageId = a.GetStringSafe("messageId"),
                            Priority = a.GetStringSafe("priority", "Medium"),
                            Status = "Pending"
                        });
                    }
                }
                return actionItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing action items");
                return new List<ActionItem>();
            }
        }

        private ConversationHealth ParseHealthAssessmentResponse(string response)
        {
            try
            {
                var cleanJson = JsonHelper.CleanJsonResponse(response);
                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                return new ConversationHealth
                {
                    HealthScore = root.TryGetProperty("overallScore", out var os) ? os.GetDouble() / 100.0 : 0.5,
                    ClarityScore = root.TryGetProperty("clarityScore", out var cs) ? cs.GetDouble() / 100.0 : 0.5,
                    ResponsivenessScore = root.TryGetProperty("responsivenessScore", out var rs) ? rs.GetDouble() / 100.0 : 0.5,
                    AlignmentScore = root.TryGetProperty("alignmentScore", out var als) ? als.GetDouble() / 100.0 : 0.5,
                    RiskLevel = root.GetStringSafe("riskLevel", "Medium"),
                    Issues = root.ParseStringArray("issues").Cast<string?>().ToList(),
                    Strengths = root.ParseStringArray("strengths").Cast<string?>().ToList(),
                    Recommendations = root.ParseStringArray("recommendations").Cast<string?>().ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing health assessment");
                return new ConversationHealth { HealthScore = 0.5, RiskLevel = "Unknown" };
            }
        }

        private DraftAnalysis ParseDraftAnalysisResponse(string response)
        {
            try
            {
                var cleanJson = JsonHelper.CleanJsonResponse(response);
                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                var analysis = new DraftAnalysis();

                if (root.TryGetProperty("tone", out var tone))
                {
                    analysis.Tone = new ToneAssessment
                    {
                        Tone = tone.GetStringSafe("tone"),
                        EscalationRisk = tone.GetStringSafe("escalationRisk", "none")
                    };
                }

                if (root.TryGetProperty("questionsCovered", out var qc))
                {
                    foreach (var q in qc.EnumerateArray())
                    {
                        analysis.QuestionsCovered.Add(new QuestionCoverage
                        {
                            Question = q.GetStringSafe("question"),
                            Addressed = q.GetBoolSafe("addressed", false)
                        });
                    }
                }

                if (root.TryGetProperty("riskFlags", out var rf))
                {
                    foreach (var r in rf.EnumerateArray())
                    {
                        analysis.RiskFlags.Add(new RiskFlag
                        {
                            Type = r.GetStringSafe("type"),
                            Description = r.GetStringSafe("description"),
                            Severity = r.GetStringSafe("severity", "low")
                        });
                    }
                }

                analysis.CompletenessScore = root.GetInt32Safe("completenessScore", 5);
                analysis.OverallAssessment = root.GetStringSafe("overallAssessment");
                analysis.ReadyToSend = root.GetBoolSafe("readyToSend", false);

                if (root.TryGetProperty("suggestions", out var sug))
                {
                    foreach (var s in sug.EnumerateArray())
                        analysis.Suggestions.Add(s.GetString() ?? "");
                }

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing draft analysis");
                return new DraftAnalysis { OverallAssessment = "Unable to analyze draft", ReadyToSend = false };
            }
        }

        #endregion
    }
}
