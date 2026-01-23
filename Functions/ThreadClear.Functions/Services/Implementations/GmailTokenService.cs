using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThreadClear.Functions.Services
{
    public class GmailTokenService
    {
        private readonly ILogger<GmailTokenService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public GmailTokenService(ILogger<GmailTokenService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _connectionString = configuration["SqlConnectionString"] 
                ?? throw new InvalidOperationException("SqlConnectionString not configured");
        }

        /// <summary>
        /// Get all users with Gmail connected for digest processing
        /// </summary>
        public async Task<List<GmailUser>> GetAllGmailUsersAsync()
        {
            var users = new List<GmailUser>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
        SELECT u.Id, u.Email, u.DisplayName, 
               ui.AccessToken, ui.RefreshToken, ui.TokenExpiry, ui.Email as GmailEmail
        FROM Users u
        INNER JOIN UserIntegrations ui ON u.Id = ui.UserId
        WHERE ui.Provider = 'gmail'";

            using var command = new SqlCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                users.Add(new GmailUser
                {
                    UserId = reader.GetGuid(0),
                    Email = reader.GetString(1),
                    DisplayName = reader.IsDBNull(2) ? null : reader.GetString(2),
                    AccessToken = reader.GetString(3),
                    RefreshToken = reader.IsDBNull(4) ? null : reader.GetString(4),
                    TokenExpiry = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    GmailEmail = reader.IsDBNull(6) ? null : reader.GetString(6)
                });
            }

            return users;
        }

        /// <summary>
        /// Refresh an expired access token using the refresh token
        /// </summary>
        public async Task<string?> RefreshAccessTokenAsync(GmailUser user)
        {
            if (string.IsNullOrEmpty(user.RefreshToken))
            {
                _logger.LogWarning("No refresh token for user {UserId}", user.UserId);
                return null;
            }

            var clientId = _configuration["GoogleClientId"];
            var clientSecret = _configuration["GoogleClientSecret"];

            using var httpClient = new HttpClient();
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId!,
                ["client_secret"] = clientSecret!,
                ["refresh_token"] = user.RefreshToken,
                ["grant_type"] = "refresh_token"
            });

            var response = await httpClient.PostAsync("https://oauth2.googleapis.com/token", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Token refresh failed for user {UserId}: {Error}", user.UserId, error);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json);

            if (tokenResponse?.AccessToken == null)
            {
                _logger.LogError("Invalid token response for user {UserId}", user.UserId);
                return null;
            }

            // Update token in database
            await UpdateAccessTokenAsync(user.UserId, tokenResponse.AccessToken, tokenResponse.ExpiresIn);

            return tokenResponse.AccessToken;
        }

        /// <summary>
        /// Get a valid access token, refreshing if necessary
        /// </summary>
        public async Task<string?> GetValidAccessTokenAsync(GmailUser user)
        {
            // Check if current token is still valid (with 5 min buffer)
            if (user.TokenExpiry.HasValue && user.TokenExpiry.Value > DateTime.UtcNow.AddMinutes(5))
            {
                return user.AccessToken;
            }

            _logger.LogInformation("Token expired for user {UserId}, refreshing...", user.UserId);
            return await RefreshAccessTokenAsync(user);
        }

        private async Task UpdateAccessTokenAsync(Guid userId, string accessToken, int expiresIn)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                UPDATE UserIntegrations 
                SET AccessToken = @AccessToken, 
                    TokenExpiry = @TokenExpiry,
                    UpdatedAt = GETUTCDATE()
                WHERE UserId = @UserId AND Provider = 'gmail'";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@AccessToken", accessToken);
            command.Parameters.AddWithValue("@TokenExpiry", DateTime.UtcNow.AddSeconds(expiresIn));
            command.Parameters.AddWithValue("@UserId", userId);

            await command.ExecuteNonQueryAsync();
        }

        private class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("token_type")]
            public string? TokenType { get; set; }
        }
    }

    public class GmailUser
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = "";
        public string? DisplayName { get; set; }
        public string AccessToken { get; set; } = "";
        public string? RefreshToken { get; set; }
        public DateTime? TokenExpiry { get; set; }
        public string? GmailEmail { get; set; }
    }
}
