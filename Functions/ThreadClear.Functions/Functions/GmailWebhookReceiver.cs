using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services;
using ThreadClear.Functions.Services.Interfaces;
using ThreadClear.Models;

namespace ThreadClear.Functions.Functions
{
    public class GmailWebhookReceiver
    {
        private readonly IGmailService _gmailService;
        private readonly IGmailTokenRepository _gmailTokenRepo;
        private readonly IConfiguration _config;
        private readonly ILogger<GmailWebhookReceiver> _logger;

        public GmailWebhookReceiver(
            IGmailService gmailService,
            IGmailTokenRepository gmailTokenRepo,
            IConfiguration config,
            ILogger<GmailWebhookReceiver> logger)
        {
            _gmailService = gmailService;
            _gmailTokenRepo = gmailTokenRepo;
            _config = config;
            _logger = logger;
        }

        [Function("GmailWebhookReceiver")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post",
                Route = "gmail/webhook")] HttpRequestData req)
        {
            var token = req.Query["token"];
            var expectedToken = _config["GoogleWebhookSecret"];
            if (token != expectedToken)
            {
                _logger.LogWarning("Gmail webhook received invalid token");
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            string body;
            try
            {
                body = await req.ReadAsStringAsync() ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read Gmail webhook body");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            _ = ProcessNotificationAsync(body);

            return req.CreateResponse(HttpStatusCode.OK);
        }

        private async Task ProcessNotificationAsync(string body)
        {
            JsonDocument envelope;
            try
            {
                envelope = JsonDocument.Parse(body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Gmail Pub/Sub envelope");
                return;
            }

            using (envelope)
            {
                if (!envelope.RootElement.TryGetProperty("message", out var message) ||
                    !message.TryGetProperty("data", out var dataProp))
                {
                    _logger.LogWarning("Gmail webhook missing message.data");
                    return;
                }

                var base64Data = dataProp.GetString();
                if (string.IsNullOrEmpty(base64Data))
                {
                    _logger.LogWarning("Gmail webhook data is empty");
                    return;
                }

                string dataJson;
                try
                {
                    var bytes = Convert.FromBase64String(base64Data);
                    dataJson = Encoding.UTF8.GetString(bytes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to decode Gmail Pub/Sub data");
                    return;
                }

                JsonDocument dataDoc;
                try
                {
                    dataDoc = JsonDocument.Parse(dataJson);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse Gmail Pub/Sub data JSON: {Data}", dataJson);
                    return;
                }

                using (dataDoc)
                {
                    var emailAddress = dataDoc.RootElement.TryGetProperty("emailAddress", out var ea)
                        ? ea.GetString() ?? "" : "";
                    var newHistoryId = dataDoc.RootElement.TryGetProperty("historyId", out var hi)
                        ? hi.GetString() ?? "" : "";

                    if (string.IsNullOrEmpty(emailAddress))
                    {
                        _logger.LogWarning("Gmail webhook missing emailAddress");
                        return;
                    }

                    await ProcessForUserAsync(emailAddress, newHistoryId);
                }
            }
        }

        private async Task ProcessForUserAsync(string emailAddress, string newHistoryId)
        {
            var tokenRecord = await _gmailTokenRepo.GetByEmailAsync(emailAddress);
            if (tokenRecord == null)
            {
                _logger.LogWarning("No token found for Gmail email {Email}", emailAddress);
                return;
            }

            var storedHistoryId = await _gmailTokenRepo.GetHistoryIdAsync(tokenRecord.UserId);
            if (string.IsNullOrEmpty(storedHistoryId))
            {
                _logger.LogInformation(
                    "No stored historyId for user {UserId}, storing {HistoryId}",
                    tokenRecord.UserId, newHistoryId);
                await _gmailTokenRepo.UpsertWatchAsync(tokenRecord.UserId, newHistoryId,
                    DateTime.UtcNow.AddDays(7));
                return;
            }

            var clientId = _config["GoogleClientId"]!;
            var clientSecret = _config["GoogleClientSecret"]!;

            string accessToken;
            try
            {
                accessToken = await _gmailService.RefreshAccessTokenAsync(
                    tokenRecord.RefreshToken, clientId, clientSecret);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh Gmail token for user {UserId}",
                    tokenRecord.UserId);
                return;
            }

            GmailHistoryResult historyResult;
            try
            {
                historyResult = await _gmailService.GetNewMessageIdsAsync(
                    accessToken, storedHistoryId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch Gmail history for user {UserId}",
                    tokenRecord.UserId);
                return;
            }

            if (!string.IsNullOrEmpty(historyResult.LatestHistoryId))
            {
                await _gmailTokenRepo.UpsertWatchAsync(
                    tokenRecord.UserId,
                    historyResult.LatestHistoryId,
                    DateTime.UtcNow.AddDays(7));
            }

            if (historyResult.NewMessageIds.Count == 0)
            {
                _logger.LogInformation("No new messages for user {UserId}", tokenRecord.UserId);
                return;
            }

            foreach (var messageId in historyResult.NewMessageIds)
            {
                try
                {
                    var email = await _gmailService.GetEmailMetadataAsync(accessToken, messageId);

                    if (string.IsNullOrWhiteSpace(email.BodyText))
                    {
                        _logger.LogInformation("Gmail message {MessageId} has no body, skipping",
                            messageId);
                        continue;
                    }

                    var queuedMessage = new QueuedEmailMessage
                    {
                        UserId = tokenRecord.UserId,
                        Provider = "gmail",
                        ThreadId = messageId,
                        MessageId = messageId,
                        Subject = email.Subject,
                        BodyText = email.BodyText,
                        ReceivedAt = email.ReceivedAt,
                        QueuedAt = DateTime.UtcNow
                    };

                    await EnqueueAsync(queuedMessage);

                    _logger.LogInformation(
                        "Queued Gmail message {MessageId} for user {UserId}",
                        messageId, tokenRecord.UserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process Gmail message {MessageId}", messageId);
                }
            }
        }

        private async Task EnqueueAsync(QueuedEmailMessage message)
        {
            var connectionString = _config["AzureWebJobsStorage"];
            var queueClient = new QueueClient(connectionString, "threadclear-digest-queue",
                new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });

            await queueClient.CreateIfNotExistsAsync();
            await queueClient.SendMessageAsync(JsonSerializer.Serialize(message));
        }
    }
}