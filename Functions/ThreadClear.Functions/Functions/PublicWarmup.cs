using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class PublicWarmup
    {
        private readonly ILogger _logger;
        private readonly IRateLimitService _rateLimitService;

        public PublicWarmup(ILoggerFactory loggerFactory, IRateLimitService rateLimitService)
        {
            _logger = loggerFactory.CreateLogger<PublicWarmup>();
            _rateLimitService = rateLimitService;
        }

        [Function("PublicWarmup")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "public/warmup")]
            HttpRequestData req)
        {
            if (req.Method == "OPTIONS")
            {
                return req.CreateResponse(HttpStatusCode.OK);
            }

            try
            {
                await _rateLimitService.PingAsync();
                _logger.LogInformation("Database warmup ping successful");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Warmup ping failed: {Message}", ex.Message);
                // Still return 200 — silent failure, best-effort only
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { status = "warm" });
            return response;
        }
    }
}