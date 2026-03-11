using System;

namespace ThreadClear.Models
{
    public class QueuedEmailMessage
    {
        // Identity
        public Guid UserId { get; set; }
        public string Provider { get; set; }      // "gmail" | "outlook"
        public string ThreadId { get; set; }       // provider's native thread ID
        public string MessageId { get; set; }      // provider's native message ID

        // Content
        public string Subject { get; set; }
        public string BodyText { get; set; }       // plain text, stripped of HTML
        public string Participants { get; set; }   // comma-separated display names/emails
        public int MessageCount { get; set; }

        // Metadata
        public DateTime ReceivedAt { get; set; }
        public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    }
}