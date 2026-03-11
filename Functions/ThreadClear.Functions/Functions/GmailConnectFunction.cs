using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Web;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services;
using ThreadClear.Functions.Services.Implementations;

namespace ThreadClear.Functions.Functions
{
    public class GmailConnectFunction
    {
        private readonly ILogger<GmailConnectFunction> _logger;
        private readonly IConfiguration _configuration;

        public GmailConnectFunction(ILogger<GmailConnectFunction> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [Function("GmailConnect")]
        public HttpResponseData Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "gmail/connect")] HttpRequestData req)
        {
            var clientId = _configuration["GoogleClientId"];
            var redirectUri = _configuration["GoogleRedirectUri"];

            // Get user ID from query string (frontend passes this)
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var userId = query["userId"] ?? "";
            var state = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userId));

            var scopes = HttpUtility.UrlEncode("https://www.googleapis.com/auth/gmail.readonly https://www.googleapis.com/auth/userinfo.email");

            var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={clientId}" +
                $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
                $"&response_type=code" +
                $"&scope={scopes}" +
                $"&access_type=offline" +
                $"&prompt=consent" +
                $"&state={state}";

            _logger.LogInformation("Redirecting user {UserId} to Google OAuth", userId);

            var response = req.CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Add("Location", authUrl);
            return response;
        }
        [Function("GmailCallback")]
        public async Task<HttpResponseData> Callback(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get",
        Route = "gmail/callback")] HttpRequestData req)
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var code = query["code"];
            var state = query["state"];
            var error = query["error"];

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Gmail OAuth error: {Error}", error);
                return RedirectToApp(req, "gmail-error");
            }

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                return req.CreateResponse(HttpStatusCode.BadRequest);

            // Decode base64 state back to userId
            string userId;
            try
            {
                userId = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(state));
            }
            catch
            {
                _logger.LogWarning("Invalid base64 state in Gmail callback");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            if (!Guid.TryParse(userId, out var userGuid))
            {
                _logger.LogWarning("Invalid userId in Gmail callback state: {State}", userId);
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            // ── Exchange code for tokens ──────────────────────────────────────
            var clientId = _configuration["GoogleClientId"];
            var clientSecret = _configuration["GoogleClientSecret"];
            var redirectUri = _configuration["GoogleRedirectUri"];

            using var httpClient = new System.Net.Http.HttpClient();
            var tokenBody = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string, string>("grant_type", "authorization_code"),
        new KeyValuePair<string, string>("client_id", clientId!),
        new KeyValuePair<string, string>("client_secret", clientSecret!),
        new KeyValuePair<string, string>("code", code),
        new KeyValuePair<string, string>("redirect_uri", redirectUri!)
    });

            System.Net.Http.HttpResponseMessage tokenResponse;
            try
            {
                tokenResponse = await httpClient.PostAsync(
                    "https://oauth2.googleapis.com/token", tokenBody);
                tokenResponse.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gmail token exchange failed for user {UserId}", userGuid);
                return RedirectToApp(req, "gmail-error");
            }

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            using var tokenDoc = JsonDocument.Parse(tokenJson);
            var tokenRoot = tokenDoc.RootElement;

            var accessToken = tokenRoot.GetProperty("access_token").GetString()!;
            var refreshToken = tokenRoot.GetProperty("refresh_token").GetString()!;
            var expiresIn = tokenRoot.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

            // ── Fetch Gmail profile ───────────────────────────────────────────
            string gmailEmail = "";
            string gmailUserId = "";
            try
            {
                var profileRequest = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Get,
                    "https://www.googleapis.com/oauth2/v2/userinfo");
                profileRequest.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var profileResponse = await httpClient.SendAsync(profileRequest);
                profileResponse.EnsureSuccessStatusCode();

                using var profileDoc = JsonDocument.Parse(
                    await profileResponse.Content.ReadAsStringAsync());
                var profile = profileDoc.RootElement;

                gmailEmail = profile.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "";
                gmailUserId = profile.TryGetProperty("id", out var gid) ? gid.GetString() ?? "" : "";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch Gmail profile for user {UserId}", userGuid);
            }

            // ── Store tokens ──────────────────────────────────────────────────
            var sqlConnectionString = _configuration["SqlConnectionString"]!;
            var tokenRepo = new GmailTokenRepository(sqlConnectionString,
                Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddConsole())
                    .CreateLogger<GmailTokenRepository>());

            var tokenRecord = new UserGmailToken
            {
                UserId = userGuid,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
                GmailUserId = gmailUserId,
                GmailUserEmail = gmailEmail
            };

            await tokenRepo.UpsertAsync(tokenRecord);
            _logger.LogInformation("Stored Gmail token for user {UserId} ({Email})", userGuid, gmailEmail);

            // ── Setup Gmail watch ─────────────────────────────────────────────
            try
            {
                var gmailService = new GmailService(
                    Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddConsole())
                        .CreateLogger<GmailService>(), httpClient);

                var pubSubTopic = _configuration["GooglePubSubTopic"]!;
                var watchResult = await gmailService.SetupWatchAsync(accessToken, pubSubTopic);
                await tokenRepo.UpsertWatchAsync(userGuid, watchResult.HistoryId, watchResult.Expiration);

                _logger.LogInformation("Gmail watch created for user {UserId}, historyId {HistoryId}",
                    userGuid, watchResult.HistoryId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Gmail watch for user {UserId}", userGuid);
            }

            return RedirectToApp(req, "gmail-connected");
        }

        private static HttpResponseData RedirectToApp(HttpRequestData req, string status)
        {
            var response = req.CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Add("Location",
                $"https://app.threadclear.com/settings?integration={status}");
            return response;
        }

    }
}