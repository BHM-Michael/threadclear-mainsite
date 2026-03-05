namespace ThreadClear.Functions.Models
{
    public class NeedsAttentionItem
    {
        public string Id { get; set; } = "";
        public string OverallRisk { get; set; } = "";
        public int HealthScore { get; set; }
        public string Timestamp { get; set; } = "";
        public string SourceType { get; set; } = "";
        public int UnansweredQuestionsCount { get; set; }
        public int TensionPointsCount { get; set; }
    }
}