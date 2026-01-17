using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ThreadClear.Functions.Functions
{
    public class GmailStatusFunction
    {
        private readonly ILogger<GmailStatusFunction> _logger;
        private readonly string _connectionString;

        public GmailStatusFunction(ILogger<GmailStatusFunction> logger, IConfiguration configuration)
        {
            _logger = logger;
            _connectionString = configuration["SqlConnectionString"]
                ?? throw new InvalidOperationException("Connection string not found");
        }

        [Function("GmailStatus")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "integrations/{userId}/gmail")] HttpRequestData req,
            string userId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "SELECT Email FROM UserIntegrations WHERE UserId = @UserId AND Provider = 'gmail'";
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@UserId", Guid.Parse(userId));

                var email = await command.ExecuteScalarAsync() as string;

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    connected = !string.IsNullOrEmpty(email),
                    email = email ?? ""
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Gmail status for user {UserId}", userId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { connected = false, email = "" });
                return error;
            }
        }
    }
}