using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    public class GraphService : IGraphService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<GraphService> _logger;
        private readonly HttpClient _httpClient;

        public GraphService(IConfiguration config, ILogger<GraphService> logger, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("graph");
        }

        public async Task<string> RefreshAccessTokenAsync(string refreshToken)
        {
            var tenantId = _config["Graph:TenantId"];
            var clientId = _config["Graph:ClientId"];
            var clientSecret = _config["Graph:ClientSecret"];

            var tokenUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

            var body = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("client_id", clientId!),
                new KeyValuePair<string, string>("client_secret", clientSecret!),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("scope", "https://graph.microsoft.com/Mail.Read offline_access")
            });

            var response = await _httpClient.PostAsync(tokenUrl, body);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("access_token").GetString()!;
        }

        public async Task<GraphEmailMetadata> GetEmailMetadataAsync(string accessToken, string messageId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://graph.microsoft.com/v1.0/me/messages/{messageId}" +
                "?$select=id,subject,body,from,receivedDateTime,conversationId");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var bodyText = "";
            if (root.TryGetProperty("body", out var body))
            {
                bodyText = body.TryGetProperty("content", out var content)
                    ? content.GetString() ?? ""
                    : "";
                // Strip HTML if content type is html
                if (body.TryGetProperty("contentType", out var ct) &&
                    ct.GetString() == "html")
                {
                    bodyText = StripHtml(bodyText);
                }
            }

            var fromEmail = "";
            if (root.TryGetProperty("from", out var from) &&
                from.TryGetProperty("emailAddress", out var emailAddr) &&
                emailAddr.TryGetProperty("address", out var addr))
            {
                fromEmail = addr.GetString() ?? "";
            }

            return new GraphEmailMetadata
            {
                MessageId = messageId,
                Subject = root.TryGetProperty("subject", out var subj)
                    ? subj.GetString() ?? "" : "",
                BodyText = bodyText,
                FromEmail = fromEmail,
                ReceivedAt = root.TryGetProperty("receivedDateTime", out var dt)
                    ? dt.GetDateTime() : DateTime.UtcNow
            };
        }

        public async Task<string> GetEmailBodyAsync(string accessToken, string messageId)
        {
            var metadata = await GetEmailMetadataAsync(accessToken, messageId);
            return metadata.BodyText;
        }

        public async Task<string> CreateSubscriptionAsync(string accessToken, Guid userId)
        {
            var webhookSecret = _config["Graph:WebhookSecret"];
            var notificationUrl = _config["Graph:WebhookUrl"]
                ?? throw new InvalidOperationException("Graph:WebhookUrl is required");

            var subscription = new
            {
                changeType = "created",
                notificationUrl,
                resource = "me/mailFolders('Inbox')/messages",
                expirationDateTime = DateTime.UtcNow.AddDays(2).ToString("o"),
                clientState = webhookSecret
            };

            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://graph.microsoft.com/v1.0/subscriptions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(
                JsonSerializer.Serialize(subscription),
                Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("id").GetString()!;
        }

        public async Task RenewSubscriptionAsync(string accessToken, string subscriptionId)
        {
            var body = new { expirationDateTime = DateTime.UtcNow.AddDays(2).ToString("o") };

            var request = new HttpRequestMessage(HttpMethod.Patch,
                $"https://graph.microsoft.com/v1.0/subscriptions/{subscriptionId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Renewed Graph subscription {SubscriptionId}", subscriptionId);
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";
            // Simple tag stripper — good enough for email body text extraction
            var result = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ");
            return result.Trim();
        }
    }
}