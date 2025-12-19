namespace ThreadClear.Functions.Models
{
    /// <summary>
    /// Parsing mode selection for cost/performance trade-offs
    /// </summary>
    public enum ParsingMode
    {
        /// <summary>
        /// Fast, free, regex-based parsing. Good for standard formats.
        /// </summary>
        Basic,
        
        /// <summary>
        /// AI-powered parsing with better accuracy and edge case handling.
        /// Costs ~$0.001-0.01 per conversation.
        /// </summary>
        Advanced,
        
        /// <summary>
        /// Automatically chooses based on conversation complexity.
        /// Simple formats use Basic, complex/ambiguous use Advanced.
        /// </summary>
        Auto
    }
}
