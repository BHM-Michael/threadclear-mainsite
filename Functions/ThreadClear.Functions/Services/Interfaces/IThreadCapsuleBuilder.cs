using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    /// <summary>
    /// Builds enriched ThreadCapsule with analysis and metadata
    /// </summary>
    public interface IThreadCapsuleBuilder
    {
        /// <summary>
        /// Build a complete ThreadCapsule from parsed messages
        /// </summary>
        Task<ThreadCapsule> BuildCapsule(List<Message> messages, List<Participant> participants);

        /// <summary>
        /// Enrich capsule with linguistic features (questions, sentiment, etc.)
        /// </summary>
        Task EnrichWithLinguisticFeatures(ThreadCapsule capsule);

        /// <summary>
        /// Calculate conversation metadata (timeline, duration, etc.)
        /// </summary>
        Task CalculateMetadata(ThreadCapsule capsule);

        /// <summary>
        /// Generate summary for the conversation
        /// </summary>
        Task<string> GenerateSummary(ThreadCapsule capsule);
    }
}
