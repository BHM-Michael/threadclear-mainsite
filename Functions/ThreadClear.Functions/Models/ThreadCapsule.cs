using System;
using System.Collections.Generic;

namespace ThreadClear.Functions.Models
{
    /// <summary>
    /// Container for a parsed conversation with all its components
    /// </summary>
    public class ThreadCapsule
    {
        public string CapsuleId { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public DateTime CreatedAt { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string RawText { get; set; } = string.Empty;
        public ConversationSource Source { get; set; } = new ConversationSource();
        public List<Participant> Participants { get; set; } = new List<Participant>();
        public List<Message> Messages { get; set; } = new List<Message>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        public ThreadMetadata ThreadMetadata { get; set; } = new ThreadMetadata();
        public ConversationAnalysis? Analysis { get; set; }
        public ConversationGraph ConversationGraph { get; set; } = new ConversationGraph();
        public List<string> SuggestedActions { get; set; } = new List<string>();
        public string Summary { get; set; } = string.Empty;
        public List<string> KeyPoints { get; set; } = new List<string>();
    }

    /// <summary>
    /// Source information for the conversation
    /// </summary>
    public class ConversationSource
    {
        public SourceType Type { get; set; } = SourceType.Unknown;
        public string? Identifier { get; set; }
    }

    /// <summary>
    /// Type of conversation source
    /// </summary>
    public enum SourceType
    {
        Unknown,
        Email,
        Slack,
        Teams,
        Chat,
        SMS,
        Forum
    }

    /// <summary>
    /// Graph representation of the conversation structure
    /// </summary>
    public class ConversationGraph
    {
        public List<string> Nodes { get; set; } = new List<string>();
        public List<GraphEdge> Edges { get; set; } = new List<GraphEdge>();
    }

    /// <summary>
    /// Edge in the conversation graph
    /// </summary>
    public class GraphEdge
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public EdgeType Type { get; set; } = EdgeType.Response;
    }

    /// <summary>
    /// Type of relationship between messages
    /// </summary>
    public enum EdgeType
    {
        Response,
        Reference,
        Quote,
        Forward
    }

    /// <summary>
    /// Metadata about the conversation thread
    /// </summary>
    public class ThreadMetadata
    {
        public string Subject { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    /// <summary>
    /// Complete conversation analysis results
    /// </summary>
    public class ConversationAnalysis
    {
        public List<UnansweredQuestion> UnansweredQuestions { get; set; } = new List<UnansweredQuestion>();
        public List<TensionPoint> TensionPoints { get; set; } = new List<TensionPoint>();
        public List<Misalignment> Misalignments { get; set; } = new List<Misalignment>();
        public ConversationHealth? ConversationHealth { get; set; }
        public List<DecisionPoint> Decisions { get; set; } = new List<DecisionPoint>();
        public List<ActionItem> ActionItems { get; set; } = new List<ActionItem>();
        public List<string> SilentAssumptions { get; set; } = new List<string>();
        public List<KeyMoment> KeyMoments { get; set; } = new List<KeyMoment>();
    }

    /// <summary>
    /// A key moment in the conversation
    /// </summary>
    public class KeyMoment
    {
        public string MessageId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Represents a question that was asked but not answered
    /// </summary>
    public class UnansweredQuestion
    {
        public string? Question { get; set; }
        public string? AskedBy { get; set; }
        public DateTime AskedAt { get; set; }
        public int TimesAsked { get; set; }
        public string? MessageId { get; set; }
        public double DaysUnanswered { get; set; }
    }

    /// <summary>
    /// Represents a point of tension or frustration in the conversation
    /// </summary>
    public class TensionPoint
    {
        public string? Type { get; set; }
        public string? Severity { get; set; }
        public string? Description { get; set; }
        public string? MessageId { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime DetectedAt { get; set; }
        public List<string> Participants { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents a misalignment between participants
    /// </summary>
    public class Misalignment
    {
        public string? Type { get; set; }
        public string? Severity { get; set; }
        public string? Description { get; set; }
        public List<string> ParticipantsInvolved { get; set; } = new List<string>();
        public string? SuggestedResolution { get; set; }
    }

    /// <summary>
    /// Overall health assessment of the conversation
    /// </summary>
    public class ConversationHealth
    {
        public string? RiskLevel { get; set; }
        public double HealthScore { get; set; }
        public double ResponsivenessScore { get; set; } = 0.5;
        public double ClarityScore { get; set; } = 0.5;
        public double AlignmentScore { get; set; } = 0.5;
        public List<string?> Issues { get; set; } = new List<string?>();
        public List<string?> Strengths { get; set; } = new List<string?>();
        public List<string?> Recommendations { get; set; } = new List<string?>();
    }

    /// <summary>
    /// Represents a decision made in the conversation
    /// </summary>
    public class DecisionPoint
    {
        public string Decision { get; set; } = string.Empty;
        public string DecidedBy { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string MessageId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents an action item identified in the conversation
    /// </summary>
    public class ActionItem
    {
        public string? Action { get; set; }
        public string? AssignedTo { get; set; }
        public string? RequestedBy { get; set; }
        public DateTime Timestamp { get; set; }
        public string? MessageId { get; set; }
        public string? Priority { get; set; }
        public string? Status { get; set; }
    }
}
