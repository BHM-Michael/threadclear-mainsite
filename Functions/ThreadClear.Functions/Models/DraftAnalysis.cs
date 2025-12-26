namespace ThreadClear.Functions.Models
{
    /// <summary>
    /// Analysis results for a draft message in context of a conversation
    /// </summary>
    public class DraftAnalysis
    {
        public ToneAssessment Tone { get; set; } = new();
        public List<QuestionCoverage> QuestionsCovered { get; set; } = new();
        public List<string> QuestionsIgnored { get; set; } = new();
        public List<string> NewQuestionsIntroduced { get; set; } = new();
        public List<RiskFlag> RiskFlags { get; set; } = new();
        public int CompletenessScore { get; set; }
        public List<string> Suggestions { get; set; } = new();
        public string OverallAssessment { get; set; } = string.Empty;
        public bool ReadyToSend { get; set; }
    }

    /// <summary>
    /// Assessment of the draft message's tone
    /// </summary>
    public class ToneAssessment
    {
        public string Tone { get; set; } = string.Empty;
        public bool MatchesConversationTone { get; set; }
        public string EscalationRisk { get; set; } = "none";
        public string Explanation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Tracks whether a question from the conversation was addressed in the draft
    /// </summary>
    public class QuestionCoverage
    {
        public string Question { get; set; } = string.Empty;
        public bool Addressed { get; set; }
        public string? HowAddressed { get; set; }
    }

    /// <summary>
    /// A potential risk identified in the draft message
    /// </summary>
    public class RiskFlag
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "low";
        public string Suggestion { get; set; } = string.Empty;
    }
}