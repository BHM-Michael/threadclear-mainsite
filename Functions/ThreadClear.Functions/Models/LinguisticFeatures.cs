using System.Collections.Generic;

namespace ThreadClear.Functions.Models
{
    /// <summary>
    /// Linguistic analysis results for a message
    /// </summary>
    public class LinguisticFeatures
    {
        public List<string> Questions { get; set; } = new List<string>();
        public bool ContainsQuestion { get; set; }
        public int WordCount { get; set; }
        public int SentenceCount { get; set; }
        public string Sentiment { get; set; } = "Neutral";
        public string Urgency { get; set; } = "Low";
        public double Politeness { get; set; } = 0.5;
        public List<string> UrgencyMarkers { get; set; } = new List<string>();
    }
}
