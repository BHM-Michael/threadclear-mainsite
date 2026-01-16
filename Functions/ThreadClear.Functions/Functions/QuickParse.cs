using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThreadClear.Functions.Functions
{
    public class QuickParse
    {
        private readonly ILogger _logger;

        public QuickParse(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<QuickParse>();
        }

        [Function("QuickParse")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "parse/quick")]
            HttpRequestData req)
        {
            // Handle CORS preflight
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                var corsResponse = req.CreateResponse(HttpStatusCode.OK);
                return corsResponse;
            }

            var request = await req.ReadFromJsonAsync<QuickParseRequest>();
            if (request == null || string.IsNullOrWhiteSpace(request.ConversationText))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteAsJsonAsync(new { success = false, error = "Invalid request" });
                return errorResponse;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Quick regex-based parsing - no AI calls
                var participants = ExtractParticipants(request.ConversationText);
                var messages = ExtractMessages(request.ConversationText, participants);
                var sourceType = DetectSourceType(request.ConversationText);
                var metadata = ExtractMetadata(messages);

                _logger.LogInformation("QuickParse completed in {Ms}ms - {Participants} participants, {Messages} messages",
                    sw.ElapsedMilliseconds, participants.Count, messages.Count);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    participants,
                    messages,
                    sourceType,
                    metadata,
                    parseTimeMs = sw.ElapsedMilliseconds
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QuickParse error");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { success = false, error = ex.Message });
                return errorResponse;
            }
        }

        private List<ParticipantDto> ExtractParticipants(string text)
        {
            var participants = new Dictionary<string, ParticipantDto>(StringComparer.OrdinalIgnoreCase);
            var index = 1;

            // Email: "From: Name" or "From: Name <email>"
            var fromMatches = Regex.Matches(text, @"From:\s*([^<\n]+?)(?:\s*<([^>]+)>)?(?:\r?\n|$)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            foreach (Match m in fromMatches)
            {
                var name = m.Groups[1].Value.Trim();
                var email = m.Groups[2].Success ? m.Groups[2].Value.Trim() : null;
                if (!string.IsNullOrWhiteSpace(name) && name.Length < 50 && !participants.ContainsKey(name))
                {
                    participants[name] = new ParticipantDto
                    {
                        Id = $"p{index++}",
                        Name = name,
                        Email = email
                    };
                }
            }

            // Simple format: "Name: message" at start of line
            var simpleMatches = Regex.Matches(text, @"^([A-Z][a-zA-Z]+(?:\s+[A-Z][a-zA-Z]+)?):\s+", RegexOptions.Multiline);
            foreach (Match m in simpleMatches)
            {
                var name = m.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(name) && name.Length < 30 && !participants.ContainsKey(name))
                {
                    // Skip common false positives
                    if (IsHeaderWord(name)) continue;

                    participants[name] = new ParticipantDto
                    {
                        Id = $"p{index++}",
                        Name = name
                    };
                }
            }

            // Slack format: "username [time]:"
            var slackMatches = Regex.Matches(text, @"^(\w+)\s+\[\d{1,2}:\d{2}", RegexOptions.Multiline);
            foreach (Match m in slackMatches)
            {
                var name = m.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(name) && !participants.ContainsKey(name))
                {
                    participants[name] = new ParticipantDto
                    {
                        Id = $"p{index++}",
                        Name = name
                    };
                }
            }

            return participants.Values.ToList();
        }

        private bool IsHeaderWord(string word)
        {
            var headers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "From", "To", "Cc", "Bcc", "Subject", "Date", "Sent", "Reply", "Forward",
                "Re", "Fw", "Fwd", "Note", "Attachment", "Priority", "Importance"
            };
            return headers.Contains(word);
        }

        private List<MessageDto> ExtractMessages(string text, List<ParticipantDto> participants)
        {
            var messages = new List<MessageDto>();
            var index = 1;

            // Try email format first
            if (text.Contains("From:", StringComparison.OrdinalIgnoreCase))
            {
                messages = ExtractEmailMessages(text, ref index);
                if (messages.Count >= 1) return messages;
            }

            // Try simple "Name: message" format
            var participantNames = participants.Select(p => Regex.Escape(p.Name)).ToList();
            if (participantNames.Any())
            {
                var pattern = $@"^({string.Join("|", participantNames)}):\s+(.+?)(?=^(?:{string.Join("|", participantNames)}):|$)";
                var matches = Regex.Matches(text, pattern, RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match m in matches)
                {
                    var sender = m.Groups[1].Value.Trim();
                    var content = m.Groups[2].Value.Trim();
                    var participant = participants.FirstOrDefault(p => p.Name.Equals(sender, StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        messages.Add(new MessageDto
                        {
                            Id = $"msg{index++}",
                            ParticipantId = participant?.Id ?? sender,
                            Sender = sender,
                            Content = content.Length > 1000 ? content.Substring(0, 1000) + "..." : content
                        });
                    }
                }
            }

            // Fallback: split by line breaks if nothing found
            if (messages.Count == 0)
            {
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines.Take(50)) // Limit for safety
                {
                    var colonIndex = line.IndexOf(':');
                    if (colonIndex > 0 && colonIndex < 30)
                    {
                        var sender = line.Substring(0, colonIndex).Trim();
                        var content = line.Substring(colonIndex + 1).Trim();

                        if (!string.IsNullOrWhiteSpace(content) && !IsHeaderWord(sender))
                        {
                            messages.Add(new MessageDto
                            {
                                Id = $"msg{index++}",
                                Sender = sender,
                                ParticipantId = participants.FirstOrDefault(p => p.Name.Equals(sender, StringComparison.OrdinalIgnoreCase))?.Id ?? sender,
                                Content = content
                            });
                        }
                    }
                }
            }

            return messages;
        }

        private List<MessageDto> ExtractEmailMessages(string text, ref int index)
        {
            var messages = new List<MessageDto>();

            // Split by "From:" headers
            var emailBlocks = Regex.Split(text, @"(?=^From:\s*)", RegexOptions.Multiline);

            foreach (var block in emailBlocks.Where(b => b.StartsWith("From:", StringComparison.OrdinalIgnoreCase)))
            {
                var fromMatch = Regex.Match(block, @"From:\s*([^<\n]+?)(?:\s*<([^>]+)>)?(?:\r?\n|$)", RegexOptions.IgnoreCase);
                var dateMatch = Regex.Match(block, @"(?:Date|Sent):\s*([^\r\n]+)", RegexOptions.IgnoreCase);

                if (fromMatch.Success)
                {
                    var sender = fromMatch.Groups[1].Value.Trim();
                    var body = ExtractEmailBody(block);
                    DateTime? timestamp = null;

                    if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value.Trim(), out var dt))
                    {
                        timestamp = dt;
                    }

                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        messages.Add(new MessageDto
                        {
                            Id = $"msg{index++}",
                            Sender = sender,
                            ParticipantId = sender,
                            Content = body.Length > 1000 ? body.Substring(0, 1000) + "..." : body,
                            Timestamp = timestamp
                        });
                    }
                }
            }

            // Reverse to get chronological order (oldest first)
            messages.Reverse();
            return messages;
        }

        private string ExtractEmailBody(string emailBlock)
        {
            var lines = emailBlock.Split('\n').ToList();
            var bodyStart = 0;
            var headerEnded = false;

            // Find where headers end
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();

                // Empty line after headers signals body start
                if (string.IsNullOrWhiteSpace(line) && i > 0)
                {
                    bodyStart = i + 1;
                    headerEnded = true;
                    break;
                }

                // Line that doesn't look like a header
                if (!Regex.IsMatch(line, @"^(From|To|Cc|Bcc|Subject|Date|Sent|Reply-To|Content-Type|MIME-Version|Importance|Priority):", RegexOptions.IgnoreCase)
                    && line.Length > 40 && !line.StartsWith("X-"))
                {
                    bodyStart = i;
                    headerEnded = true;
                    break;
                }
            }

            if (!headerEnded || bodyStart >= lines.Count) return "";

            var body = string.Join("\n", lines.Skip(bodyStart));

            // Remove quoted replies (previous emails in thread)
            var quotePatterns = new[] {
        @"^On .+wrote:[\s\S]*$",
        @"^From:.+\nSent:.+\nTo:[\s\S]*$",
        @"^From:.+\nDate:.+\nTo:[\s\S]*$",
        @"_{10,}[\s\S]*$",  // Line of underscores starts quoted content
        @"-{10,}[\s\S]*$",  // Line of dashes
    };

            foreach (var pattern in quotePatterns)
            {
                var match = Regex.Match(body, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (match.Success && match.Index > 20)
                {
                    body = body.Substring(0, match.Index).Trim();
                }
            }

            // Remove signatures and footers
            var sigPatterns = new[] {
        @"^--\s*$",
        @"^Sent from my",
        @"^Sent from Mail",
        @"^Best regards,?\s*$",
        @"^Thanks,?\s*$",
        @"^Thank you,?\s*$",
        @"^Regards,?\s*$",
        @"^Cheers,?\s*$",
        @"^Best,?\s*$",
        @"^\s*This message \(including any attachments\)",
        @"^\s*This email and any attachments",
        @"^\s*CONFIDENTIAL",
        @"^\s*Deloitte refers to",
        @"^Check out our careers",
        @"^Follow us on Twitter",
        @"^v\.E\.\d+",  // Deloitte version footer
        @"^\s*\d{3}[-.]?\d{3}[-.]?\d{4}\s*$",  // Just a phone number
        @"^[A-Z]+\s*$",  // Just a company name in caps (like "HALVIK")
    };

            foreach (var pattern in sigPatterns)
            {
                var match = Regex.Match(body, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (match.Success && match.Index > 10)
                {
                    body = body.Substring(0, match.Index).Trim();
                }
            }

            // Remove URLs
            body = Regex.Replace(body, @"https?://\S+", "", RegexOptions.IgnoreCase);

            // Remove email addresses on their own line
            body = Regex.Replace(body, @"^\s*[\w\.-]+@[\w\.-]+\s*$", "", RegexOptions.Multiline);

            // Clean up multiple blank lines
            body = Regex.Replace(body, @"\n{3,}", "\n\n");

            // Limit length
            body = body.Trim();
            if (body.Length > 500)
            {
                body = body.Substring(0, 500) + "...";
            }

            return body;
        }

        private string DetectSourceType(string text)
        {
            if (Regex.IsMatch(text, @"^From:\s*.+", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                return "email";
            if (Regex.IsMatch(text, @"\w+\s+\[\d{1,2}:\d{2}", RegexOptions.Multiline))
                return "slack";
            if (Regex.IsMatch(text, @"\[\d{1,2}:\d{2}.*?\]\s*\w+", RegexOptions.Multiline))
                return "teams";
            return "conversation";
        }

        private object ExtractMetadata(List<MessageDto> messages)
        {
            if (messages.Count == 0)
            {
                return new
                {
                    MessageCount = 0,
                    StartDate = (DateTime?)null,
                    EndDate = (DateTime?)null
                };
            }

            var messagesWithDates = messages.Where(m => m.Timestamp.HasValue).ToList();

            return new
            {
                MessageCount = messages.Count,
                StartDate = messagesWithDates.FirstOrDefault()?.Timestamp,
                EndDate = messagesWithDates.LastOrDefault()?.Timestamp,
                ThreadInitiator = messages.FirstOrDefault()?.Sender
            };
        }
    }

    public class QuickParseRequest
    {
        public string ConversationText { get; set; } = "";
    }

    public class ParticipantDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Email { get; set; }
        public int? InferredRole { get; set; }
    }

    public class MessageDto
    {
        public string Id { get; set; } = "";
        public string ParticipantId { get; set; } = "";
        public string Sender { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime? Timestamp { get; set; }
    }
}