using System;
using System.Collections.Generic;

namespace ThreadClear.Functions.Models
{
    /// <summary>
    /// Represents a single message in a conversation
    /// </summary>
    public class Message
    {
        public string Id { get; set; } = string.Empty;
        public string ParticipantId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? ResponseTo { get; set; }
        public Sentiment? Sentiment { get; set; }
        public LinguisticFeatures? LinguisticFeatures { get; set; } = new LinguisticFeatures();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Sentiment analysis result
    /// </summary>
    public class Sentiment
    {
        public SentimentPolarity Polarity { get; set; } = SentimentPolarity.Neutral;
        public double Intensity { get; set; } = 0.5;
    }

    /// <summary>
    /// Sentiment polarity enumeration
    /// </summary>
    public enum SentimentPolarity
    {
        Positive,
        Negative,
        Neutral
    }
}
