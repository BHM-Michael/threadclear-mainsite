using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services
{
    public class GmailService
    {
        private readonly ILogger<GmailService> _logger;
        private readonly HttpClient _httpClient;
        private const string GmailApiBase = "https://gmail.googleapis.com/gmail/v1/users/me";

        public GmailService(ILogger<GmailService> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        /// <summary>
        /// List threads from the last N hours
        /// </summary>
        public async Task<List<GmailThread>> ListRecentThreadsAsync(string accessToken, int hoursBack = 24, int maxResults = 50)
        {
            var threads = new List<GmailThread>();
            var afterTimestamp = DateTimeOffset.UtcNow.AddHours(-hoursBack).ToUnixTimeSeconds();
            var query = $"after:{afterTimestamp}";

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var url = $"{GmailApiBase}/threads?q={Uri.EscapeDataString(query)}&maxResults={maxResults}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to list threads: {Error}", error);
                return threads;
            }

            var json = await response.Content.ReadAsStringAsync();
            var listResponse = JsonSerializer.Deserialize<ThreadListResponse>(json);

            if (listResponse?.Threads == null)
                return threads;

            foreach (var threadStub in listResponse.Threads)
            {
                var fullThread = await GetThreadAsync(accessToken, threadStub.Id);
                if (fullThread != null)
                    threads.Add(fullThread);
            }

            return threads;
        }

        /// <summary>
        /// Get a full thread with all messages
        /// </summary>
        public async Task<GmailThread?> GetThreadAsync(string accessToken, string threadId)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var url = $"{GmailApiBase}/threads/{threadId}?format=full";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get thread {ThreadId}: {Error}", threadId, error);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GmailThread>(json);
        }

        /// <summary>
        /// Get a full thread by message ID (used by Gmail Add-on trigger)
        /// </summary>
        public async Task<GmailThread?> GetThreadByMessageIdAsync(string accessToken, string messageId)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // First get the message to find its threadId
            var msgUrl = $"{GmailApiBase}/messages/{messageId}?format=minimal";
            var msgResponse = await _httpClient.GetAsync(msgUrl);

            if (!msgResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get message {MessageId}", messageId);
                return null;
            }

            var msgJson = await msgResponse.Content.ReadAsStringAsync();
            var message = JsonSerializer.Deserialize<GmailMessage>(msgJson);

            if (string.IsNullOrEmpty(message?.ThreadId))
                return null;

            // Now get the full thread
            return await GetThreadAsync(accessToken, message.ThreadId);
        }

        /// <summary>
        /// Convert a Gmail thread to a conversation string for analysis
        /// </summary>
        public string ConvertThreadToConversation(GmailThread thread)
        {
            var sb = new StringBuilder();
            var subject = GetHeader(thread.Messages?.FirstOrDefault()?.Payload, "Subject") ?? "(No Subject)";
            sb.AppendLine($"Subject: {subject}");
            sb.AppendLine();

            if (thread.Messages == null) return sb.ToString();

            foreach (var message in thread.Messages.OrderBy(m => long.Parse(m.InternalDate ?? "0")))
            {
                var from = GetHeader(message.Payload, "From") ?? "Unknown";
                var date = GetHeader(message.Payload, "Date") ?? "";
                var body = ExtractBody(message.Payload);

                // Extract just the name/email from "Name <email@example.com>"
                var sender = ExtractSenderName(from);

                sb.AppendLine($"[{sender}] ({date}):");
                sb.AppendLine(body);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string? GetHeader(GmailPayload? payload, string name)
        {
            return payload?.Headers?.FirstOrDefault(h => 
                h.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        private string ExtractBody(GmailPayload? payload)
        {
            if (payload == null) return "";

            // Try direct body first
            if (!string.IsNullOrEmpty(payload.Body?.Data))
            {
                return DecodeBase64(payload.Body.Data);
            }

            // Check parts for text/plain or text/html
            if (payload.Parts != null)
            {
                // Prefer text/plain
                var textPart = payload.Parts.FirstOrDefault(p => p.MimeType == "text/plain");
                if (textPart?.Body?.Data != null)
                    return DecodeBase64(textPart.Body.Data);

                // Fall back to text/html
                var htmlPart = payload.Parts.FirstOrDefault(p => p.MimeType == "text/html");
                if (htmlPart?.Body?.Data != null)
                    return StripHtml(DecodeBase64(htmlPart.Body.Data));

                // Recurse into nested parts
                foreach (var part in payload.Parts)
                {
                    var nested = ExtractBody(part);
                    if (!string.IsNullOrEmpty(nested))
                        return nested;
                }
            }

            return "";
        }

        private string DecodeBase64(string encoded)
        {
            try
            {
                // Gmail uses URL-safe base64
                var base64 = encoded.Replace('-', '+').Replace('_', '/');
                var padded = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
                var bytes = Convert.FromBase64String(padded);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return encoded;
            }
        }

        private string StripHtml(string html)
        {
            // Basic HTML stripping - consider using HtmlAgilityPack for production
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            return System.Net.WebUtility.HtmlDecode(text).Trim();
        }

        private string ExtractSenderName(string from)
        {
            // "John Doe <john@example.com>" -> "John Doe"
            var match = System.Text.RegularExpressions.Regex.Match(from, @"^([^<]+)<");
            if (match.Success)
                return match.Groups[1].Value.Trim().Trim('"');
            return from;
        }

        /// <summary>
        /// Refresh an access token using a refresh token
        /// </summary>
        public async Task<string> RefreshAccessTokenAsync(string refreshToken, string clientId, string clientSecret)
        {
            var body = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string, string>("grant_type", "refresh_token"),
        new KeyValuePair<string, string>("client_id", clientId),
        new KeyValuePair<string, string>("client_secret", clientSecret),
        new KeyValuePair<string, string>("refresh_token", refreshToken)
    });

            var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", body);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("access_token").GetString()!;
        }

        /// <summary>
        /// Setup Gmail push notifications via Pub/Sub watch
        /// </summary>
        public async Task<GmailWatchResult> SetupWatchAsync(string accessToken, string pubSubTopic)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var body = JsonSerializer.Serialize(new
            {
                topicName = pubSubTopic,
                labelIds = new[] { "INBOX" },
                labelFilterBehavior = "INCLUDE"
            });

            var response = await _httpClient.PostAsync(
                $"{GmailApiBase}/watch",
                new StringContent(body, Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var historyId = root.GetProperty("historyId").GetString()!;
            var expirationMs = long.Parse(root.GetProperty("expiration").GetString()!);

            _logger.LogInformation("Gmail watch set up — historyId: {HistoryId}", historyId);

            return new GmailWatchResult
            {
                HistoryId = historyId,
                Expiration = DateTimeOffset.FromUnixTimeMilliseconds(expirationMs).UtcDateTime
            };
        }

        /// <summary>
        /// Get new message IDs since the last known historyId
        /// </summary>
        public async Task<GmailHistoryResult> GetNewMessageIdsAsync(string accessToken, string startHistoryId)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var url = $"{GmailApiBase}/history" +
                      $"?startHistoryId={startHistoryId}&historyTypes=messageAdded&labelId=INBOX";

            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new GmailHistoryResult { LatestHistoryId = startHistoryId };

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var result = new GmailHistoryResult
            {
                LatestHistoryId = root.TryGetProperty("historyId", out var hid)
                    ? hid.GetString() ?? startHistoryId : startHistoryId
            };

            if (root.TryGetProperty("history", out var historyArray))
            {
                foreach (var item in historyArray.EnumerateArray())
                {
                    if (!item.TryGetProperty("messagesAdded", out var messagesAdded)) continue;

                    foreach (var msgAdded in messagesAdded.EnumerateArray())
                    {
                        if (msgAdded.TryGetProperty("message", out var msg) &&
                            msg.TryGetProperty("id", out var msgId))
                        {
                            var id = msgId.GetString();
                            if (!string.IsNullOrEmpty(id) && !result.NewMessageIds.Contains(id))
                                result.NewMessageIds.Add(id);
                        }
                    }
                }
            }

            return result;
        }
    }

    #region Gmail API Response Models

    public class ThreadListResponse
    {
        [JsonPropertyName("threads")]
        public List<ThreadStub>? Threads { get; set; }

        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; set; }
    }

    public class ThreadStub
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("snippet")]
        public string? Snippet { get; set; }
    }

    public class GmailThread
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("historyId")]
        public string? HistoryId { get; set; }

        [JsonPropertyName("messages")]
        public List<GmailMessage>? Messages { get; set; }
    }

    public class GmailMessage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("threadId")]
        public string? ThreadId { get; set; }

        [JsonPropertyName("internalDate")]
        public string? InternalDate { get; set; }

        [JsonPropertyName("payload")]
        public GmailPayload? Payload { get; set; }

        [JsonPropertyName("snippet")]
        public string? Snippet { get; set; }
    }

    public class GmailPayload
    {
        [JsonPropertyName("mimeType")]
        public string? MimeType { get; set; }

        [JsonPropertyName("headers")]
        public List<GmailHeader>? Headers { get; set; }

        [JsonPropertyName("body")]
        public GmailBody? Body { get; set; }

        [JsonPropertyName("parts")]
        public List<GmailPayload>? Parts { get; set; }
    }

    public class GmailHeader
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("value")]
        public string Value { get; set; } = "";
    }

    public class GmailBody
    {
        [JsonPropertyName("data")]
        public string? Data { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }
    }

    public class GmailWatchResult
    {
        public string HistoryId { get; set; } = "";
        public DateTime Expiration { get; set; }
    }

    public class GmailHistoryResult
    {
        public List<string> NewMessageIds { get; set; } = new();
        public string LatestHistoryId { get; set; } = "";
    }

    #endregion
}
