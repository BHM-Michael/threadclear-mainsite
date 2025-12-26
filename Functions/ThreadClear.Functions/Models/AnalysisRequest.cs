namespace ThreadClear.Functions.Models
{
    /// <summary>
    /// Request model for conversation analysis
    /// </summary>
    public class AnalysisRequest
    {
        public string ConversationText { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public string? PriorityLevel { get; set; }
        public ParsingMode? ParsingMode { get; set; }

        /// <summary>
        /// Optional draft reply to analyze in context of the conversation
        /// </summary>
        public string? DraftMessage { get; set; }

        // Permission flags for combined analysis
        public bool? EnableUnansweredQuestions { get; set; }
        public bool? EnableTensionPoints { get; set; }
        public bool? EnableMisalignments { get; set; }
        public bool? EnableConversationHealth { get; set; }
        public bool? EnableSuggestedActions { get; set; }

        /// <summary>
        /// Returns true if any permission flags were explicitly set
        /// </summary>
        public bool HasPermissionFlags()
        {
            return EnableUnansweredQuestions.HasValue ||
                   EnableTensionPoints.HasValue ||
                   EnableMisalignments.HasValue ||
                   EnableConversationHealth.HasValue ||
                   EnableSuggestedActions.HasValue;
        }
    }
}