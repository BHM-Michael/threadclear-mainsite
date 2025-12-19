using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    /// <summary>
    /// Interface for parsing conversations with support for multiple parsing modes
    /// </summary>
    public interface IConversationParser
    {
        /// <summary>
        /// Parses a conversation into a structured ThreadCapsule
        /// </summary>
        /// <param name="conversationText">Raw conversation text</param>
        /// <param name="sourceType">Type of conversation (email, slack, sms, etc.)</param>
        /// <param name="mode">Optional parsing mode (Basic, Advanced, Auto). Uses default if not specified.</param>
        /// <returns>Parsed ThreadCapsule containing participants and messages</returns>
        Task<ThreadCapsule> ParseConversation(string conversationText, string sourceType, ParsingMode? mode = null);

        /// <summary>
        /// Extracts participants from conversation text
        /// </summary>
        /// <param name="conversationText">Raw conversation text</param>
        /// <param name="sourceType">Type of conversation</param>
        /// <param name="mode">Optional parsing mode</param>
        /// <returns>List of identified participants</returns>
        Task<List<Participant>> ExtractParticipants(string conversationText, string sourceType, ParsingMode? mode = null);

        /// <summary>
        /// Extracts messages from conversation text
        /// </summary>
        /// <param name="conversationText">Raw conversation text</param>
        /// <param name="sourceType">Type of conversation</param>
        /// <param name="mode">Optional parsing mode</param>
        /// <returns>List of parsed messages</returns>
        Task<List<Message>> ExtractMessages(string conversationText, string sourceType, ParsingMode? mode = null);
    }
}
