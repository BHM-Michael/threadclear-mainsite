using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    /// <summary>
    /// Interface for AI service operations
    /// Can be implemented with OpenAI, Anthropic Claude, Azure OpenAI, or other LLM providers
    /// </summary>
    public interface IAIService
    {
        /// <summary>
        /// Generates a response from the AI model based on the prompt
        /// </summary>
        /// <param name="prompt">The prompt to send to the AI</param>
        /// <returns>AI-generated response text</returns>
        Task<string> GenerateResponseAsync(string prompt);

        /// <summary>
        /// Generates a structured JSON response from the AI model
        /// </summary>
        /// <param name="prompt">The prompt requesting JSON output</param>
        /// <returns>AI-generated JSON string</returns>
        Task<string> GenerateStructuredResponseAsync(string prompt);

        /// <summary>
        /// Analyzes text and returns sentiment, urgency, and other metrics
        /// </summary>
        /// <param name="text">Text to analyze</param>
        /// <returns>Analysis results as JSON</returns>
        Task<string> AnalyzeTextAsync(string text);

        /// <summary>
        /// Analyzes a conversation thread using AI
        /// </summary>
        /// <param name="prompt">The analysis prompt</param>
        /// <param name="capsule">The conversation capsule to analyze</param>
        /// <returns>AI-generated analysis response</returns>
        Task<string> AnalyzeConversation(string prompt, ThreadCapsule? capsule);

        /// <summary>
        /// Generates suggested actions based on conversation analysis
        /// </summary>
        /// <param name="capsule">The analyzed conversation capsule</param>
        /// <returns>List of suggested action strings</returns>
        Task<List<SuggestedActionItem>> GenerateSuggestedActions(ThreadCapsule capsule);

        Task<string> ExtractTextFromImage(string base64Image, string mimeType);

        // NEW: Streaming support
        IAsyncEnumerable<string> StreamResponseAsync(string prompt);
    }
}
