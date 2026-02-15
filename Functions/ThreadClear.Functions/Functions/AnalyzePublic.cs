using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class AnalyzePublic
    {
        private readonly ILogger _logger;
        private readonly IConversationParser _parser;
        private readonly IConversationAnalyzer _analyzer;
        private readonly IThreadCapsuleBuilder _builder;
        private readonly IRateLimitService _rateLimitService;

        private const int MAX_FREE_SCANS_PER_DAY = 3;
        private const int MAX_TEXT_LENGTH = 10000; // Prevent abuse with huge payloads

        public AnalyzePublic(
            ILoggerFactory loggerFactory,
            IConversationParser parser,
            IConversationAnalyzer analyzer,
            IThreadCapsuleBuilder builder,
            IRateLimitService rateLimitService)
        {
            _logger = loggerFactory.CreateLogger<AnalyzePublic>();
            _parser = parser;
            _analyzer = analyzer;
            _builder = builder;
            _rateLimitService = rateLimitService;
        }

        [Function("AnalyzePublic")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "analyze-public")]
            HttpRequestData req)
        {
            if (req.Method == "OPTIONS")
            {
                var corsResponse = req.CreateResponse(HttpStatusCode.OK);
                return corsResponse;
            }

            _logger.LogInformation("Processing PUBLIC conversation analysis request");

            try
            {
                // Get client IP for rate limiting
                var clientIp = GetClientIp(req);

                // Check rate limit
                if (!_rateLimitService.IsAllowed(clientIp, MAX_FREE_SCANS_PER_DAY))
                {
                    var remaining = _rateLimitService.GetRemainingRequests(clientIp, MAX_FREE_SCANS_PER_DAY);
                    var rateLimitResponse = req.CreateResponse(HttpStatusCode.TooManyRequests);
                    await rateLimitResponse.WriteAsJsonAsync(new
                    {
                        success = false,
                        error = "Daily free scan limit reached. Create a free account for more analyses!",
                        remainingScans = remaining,
                        upgradeUrl = "https://app.threadclear.com/login"
                    });
                    return rateLimitResponse;
                }

                // Parse request
                var request = await req.ReadFromJsonAsync<AnalysisRequest>();

                if (request == null || string.IsNullOrWhiteSpace(request.ConversationText))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Please paste a conversation to analyze.");
                }

                // Enforce text length limit for public endpoint
                if (request.ConversationText.Length > MAX_TEXT_LENGTH)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest,
                        $"Text exceeds {MAX_TEXT_LENGTH} character limit. Create a free account for longer conversations!");
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Parse conversation
                var capsule = await _parser.ParseConversation(
                    request.ConversationText,
                    request.SourceType ?? "simple",
                    null); // Auto mode

                _logger.LogInformation("PUBLIC TIMING: ParseConversation took {Ms}ms", sw.ElapsedMilliseconds);
                sw.Restart();

                // Run analysis - all features enabled for public (show full value)
                var options = new AnalysisOptions
                {
                    EnableUnansweredQuestions = true,
                    EnableTensionPoints = true,
                    EnableMisalignments = true,
                    EnableConversationHealth = true,
                    EnableSuggestedActions = true
                };

                await _analyzer.AnalyzeConversation(capsule, options, null); // No taxonomy for public

                await _builder.CalculateMetadata(capsule);

                _logger.LogInformation("PUBLIC TIMING: Full analysis took {Ms}ms", sw.ElapsedMilliseconds);

                var modeUsed = capsule.Metadata.TryGetValue("ParsingMode", out var pm) ? pm : "Advanced";
                var remaining2 = _rateLimitService.GetRemainingRequests(clientIp, MAX_FREE_SCANS_PER_DAY);

                // Track the scan (fire and forget - don't slow down response)
                _ = _rateLimitService.LogPublicScanAsync(
                    clientIp,
                    request.SourceType ?? "simple",
                    request.ConversationText.Length,
                    capsule.Participants?.Count ?? 0,
                    capsule.Messages?.Count ?? 0,
                    (int)sw.ElapsedMilliseconds);

                _logger.LogInformation("Public analysis complete - IP {IP} has {Remaining} scans remaining",
                    clientIp.Split('.').Take(2).Aggregate((a, b) => $"{a}.{b}") + ".xxx.xxx",
                    remaining2);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    capsule = capsule,
                    parsingMode = modeUsed,
                    remainingScans = remaining2,
                    isPublicScan = true
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing public conversation");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError,
                    "An error occurred processing your request. Please try again.");
            }
        }

        private string GetClientIp(HttpRequestData req)
        {
            // Azure Functions behind a load balancer/proxy
            if (req.Headers.TryGetValues("X-Forwarded-For", out var forwardedFor))
            {
                var ip = forwardedFor.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(ip))
                    return ip;
            }

            if (req.Headers.TryGetValues("X-Real-IP", out var realIp))
            {
                var ip = realIp.FirstOrDefault();
                if (!string.IsNullOrEmpty(ip))
                    return ip;
            }

            // Fallback
            return "unknown";
        }

        private async Task<HttpResponseData> CreateErrorResponse(
            HttpRequestData req,
            HttpStatusCode statusCode,
            string message)
        {
            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(new
            {
                success = false,
                error = message
            });
            return response;
        }
    }
}