using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class SpellCheck
    {
        private readonly ILogger _logger;
        private readonly ISpellCheckService _spellCheckService;

        public SpellCheck(ILoggerFactory loggerFactory, ISpellCheckService spellCheckService)
        {
            _logger = loggerFactory.CreateLogger<SpellCheck>();
            _spellCheckService = spellCheckService;
        }

        /// <summary>
        /// Check spelling/grammar for a list of messages
        /// POST /api/spellcheck/messages
        /// </summary>
        [Function("SpellCheckMessages")]
        public async Task<HttpResponseData> CheckMessages(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "spellcheck/messages")]
            HttpRequestData req)
        {
            if (req.Method == "OPTIONS")
            {
                return req.CreateResponse(HttpStatusCode.OK);
            }

            _logger.LogInformation("Processing spell check request");

            try
            {
                var request = await req.ReadFromJsonAsync<SpellCheckMessagesRequest>();

                if (request?.Messages == null || request.Messages.Count == 0)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "No messages provided");
                }

                _logger.LogInformation("Checking {Count} messages for spelling/grammar", request.Messages.Count);

                var results = await _spellCheckService.CheckMessagesAsync(request.Messages);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    results = results,
                    totalIssues = results.Sum(r => r.Issues.Count)
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing spell check request");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to check spelling");
            }
        }

        /// <summary>
        /// Check spelling/grammar for a single text block
        /// POST /api/spellcheck/text
        /// </summary>
        [Function("SpellCheckText")]
        public async Task<HttpResponseData> CheckText(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "spellcheck/text")]
            HttpRequestData req)
        {
            if (req.Method == "OPTIONS")
            {
                return req.CreateResponse(HttpStatusCode.OK);
            }

            try
            {
                var request = await req.ReadFromJsonAsync<SpellCheckTextRequest>();

                if (string.IsNullOrWhiteSpace(request?.Text))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "No text provided");
                }

                var result = await _spellCheckService.CheckTextAsync(request.Text);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    issues = result.Issues,
                    totalIssues = result.TotalIssues
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing spell check request");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to check spelling");
            }
        }

        private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
        {
            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(new { success = false, error = message });
            return response;
        }
    }

    public class SpellCheckMessagesRequest
    {
        public List<MessageToCheck> Messages { get; set; } = new List<MessageToCheck>();
    }

    public class SpellCheckTextRequest
    {
        public string Text { get; set; } = string.Empty;
    }
}