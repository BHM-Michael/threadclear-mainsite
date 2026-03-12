using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;
using ThreadClear.Models;

namespace ThreadClear.Functions.Functions
{
    public class DigestAnalyzerFunction
    {
        private readonly IConversationAnalysisService _analysisService;
        private readonly IDigestInsightRepository _digestRepo;
        private readonly ILogger<DigestAnalyzerFunction> _logger;

        public DigestAnalyzerFunction(
            IConversationAnalysisService analysisService,
            IDigestInsightRepository digestRepo,
            ILogger<DigestAnalyzerFunction> logger)
        {
            _analysisService = analysisService;
            _digestRepo = digestRepo;
            _logger = logger;
        }

        [Function("DigestAnalyzerFunction")]
        public async Task Run(
            [QueueTrigger("threadclear-digest-queue",
                Connection = "AzureWebJobsStorage")] string messageJson)
        {
            QueuedEmailMessage? queuedMessage;
            try
            {
                queuedMessage = JsonSerializer.Deserialize<QueuedEmailMessage>(messageJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize queue message");
                return;
            }

            if (queuedMessage == null || string.IsNullOrWhiteSpace(queuedMessage.BodyText))
            {
                _logger.LogWarning("Queue message missing BodyText, skipping");
                return;
            }

            _logger.LogInformation(
                "DigestAnalyzer processing thread {ThreadId} for user {UserId} via {Provider}",
                queuedMessage.ThreadId, queuedMessage.UserId, queuedMessage.Provider);

            try
            {
                // Call the exact same service AnalyzeSection uses — one set of prompts
                var healthResult = await _analysisService.AnalyzeHealth(queuedMessage.BodyText);
                var questionsResult = await _analysisService.AnalyzeQuestions(queuedMessage.BodyText);
                var tensionsResult = await _analysisService.AnalyzeTensions(queuedMessage.BodyText);
                var summaryResult = await _analysisService.AnalyzeSummary(queuedMessage.BodyText);

                // Extract values from the returned JsonElement objects
                var healthScore = 50;
                var riskLevel = "Medium";
                var unansweredCount = 0;
                var tensionCount = 0;
                var summary = "No summary available.";

                if (healthResult is JsonElement healthEl &&
                    healthEl.TryGetProperty("ConversationHealth", out var health))
                {
                    if (health.TryGetProperty("OverallScore", out var score))
                        healthScore = score.GetInt32();
                    if (health.TryGetProperty("RiskLevel", out var risk))
                        riskLevel = risk.GetString() ?? "Medium";
                }

                if (questionsResult is JsonElement questionsEl &&
                    questionsEl.TryGetProperty("UnansweredQuestions", out var questions))
                    unansweredCount = questions.GetArrayLength();

                if (tensionsResult is JsonElement tensionsEl &&
                    tensionsEl.TryGetProperty("TensionPoints", out var tensions))
                    tensionCount = tensions.GetArrayLength();

                if (summaryResult is JsonElement summaryEl &&
                    summaryEl.TryGetProperty("Summary", out var summaryProp))
                    summary = summaryProp.GetString() ?? "No summary available.";

                var digestInsight = new DigestInsight
                {
                    UserId = queuedMessage.UserId,
                    Provider = queuedMessage.Provider,
                    ThreadId = queuedMessage.ThreadId,
                    Subject = queuedMessage.Subject,
                    HealthScore = healthScore,
                    RiskLevel = riskLevel,
                    UnansweredQuestions = unansweredCount,
                    TensionSignals = tensionCount,
                    Summary = summary,
                    AnalyzedAt = DateTime.UtcNow
                };

                await _digestRepo.CreateAsync(digestInsight);

                _logger.LogInformation(
                    "DigestInsight stored for thread {ThreadId} — Health: {Score}, Risk: {Risk}",
                    queuedMessage.ThreadId, digestInsight.HealthScore, digestInsight.RiskLevel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Analysis failed for thread {ThreadId}", queuedMessage.ThreadId);
                throw;
            }
        }
    }
}