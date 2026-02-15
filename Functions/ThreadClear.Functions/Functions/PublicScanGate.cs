using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class PublicScanGate
    {
        private readonly ILogger _logger;
        private readonly IRateLimitService _rateLimitService;

        private const int MAX_FREE_SCANS_PER_DAY = 3;

        public PublicScanGate(
            ILoggerFactory loggerFactory,
            IRateLimitService rateLimitService)
        {
            _logger = loggerFactory.CreateLogger<PublicScanGate>();
            _rateLimitService = rateLimitService;
        }

        [Function("PublicScanGate")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "public/start")]
            HttpRequestData req)
        {
            if (req.Method == "OPTIONS")
            {
                return req.CreateResponse(HttpStatusCode.OK);
            }

            var clientIp = GetClientIp(req);

            if (!_rateLimitService.IsAllowed(clientIp, MAX_FREE_SCANS_PER_DAY))
            {
                var remaining = _rateLimitService.GetRemainingRequests(clientIp, MAX_FREE_SCANS_PER_DAY);
                var blockedResponse = req.CreateResponse(HttpStatusCode.TooManyRequests);
                await blockedResponse.WriteAsJsonAsync(new
                {
                    allowed = false,
                    remainingScans = remaining,
                    error = "Daily free scan limit reached. Create a free account for more analyses!"
                });
                return blockedResponse;
            }

            var remainingScans = _rateLimitService.GetRemainingRequests(clientIp, MAX_FREE_SCANS_PER_DAY);

            // Read request body to log scan metadata
            var request = await req.ReadFromJsonAsync<PublicScanStartRequest>();
            var textLength = request?.TextLength ?? 0;

            _logger.LogInformation("Public scan started - IP {IP}, {Length} chars, {Remaining} scans remaining",
                MaskIp(clientIp), textLength, remainingScans);

            // Log the scan (fire and forget)
            _ = _rateLimitService.LogPublicScanAsync(
                clientIp,
                request?.SourceType ?? "unknown",
                textLength,
                0, 0, 0); // Participant/message/time counts updated aren't critical here

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                allowed = true,
                remainingScans = remainingScans
            });
            return response;
        }

        private string GetClientIp(HttpRequestData req)
        {
            if (req.Headers.TryGetValues("X-Forwarded-For", out var forwardedFor))
            {
                var ip = forwardedFor.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(ip)) return ip;
            }
            if (req.Headers.TryGetValues("X-Real-IP", out var realIp))
            {
                var ip = realIp.FirstOrDefault();
                if (!string.IsNullOrEmpty(ip)) return ip;
            }
            return "unknown";
        }

        private string MaskIp(string ip)
        {
            var parts = ip.Split('.');
            return parts.Length == 4 ? $"{parts[0]}.{parts[1]}.xxx.xxx" : "masked";
        }
    }

    public class PublicScanStartRequest
    {
        public int TextLength { get; set; }
        public string? SourceType { get; set; }
    }
}