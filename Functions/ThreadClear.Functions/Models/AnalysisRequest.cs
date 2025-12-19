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
    }
}
