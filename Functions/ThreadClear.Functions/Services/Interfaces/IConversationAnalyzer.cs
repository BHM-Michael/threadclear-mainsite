using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    /// <summary>
    /// ⭐ HERO FEATURES - Advanced conversation analysis
    /// Identifies unanswered questions, tension points, and conversation health
    /// </summary>
    public interface IConversationAnalyzer
    {
        /// <summary>
        /// Perform complete conversation analysis (hero features)
        /// </summary>
        Task AnalyzeConversation(ThreadCapsule capsule, AnalysisOptions? options = null, TaxonomyData? taxonomy = null);

        /// <summary>
        /// ⭐ HERO: Analyze a draft reply in context of the conversation
        /// </summary>
        Task<DraftAnalysis> AnalyzeDraft(ThreadCapsule capsule, string draftMessage);

        /// <summary>
        /// ⭐ HERO: Detect unanswered questions with persistence tracking
        /// </summary>
        Task<List<UnansweredQuestion>> DetectUnansweredQuestions(ThreadCapsule capsule);

        /// <summary>
        /// ⭐ HERO: Identify tension points and communication breakdowns
        /// </summary>
        Task<List<TensionPoint>> IdentifyTensionPoints(ThreadCapsule capsule);

        /// <summary>
        /// ⭐ HERO: Assess overall conversation health and risk level
        /// </summary>
        Task<ConversationHealth> AssessConversationHealth(ThreadCapsule capsule);

        /// <summary>
        /// Detect misalignments between participants
        /// </summary>
        Task<List<Misalignment>> DetectMisalignments(ThreadCapsule capsule);

        /// <summary>
        /// Track decision points and commitments
        /// </summary>
        Task<List<DecisionPoint>> TrackDecisions(ThreadCapsule capsule);

        /// <summary>
        /// Identify action items and follow-ups needed
        /// </summary>
        Task<List<ActionItem>> IdentifyActionItems(ThreadCapsule capsule);
    }
}