using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class OutlookConnectFunction
    {
        private readonly IGraphService _graphService;
        private readonly IGraphTokenRepository _graphTokenRepo;
        private readonly IUserService _userService;
        private readonly IConfiguration _config;
        private readonly ILogger<OutlookConnectFunction> _logger;
        private readonly HttpClient _httpClient;

        public OutlookConnectFunction(
            IGraphService graphService,
            IGraphTokenRepository graphTokenRepo,
            IUserService userService,
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<OutlookConnectFunction> logger)
        {
            _graphService = graphService;
            _graphTokenRepo = graphTokenRepo;
            _userService = userService;
            _config = config;
            _httpClient = httpClientFactory.CreateClient("graph");
            _logger = logger;
        }

        // ── Step 1: Redirect user to Microsoft login ─────────────────────────
        [Function("OutlookConnect")]
        public HttpResponseData Connect(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get",
                Route = "outlook/connect")] HttpRequestData req)
        {
            // Expect ?userId=<ThreadClear userId> so we can tie the token back
            var userId = req.Query["userId"];
            if (string.IsNullOrEmpty(userId))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                bad.WriteString("userId is required");
                return bad;
            }

            var clientId = _config["Graph:ClientId"];
            var tenantId = _config["Graph:TenantId"];
            var redirectUri = _config["Graph:OAuthRedirectUri"];

            var authUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize" +
                $"?client_id={clientId}" +
                $"&response_type=code" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri!)}" +
                $"&scope={Uri.EscapeDataString("https://graph.microsoft.com/Mail.Read offline_access User.Read")}" +
                $"&state={Uri.EscapeDataString(userId)}" +  // pass userId through state
                $"&prompt=consent";                          // force refresh_token issuance

            var response = req.CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Add("Location", authUrl);
            return response;
        }

        // ── Step 2: Exchange auth code for tokens, store, create subscription ─
        [Function("OutlookCallback")]
        public async Task<HttpResponseData> Callback(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get",
                Route = "outlook/callback")] HttpRequestData req)
        {
            var code = req.Query["code"];
            var state = req.Query["state"]; // userId we passed through
            var error = req.Query["error"];

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("OAuth error: {Error}", error);
                return RedirectToApp(req, "outlook-error");
            }

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            if (!Guid.TryParse(state, out var userId))
            {
                _logger.LogWarning("Invalid userId in OAuth state: {State}", state);
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            // ── Exchange code for tokens ─────────────────────────────────────
            var clientId = _config["Graph:ClientId"];
            var clientSecret = _config["Graph:ClientSecret"];
            var tenantId = _config["Graph:TenantId"];
            var redirectUri = _config["Graph:OAuthRedirectUri"];

            var tokenUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

            var tokenBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("client_id", clientId!),
                new KeyValuePair<string, string>("client_secret", clientSecret!),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri!),
                new KeyValuePair<string, string>("scope",
                    "https://graph.microsoft.com/Mail.Read offline_access User.Read")
            });

            HttpResponseMessage tokenResponse;
            try
            {
                tokenResponse = await _httpClient.PostAsync(tokenUrl, tokenBody);
                tokenResponse.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token exchange failed for user {UserId}", userId);
                return RedirectToApp(req, "outlook-error");
            }

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            using var tokenDoc = JsonDocument.Parse(tokenJson);
            var root = tokenDoc.RootElement;

            var accessToken = root.GetProperty("access_token").GetString()!;
            var refreshToken = root.GetProperty("refresh_token").GetString()!;
            var expiresIn = root.TryGetProperty("expires_in", out var exp)
                ? exp.GetInt32() : 3600;

            // ── Fetch Graph user profile ──────────────────────────────────────
            string graphUserId = "";
            string graphUserEmail = "";
            try
            {
                var profileRequest = new HttpRequestMessage(HttpMethod.Get,
                    "https://graph.microsoft.com/v1.0/me?$select=id,mail,userPrincipalName");
                profileRequest.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var profileResponse = await _httpClient.SendAsync(profileRequest);
                profileResponse.EnsureSuccessStatusCode();

                var profileJson = await profileResponse.Content.ReadAsStringAsync();
                using var profileDoc = JsonDocument.Parse(profileJson);
                var profile = profileDoc.RootElement;

                graphUserId = profile.TryGetProperty("id", out var gid)
                    ? gid.GetString() ?? "" : "";
                graphUserEmail = profile.TryGetProperty("mail", out var mail)
                    ? mail.GetString() ?? ""
                    : profile.TryGetProperty("userPrincipalName", out var upn)
                        ? upn.GetString() ?? "" : "";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch Graph profile for user {UserId}", userId);
            }

            // ── Store tokens ──────────────────────────────────────────────────
            var tokenRecord = new UserGraphToken
            {
                UserId = userId,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
                GraphUserId = graphUserId,
                GraphUserEmail = graphUserEmail
            };

            await _graphTokenRepo.UpsertAsync(tokenRecord);
            _logger.LogInformation("Stored Graph token for user {UserId} ({Email})",
                userId, graphUserEmail);

            // ── Create Graph subscription ─────────────────────────────────────
            try
            {
                var subscriptionId = await _graphService.CreateSubscriptionAsync(accessToken, userId);
                var expiresAt = DateTime.UtcNow.AddDays(2);
                await _graphTokenRepo.UpsertSubscriptionAsync(userId, subscriptionId, expiresAt);

                _logger.LogInformation(
                    "Created Graph subscription {SubscriptionId} for user {UserId}",
                    subscriptionId, userId);
            }
            catch (Exception ex)
            {
                // Non-fatal — token is stored, subscription can be retried
                _logger.LogError(ex, "Failed to create Graph subscription for user {UserId}", userId);
            }

            // ── Redirect back to app ──────────────────────────────────────────
            return RedirectToApp(req, "outlook-connected");
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