using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ThreadClear.Functions.Functions
{
    public class HealthCheck
    {
        private readonly ILogger _logger;

        public HealthCheck(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HealthCheck>();
        }

        [Function("HealthCheck")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] 
            HttpRequestData req)
        {
            _logger.LogInformation("Health check requested");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            });

            return response;
        }
    }
}
