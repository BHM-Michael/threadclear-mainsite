using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Implementations
{
    public class InsightTransformer
    {
        private readonly TaxonomyData _taxonomy;

        public InsightTransformer(TaxonomyData taxonomy)
        {
            _taxonomy = taxonomy;
        }

        public StorableInsight Transform(ThreadCapsule capsule, Guid organizationId, Guid? userId = null)
        {
            var insight = new StorableInsight
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                Timestamp = capsule.CreatedAt,
                SourceType = capsule.SourceType ?? "unknown",
                ParticipantCount = capsule.Participants?.Count ?? 0,
                MessageCount = capsule.Messages?.Count ?? 0,
                OverallRisk = capsule.Analysis?.ConversationHealth?.RiskLevel ?? "Low",
                HealthScore = (int)(capsule.Analysis?.ConversationHealth?.HealthScore ?? 0)
            };

            if (capsule.Analysis == null) return insight;

            var insights = new List<InsightEntry>();

            // Transform Unanswered Questions
            if (capsule.Analysis.UnansweredQuestions != null)
            {
                foreach (var uq in capsule.Analysis.UnansweredQuestions)
                {
                    var topic = InferTopic(uq.Question);
                    var role = InferRole(uq.AskedBy, capsule);

                    insights.Add(new InsightEntry
                    {
                        Category = "QUESTION_STATUS",
                        Value = uq.TimesAsked > 1 ? "repeated_unanswered" : "unanswered",
                        Role = role,
                        Topic = topic,
                        Severity = DetermineSeverity("QUESTION_STATUS",
                            uq.TimesAsked > 1 ? "repeated_unanswered" : "unanswered",
                            topic, (int)uq.DaysUnanswered, uq.TimesAsked)
                    });
                }
            }

            // Transform Tension Points
            if (capsule.Analysis.TensionPoints != null)
            {
                foreach (var tp in capsule.Analysis.TensionPoints)
                {
                    var tensionValue = MapTensionType(tp.Type);
                    var topic = InferTopic(tp.Description);

                    insights.Add(new InsightEntry
                    {
                        Category = "TENSION_SIGNAL",
                        Value = tensionValue,
                        Role = "unknown",
                        Topic = topic,
                        Severity = tp.Severity?.ToLower() ?? "medium"
                    });
                }
            }

            // Transform Misalignments
            if (capsule.Analysis.Misalignments != null)
            {
                foreach (var ma in capsule.Analysis.Misalignments)
                {
                    var topic = InferTopic(ma.Description ?? ma.Type);

                    insights.Add(new InsightEntry
                    {
                        Category = "MISALIGNMENT",
                        Value = "detected",
                        Role = "multiple_parties",
                        Topic = topic,
                        Severity = ma.Severity?.ToLower() ?? "medium"
                    });
                }
            }

            // Transform Decisions
            if (capsule.Analysis.Decisions != null)
            {
                foreach (var d in capsule.Analysis.Decisions)
                {
                    var topic = InferTopic(d.Decision);
                    var role = InferRole(d.DecidedBy, capsule);

                    insights.Add(new InsightEntry
                    {
                        Category = "DECISION",
                        Value = "made",
                        Role = role,
                        Topic = topic,
                        Severity = "low"
                    });
                }
            }

            // Transform Action Items
            if (capsule.Analysis.ActionItems != null)
            {
                foreach (var ai in capsule.Analysis.ActionItems)
                {
                    var topic = InferTopic(ai.Action);
                    var role = InferRole(ai.AssignedTo, capsule);

                    var value = "assigned";
                    var severity = ai.Priority?.ToLower() == "high" ? "high" : "low";

                    // Check status for overdue
                    if (ai.Status?.ToLower() == "overdue")
                    {
                        value = "overdue";
                        severity = "high";
                    }

                    insights.Add(new InsightEntry
                    {
                        Category = "ACTION_ITEM",
                        Value = value,
                        Role = role,
                        Topic = topic,
                        Severity = severity
                    });
                }
            }

            // Add health-derived insights
            var health = capsule.Analysis.ConversationHealth;
            if (health != null)
            {
                if (health.ResponsivenessScore < 0.5)
                {
                    insights.Add(new InsightEntry
                    {
                        Category = "RESPONSE_PATTERN",
                        Value = "low_responsiveness",
                        Role = "unknown",
                        Topic = "general",
                        Severity = health.ResponsivenessScore < 0.3 ? "high" : "medium"
                    });
                }

                if (health.ClarityScore < 0.5)
                {
                    insights.Add(new InsightEntry
                    {
                        Category = "RESPONSE_PATTERN",
                        Value = "low_clarity",
                        Role = "unknown",
                        Topic = "general",
                        Severity = health.ClarityScore < 0.3 ? "high" : "medium"
                    });
                }

                if (health.AlignmentScore < 0.5)
                {
                    insights.Add(new InsightEntry
                    {
                        Category = "MISALIGNMENT",
                        Value = "low_alignment_score",
                        Role = "multiple_parties",
                        Topic = "general",
                        Severity = health.AlignmentScore < 0.3 ? "high" : "medium"
                    });
                }
            }

            insight.Insights = insights;
            return insight;
        }

        private string MapTensionType(string? tensionType)
        {
            if (string.IsNullOrEmpty(tensionType)) return "tension_detected";

            return tensionType.ToLower() switch
            {
                "urgent" => "urgency_expressed",
                "repeatedquestion" => "repetition_required",
                "repeated_question" => "repetition_required",
                "delayed" => "delayed_response",
                "delayedresponse" => "delayed_response",
                "delayed_response" => "delayed_response",
                "negative" => "frustration_expressed",
                "negativesentiment" => "frustration_expressed",
                "negative_sentiment" => "frustration_expressed",
                "escalation" => "escalation_threatened",
                "dismissive" => "dismissive_response",
                _ => "tension_detected"
            };
        }

        private string InferTopic(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "general";

            var lowerText = text.ToLower();

            foreach (var topic in _taxonomy.Topics)
            {
                if (topic.Keywords != null && topic.Keywords.Any(k => lowerText.Contains(k.ToLower())))
                {
                    return topic.Key;
                }
            }

            return "general";
        }

        private string InferRole(string? participantName, ThreadCapsule capsule)
        {
            if (string.IsNullOrEmpty(participantName)) return "unknown";

            var lowerName = participantName.ToLower();

            // Check against taxonomy roles
            foreach (var role in _taxonomy.Roles)
            {
                if (role.Keywords != null && role.Keywords.Any(k => lowerName.Contains(k.ToLower())))
                {
                    return role.Key;
                }

                // Check email domain patterns if we have participant email
                if (role.EmailDomainPatterns != null && role.EmailDomainPatterns.Length > 0)
                {
                    var participant = capsule.Participants?.FirstOrDefault(p =>
                        p.Name?.Equals(participantName, StringComparison.OrdinalIgnoreCase) == true);

                    if (participant?.Email != null)
                    {
                        foreach (var pattern in role.EmailDomainPatterns)
                        {
                            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                            if (Regex.IsMatch(participant.Email, regexPattern, RegexOptions.IgnoreCase))
                            {
                                return role.Key;
                            }
                        }
                    }
                }
            }

            return "unknown";
        }

        private string DetermineSeverity(string category, string value, string topic, int daysUnanswered = 0, int timesAsked = 1)
        {
            // Check custom severity rules first
            foreach (var rule in _taxonomy.SeverityRules)
            {
                if (rule.Category == category && (rule.Value == "*" || rule.Value == value))
                {
                    if (EvaluateCondition(rule.Condition, topic, daysUnanswered, timesAsked))
                    {
                        return rule.Severity.ToLower();
                    }
                }
            }

            // Default severity logic
            if (value == "repeated_unanswered" || timesAsked > 2) return "high";
            if (daysUnanswered > 2) return "high";
            if (daysUnanswered > 0 || timesAsked > 1) return "medium";

            return "low";
        }

        private bool EvaluateCondition(string? condition, string topic, int daysUnanswered, int timesAsked)
        {
            if (string.IsNullOrEmpty(condition)) return true;

            // Simple condition parser
            if (condition.Contains("topic =="))
            {
                var match = Regex.Match(condition, @"topic\s*==\s*'(\w+)'");
                if (match.Success)
                {
                    return topic == match.Groups[1].Value;
                }
            }

            if (condition.Contains("daysUnanswered >"))
            {
                var match = Regex.Match(condition, @"daysUnanswered\s*>\s*(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var threshold))
                {
                    return daysUnanswered > threshold;
                }
            }

            if (condition.Contains("timesAsked >"))
            {
                var match = Regex.Match(condition, @"timesAsked\s*>\s*(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var threshold))
                {
                    return timesAsked > threshold;
                }
            }

            return false;
        }
    }
}