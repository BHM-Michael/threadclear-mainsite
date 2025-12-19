namespace ThreadClear.Functions.Models
{
    /// <summary>
    /// Represents a participant in a conversation
    /// </summary>
    public class Participant
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public ParticipantRole InferredRole { get; set; } = ParticipantRole.Unknown;
    }

    /// <summary>
    /// Role of a participant in the conversation
    /// </summary>
    public enum ParticipantRole
    {
        Unknown,
        Manager,
        Employee,
        Customer,
        Vendor,
        Support,
        Executive
    }
}
