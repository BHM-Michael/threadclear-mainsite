using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class AnalyzeSection
    {
        private readonly ILogger _logger;
        private readonly IConversationAnalysisService _analysisService;

        public AnalyzeSection(ILoggerFactory loggerFactory, IConversationAnalysisService analysisService)
        {
            _logger = loggerFactory.CreateLogger<AnalyzeSection>();
            _analysisService = analysisService;
        }

        [Function("AnalyzeSection")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options",
                Route = "analyze/section")] HttpRequestData req)
        {
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                return req.CreateResponse(HttpStatusCode.OK);

            var request = await req.ReadFromJsonAsync<SectionAnalysisRequest>();
            if (request == null || string.IsNullOrWhiteSpace(request.ConversationText)
                                || string.IsNullOrWhiteSpace(request.Section))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteAsJsonAsync(new
                {
                    success = false,
                    error = "Invalid request - conversationText and section are required"
                });
                return errorResponse;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("AnalyzeSection started: {Section}", request.Section);

            try
            {
                object result = request.Section.ToLower() switch
                {
                    "summary" => await _analysisService.AnalyzeSummary(request.ConversationText),
                    "questions" => await _analysisService.AnalyzeQuestions(request.ConversationText),
                    "tensions" => await _analysisService.AnalyzeTensions(request.ConversationText),
                    "health" => await _analysisService.AnalyzeHealth(request.ConversationText),
                    "actions" => await _analysisService.AnalyzeActions(request.ConversationText),
                    "misalignments" => await _analysisService.AnalyzeMisalignments(request.ConversationText),
                    _ => throw new ArgumentException($"Unknown section: {request.Section}")
                };

                _logger.LogInformation("AnalyzeSection completed: {Section} in {Ms}ms",
                    request.Section, sw.ElapsedMilliseconds);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    section = request.Section,
                    data = result,
                    timeMs = sw.ElapsedMilliseconds
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AnalyzeSection error for section {Section}", request.Section);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new
                {
                    success = false,
                    section = request.Section,
                    error = ex.Message
                });
                return errorResponse;
            }
        }
    }

    public class SectionAnalysisRequest
    {
        public string ConversationText { get; set; } = "";
        public string Section { get; set; } = "";
    }
}