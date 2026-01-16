using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.Azure.Functions.Worker;

namespace ThreadClear.Functions.Functions
{
    public class HealthCheck
    {
        private readonly ILogger _logger;
        private readonly string _connectionString;

        public HealthCheck(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HealthCheck>();
            _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString")
                ?? throw new InvalidOperationException("SqlConnectionString not configured");
        }

        [Function("HealthCheck")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] 
            HttpRequestData req)
        {
            _logger.LogInformation("Health check requested");

            var response = req.CreateResponse(HttpStatusCode.OK);

            try
            {
   
                await response.WriteAsJsonAsync(new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    version = "1.0.0"
                });

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync();
                _logger.LogDebug("Database keep-alive successful at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database keep-alive failed");
            }
            return response;
        }

        [Function("DatabaseKeepAlive")]
        public async Task RunKeepAlive(
    [TimerTrigger("0 */5 * * * *")] TimerInfo timer)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync();
                _logger.LogDebug("Database keep-alive successful at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database keep-alive failed");
            }
        }
    }
}
