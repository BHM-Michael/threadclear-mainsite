using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;
using ThreadClear.Models;

namespace ThreadClear.Functions.Functions
{
    public class OutlookWebhookReceiver
    {
        private readonly IGraphService _graphService;
        private readonly IGraphTokenRepository _graphTokenRepo;
        private readonly IConfiguration _config;
        private readonly ILogger<OutlookWebhookReceiver> _logger;

        public OutlookWebhookReceiver(
            IGraphService graphService,
            IGraphTokenRepository graphTokenRepo,
            IConfiguration config,
            ILogger<OutlookWebhookReceiver> logger)
        {
            _graphService = graphService;
            _graphTokenRepo = graphTokenRepo;
            _config = config;
            _logger = logger;
        }

        [Function("OutlookWebhookReceiver")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                Route = "outlook/webhook")] HttpRequestData req)
        {
            // ── Step 1: Graph validation handshake ──────────────────────────
            // When you first create a subscription, Graph sends a GET with
            // ?validationToken=xxx and expects it echoed back as plain text
            var validationToken = req.Query["validationToken"];
            if (!string.IsNullOrEmpty(validationToken))
            {
                _logger.LogInformation("Graph webhook validation handshake received");
                var validationResponse = req.CreateResponse(HttpStatusCode.OK);
                validationResponse.Headers.Add("Content-Type", "text/plain");
                await validationResponse.WriteStringAsync(validationToken);
                return validationResponse;
            }

            // ── Step 2: Parse notification payload ──────────────────────────
            string body;
            try
            {
                body = await req.ReadAsStringAsync() ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read webhook request body");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            // Always return 202 quickly — Graph will retry if you're slow
            // Process asynchronously after responding
            _ = ProcessNotificationsAsync(body);

            return req.CreateResponse(HttpStatusCode.Accepted);
        }

        private async Task ProcessNotificationsAsync(string body)
        {
            var webhookSecret = _config["Graph:WebhookSecret"];

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse webhook notification JSON");
                return;
            }

            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("value", out var notifications))
                {
                    _logger.LogWarning("Webhook body missing 'value' array");
                    return;
                }

                foreach (var notification in notifications.EnumerateArray())
                {
                    try
                    {
                        await ProcessSingleNotification(notification, webhookSecret!);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process notification");
                    }
                }
            }
        }

        private async Task ProcessSingleNotification(JsonElement notification, string webhookSecret)
        {
            // ── Step 3: Verify clientState ───────────────────────────────────
            if (!notification.TryGetProperty("clientState", out var clientStateProp) ||
                clientStateProp.GetString() != webhookSecret)
            {
                _logger.LogWarning("Webhook notification failed clientState validation — ignoring");
                return;
            }

            // ── Step 4: Extract Graph user ID and message ID ─────────────────
            // resource format: "users/{graphUserId}/messages/{messageId}"
            if (!notification.TryGetProperty("resource", out var resourceProp))
            {
                _logger.LogWarning("Notification missing resource property");
                return;
            }

            var resource = resourceProp.GetString() ?? "";
            var parts = resource.Split('/');
            if (parts.Length < 4)
            {
                _logger.LogWarning("Unexpected resource format: {Resource}", resource);
                return;
            }

            var graphUserId = parts[1];
            var messageId = parts[3];

            // ── Step 5: Look up ThreadClear user by Graph user ID ────────────
            var tokenRecord = await _graphTokenRepo.GetByGraphUserIdAsync(graphUserId);
            if (tokenRecord == null)
            {
                _logger.LogWarning("No token found for Graph user {GraphUserId}", graphUserId);
                return;
            }

            // ── Step 6: Refresh access token ─────────────────────────────────
            string accessToken;
            try
            {
                accessToken = await _graphService.RefreshAccessTokenAsync(tokenRecord.RefreshToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh token for user {UserId}", tokenRecord.UserId);
                return;
            }

            // ── Step 7: Fetch email content ──────────────────────────────────
            GraphEmailMetadata email;
            try
            {
                email = await _graphService.GetEmailMetadataAsync(accessToken, messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch email {MessageId}", messageId);
                return;
            }

            if (string.IsNullOrWhiteSpace(email.BodyText))
            {
                _logger.LogInformation("Email {MessageId} has no body text, skipping", messageId);
                return;
            }

            // ── Step 8: Drop onto digest queue ───────────────────────────────
            var queuedMessage = new QueuedEmailMessage
            {
                UserId = tokenRecord.UserId,
                Provider = "outlook",
                ThreadId = messageId,
                MessageId = messageId,
                Subject = email.Subject,
                BodyText = email.BodyText,
                ReceivedAt = email.ReceivedAt,
                QueuedAt = DateTime.UtcNow
            };

            await EnqueueAsync(queuedMessage);

            _logger.LogInformation(
                "Queued Outlook email {MessageId} for user {UserId}",
                messageId, tokenRecord.UserId);
        }

        private async Task EnqueueAsync(QueuedEmailMessage message)
        {
            var connectionString = _config["AzureWebJobsStorage"];
            var queueClient = new QueueClient(connectionString, "threadclear-digest-queue",
                new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });

            await queueClient.CreateIfNotExistsAsync();

            var json = JsonSerializer.Serialize(message);
            await queueClient.SendMessageAsync(json);
        }
    }
}