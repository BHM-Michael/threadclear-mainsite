using System.Threading.Tasks;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IConversationAnalysisService
    {
        Task<object> AnalyzeSummary(string text);
        Task<object> AnalyzeQuestions(string text);
        Task<object> AnalyzeTensions(string text);
        Task<object> AnalyzeHealth(string text);
        Task<object> AnalyzeActions(string text);
        Task<object> AnalyzeMisalignments(string text);
    }
}