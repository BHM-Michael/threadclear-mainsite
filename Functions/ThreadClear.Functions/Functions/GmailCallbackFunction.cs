using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThreadClear.Functions.Functions
{
    public class GmailCallbackFunction
    {
        private readonly ILogger<GmailCallbackFunction> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public GmailCallbackFunction(ILogger<GmailCallbackFunction> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _connectionString = configuration["SqlConnectionString"] ?? throw new InvalidOperationException("Connection string not found");
        }

        [Function("GmailCallback")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "gmail/callback")] HttpRequestData req)
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var code = query["code"] ?? "";
            var state = query["state"] ?? "";
            var error = query["error"] ?? "";

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError("Gmail OAuth error: {Error}", error);
                return CreateRedirect(req, "https://app.threadclear.com/profile?gmail=error");
            }

            if (string.IsNullOrEmpty(code))
            {
                return CreateRedirect(req, "https://app.threadclear.com/profile?gmail=error");
            }

            // Decode user ID from state
            var userId = Encoding.UTF8.GetString(Convert.FromBase64String(state));

            try
            {
                // Exchange code for tokens
                var tokens = await ExchangeCodeForTokens(code);

                // Get user's Gmail address
                var gmailEmail = await GetGmailEmail(tokens.AccessToken);

                // Save to database
                await SaveUserIntegration(Guid.Parse(userId), tokens, gmailEmail);

                _logger.LogInformation("Gmail connected for user {UserId}: {Email}", userId, gmailEmail);

                return CreateRedirect(req, "https://app.threadclear.com/profile?gmail=connected");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete Gmail OAuth for user {UserId}", userId);
                return CreateRedirect(req, "https://app.threadclear.com/profile?gmail=error");
            }
        }

        private HttpResponseData CreateRedirect(HttpRequestData req, string url)
        {
            var response = req.CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Add("Location", url);
            return response;
        }

        private async Task<TokenResponse> ExchangeCodeForTokens(string code)
        {
            using var client = new HttpClient();
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _configuration["GoogleClientId"]!,
                ["client_secret"] = _configuration["GoogleClientSecret"]!,
                ["redirect_uri"] = _configuration["GoogleRedirectUri"]!,
                ["grant_type"] = "authorization_code"
            });

            var response = await client.PostAsync("https://oauth2.googleapis.com/token", content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Token exchange failed: {json}");
            }

            return JsonSerializer.Deserialize<TokenResponse>(json)!;
        }

        private async Task<string> GetGmailEmail(string accessToken)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("email").GetString() ?? "";
        }

        private async Task SaveUserIntegration(Guid userId, TokenResponse tokens, string email)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                MERGE UserIntegrations AS target
                USING (SELECT @UserId AS UserId, @Provider AS Provider) AS source
                ON target.UserId = source.UserId AND target.Provider = source.Provider
                WHEN MATCHED THEN
                    UPDATE SET 
                        Email = @Email,
                        AccessToken = @AccessToken,
                        RefreshToken = @RefreshToken,
                        TokenExpiry = @TokenExpiry,
                        Scopes = @Scopes,
                        UpdatedAt = GETUTCDATE()
                WHEN NOT MATCHED THEN
                    INSERT (UserId, Provider, Email, AccessToken, RefreshToken, TokenExpiry, Scopes)
                    VALUES (@UserId, @Provider, @Email, @AccessToken, @RefreshToken, @TokenExpiry, @Scopes);";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Provider", "gmail");
            command.Parameters.AddWithValue("@Email", email);
            command.Parameters.AddWithValue("@AccessToken", tokens.AccessToken);
            command.Parameters.AddWithValue("@RefreshToken", tokens.RefreshToken ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@TokenExpiry", DateTime.UtcNow.AddSeconds(tokens.ExpiresIn));
            command.Parameters.AddWithValue("@Scopes", "gmail.readonly userinfo.email");

            await command.ExecuteNonQueryAsync();
        }

        private class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = "";

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }
        }
    }
}