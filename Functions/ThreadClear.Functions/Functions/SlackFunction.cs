using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class SlackFunction
    {
        private readonly ILogger _logger;
        private readonly IConversationParser _parser;
        private readonly IConversationAnalyzer _analyzer;
        private readonly IThreadCapsuleBuilder _builder;
        private readonly ISlackWorkspaceRepository _workspaceRepo;
        private readonly IConfiguration _configuration;
        private static readonly HttpClient _httpClient = new();

        // OAuth credentials from configuration
        private string SlackClientId => _configuration["SlackClientId"] ?? "";
        private string SlackClientSecret => _configuration["SlackClientSecret"] ?? "";
        private int FreeTierMonthlyLimit => int.TryParse(_configuration["SlackFreeTierLimit"], out var limit) ? limit : 20;

        public SlackFunction(
            ILoggerFactory loggerFactory,
            IConversationParser parser,
            IConversationAnalyzer analyzer,
            IThreadCapsuleBuilder builder,
            ISlackWorkspaceRepository workspaceRepo,
            IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<SlackFunction>();
            _parser = parser;
            _analyzer = analyzer;
            _builder = builder;
            _workspaceRepo = workspaceRepo;
            _configuration = configuration;
        }

        /// <summary>
        /// OAuth callback - called when a workspace installs the app
        /// </summary>
        [Function("slack-oauth")]
        public async Task<HttpResponseData> HandleOAuthCallback(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "slack-oauth")]
            HttpRequestData req)
        {
            _logger.LogInformation("Slack OAuth callback received");

            try
            {
                var query = HttpUtility.ParseQueryString(req.Url.Query);
                var code = query["code"];
                var error = query["error"];

                if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogWarning("OAuth error: {Error}", error);
                    return await CreateRedirectResponse(req, "https://threadclear.com/slack/error?reason=" + error);
                }

                if (string.IsNullOrEmpty(code))
                {
                    return await CreateRedirectResponse(req, "https://threadclear.com/slack/error?reason=no_code");
                }

                // Exchange code for token
                var tokenResponse = await ExchangeCodeForToken(code);
                if (tokenResponse == null)
                {
                    return await CreateRedirectResponse(req, "https://threadclear.com/slack/error?reason=token_exchange_failed");
                }

                // Store or update workspace
                var existingWorkspace = await _workspaceRepo.GetByTeamIdAsync(tokenResponse.TeamId);

                if (existingWorkspace != null)
                {
                    // Update existing workspace
                    existingWorkspace.AccessToken = tokenResponse.AccessToken;
                    existingWorkspace.Scope = tokenResponse.Scope;
                    existingWorkspace.TeamName = tokenResponse.TeamName;
                    existingWorkspace.IsActive = true;
                    await _workspaceRepo.UpdateAsync(existingWorkspace);
                    _logger.LogInformation("Updated existing workspace {TeamId}", tokenResponse.TeamId);
                }
                else
                {
                    // Create new workspace
                    var workspace = new SlackWorkspace
                    {
                        TeamId = tokenResponse.TeamId,
                        TeamName = tokenResponse.TeamName,
                        AccessToken = tokenResponse.AccessToken,
                        Scope = tokenResponse.Scope,
                        InstalledByUserId = tokenResponse.InstalledByUserId,
                        Tier = "free",
                        MonthlyAnalysisLimit = FreeTierMonthlyLimit
                    };
                    await _workspaceRepo.CreateAsync(workspace);
                    _logger.LogInformation("Created new workspace {TeamId} ({TeamName})", tokenResponse.TeamId, tokenResponse.TeamName);
                }

                // Redirect to success page
                return await CreateRedirectResponse(req, "https://threadclear.com/slack/success");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OAuth callback");
                return await CreateRedirectResponse(req, "https://threadclear.com/slack/error?reason=internal_error");
            }
        }

        /// <summary>
        /// Handle /threadclear slash command
        /// </summary>
        [Function("slack-command")]
        public async Task<HttpResponseData> HandleSlashCommand(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slack-command")]
            HttpRequestData req)
        {
            _logger.LogInformation("Slack slash command received");

            try
            {
                // Parse form data from Slack
                var body = await req.ReadAsStringAsync();
                var formData = HttpUtility.ParseQueryString(body ?? "");

                var teamId = formData["team_id"];
                var channelId = formData["channel_id"];
                var userId = formData["user_id"];
                var userName = formData["user_name"];
                var text = formData["text"];
                var responseUrl = formData["response_url"];

                _logger.LogInformation("Slash command from user {User} in team {Team}", userName, teamId);

                // Get workspace from database
                var workspace = await _workspaceRepo.GetByTeamIdAsync(teamId ?? "");
                if (workspace == null)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.OK);
                    await errorResponse.WriteAsJsonAsync(new
                    {
                        response_type = "ephemeral",
                        text = "❌ ThreadClear is not properly installed in this workspace. Please reinstall from https://threadclear.com/slack/install"
                    });
                    return errorResponse;
                }

                // Check usage limits
                if (workspace.HasExceededLimit())
                {
                    var limitResponse = req.CreateResponse(HttpStatusCode.OK);
                    await limitResponse.WriteAsJsonAsync(new
                    {
                        response_type = "ephemeral",
                        text = $"⚠️ You've reached your monthly limit of {workspace.MonthlyAnalysisLimit} analyses.\n\nUpgrade to Pro for unlimited analyses: https://threadclear.com/pricing"
                    });
                    return limitResponse;
                }

                // Handle connect command
                if (text?.Trim().ToLower() == "connect")
                {
                    var connectResponse = req.CreateResponse(HttpStatusCode.OK);
                    var connectUrl = $"https://app.threadclear.com/connect?platform=slack&id={teamId}&name={Uri.EscapeDataString(workspace.TeamName ?? "Your Workspace")}";

                    await connectResponse.WriteAsJsonAsync(new
                    {
                        response_type = "ephemeral",
                        blocks = new object[]
                        {
                            new
                            {
                                type = "section",
                                text = new { type = "mrkdwn", text = $"🔗 *Connect this workspace to your ThreadClear account*\n\nLink your paid ThreadClear subscription to unlock unlimited analyses for your entire workspace.\n\nWorkspace ID: `{teamId}`" }
                            },
                            new
                            {
                                type = "actions",
                                elements = new object[]
                                {
                                    new
                                    {
                                        type = "button",
                                        text = new { type = "plain_text", text = "Connect Workspace", emoji = true },
                                        url = connectUrl,
                                        style = "primary"
                                    }
                                }
                            }
                        }
                    });
                    return connectResponse;
                }

                // Handle status command
                if (text?.Trim().ToLower() == "status")
                {
                    var statusResponse = req.CreateResponse(HttpStatusCode.OK);
                    var tierDisplay = workspace.Tier == "free" ? "Free" : workspace.Tier == "pro" ? "Pro ✨" : "Enterprise 🏢";
                    var usageDisplay = workspace.Tier == "free"
                        ? $"{workspace.MonthlyAnalysisCount} of {workspace.MonthlyAnalysisLimit} used this month"
                        : "Unlimited";
                    var linkedStatus = workspace.OrganizationId.HasValue ? "✅ Connected to ThreadClear organization" : "❌ Not connected";

                    await statusResponse.WriteAsJsonAsync(new
                    {
                        response_type = "ephemeral",
                        blocks = new object[]
                        {
                            new
                            {
                                type = "section",
                                text = new { type = "mrkdwn", text = $"📊 *ThreadClear Workspace Status*\n\n*Workspace:* {workspace.TeamName ?? teamId}\n*Tier:* {tierDisplay}\n*Usage:* {usageDisplay}\n*Status:* {linkedStatus}" }
                            }
                        }
                    });
                    return statusResponse;
                }

                // Immediate response to Slack (must respond within 3 seconds)
                var ackResponse = req.CreateResponse(HttpStatusCode.OK);
                var remaining = workspace.GetRemainingAnalyses();
                var remainingText = remaining < int.MaxValue ? $" ({remaining - 1} remaining this month)" : "";

                await ackResponse.WriteAsJsonAsync(new
                {
                    response_type = "ephemeral",
                    text = $"🔍 Analyzing conversation...{remainingText}"
                });

                // Process analysis in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessAndRespond(text, channelId, workspace, responseUrl);
                        await _workspaceRepo.IncrementUsageAsync(teamId ?? "");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in background processing");
                        await SendSlackResponse(responseUrl, "❌ Analysis failed: " + ex.Message, true);
                    }
                });

                return ackResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Slack command");
                var errorResponse = req.CreateResponse(HttpStatusCode.OK);
                await errorResponse.WriteAsJsonAsync(new
                {
                    response_type = "ephemeral",
                    text = "❌ Error: " + ex.Message
                });
                return errorResponse;
            }
        }

        private async Task<OAuthTokenResponse?> ExchangeCodeForToken(string code)
        {
            try
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = SlackClientId,
                    ["client_secret"] = SlackClientSecret,
                    ["code"] = code
                });

                var response = await _httpClient.PostAsync("https://slack.com/api/oauth.v2.access", content);
                var json = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("OAuth response: {Response}", json);

                var data = JsonDocument.Parse(json);
                var root = data.RootElement;

                if (!root.GetProperty("ok").GetBoolean())
                {
                    _logger.LogError("OAuth error: {Error}", root.GetProperty("error").GetString());
                    return null;
                }

                return new OAuthTokenResponse
                {
                    AccessToken = root.GetProperty("access_token").GetString() ?? "",
                    TeamId = root.GetProperty("team").GetProperty("id").GetString() ?? "",
                    TeamName = root.GetProperty("team").GetProperty("name").GetString() ?? "",
                    Scope = root.GetProperty("scope").GetString() ?? "",
                    InstalledByUserId = root.TryGetProperty("authed_user", out var user)
                        ? user.GetProperty("id").GetString()
                        : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exchanging code for token");
                return null;
            }
        }

        private async Task ProcessAndRespond(string? text, string? channelId, SlackWorkspace workspace, string? responseUrl)
        {
            string conversationText;

            if (!string.IsNullOrWhiteSpace(text))
            {
                conversationText = text;
            }
            else if (!string.IsNullOrEmpty(channelId))
            {
                conversationText = await FetchChannelHistory(channelId, workspace.AccessToken);
                if (string.IsNullOrEmpty(conversationText))
                {
                    await SendSlackResponse(responseUrl, "❌ No messages found. Try: `/threadclear [paste your conversation]`", true);
                    return;
                }
            }
            else
            {
                await SendSlackResponse(responseUrl, "❌ Please provide a conversation: `/threadclear [paste conversation]`", true);
                return;
            }

            // Parse and analyze
            var capsule = await _parser.ParseConversation(conversationText, "slack", null);
            await _analyzer.AnalyzeConversation(capsule, null);
            await _builder.EnrichWithLinguisticFeatures(capsule);
            await _builder.CalculateMetadata(capsule);
            var summary = await _builder.GenerateSummary(capsule);
            capsule.Summary = summary;

            // Format response
            var blocks = FormatSlackBlocks(capsule, workspace);
            await SendSlackBlockResponse(responseUrl, blocks, false);
        }

        private async Task<string> FetchChannelHistory(string channelId, string accessToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"https://slack.com/api/conversations.history?channel={channelId}&limit=20");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonDocument.Parse(json);

                if (!data.RootElement.GetProperty("ok").GetBoolean())
                {
                    _logger.LogWarning("Slack API error: {Response}", json);
                    return "";
                }

                var messages = data.RootElement.GetProperty("messages");
                var sb = new StringBuilder();

                var messageList = messages.EnumerateArray().ToList();
                messageList.Reverse();

                foreach (var msg in messageList)
                {
                    if (msg.TryGetProperty("text", out var textProp))
                    {
                        var user = msg.TryGetProperty("user", out var userProp) ? userProp.GetString() : "Unknown";
                        sb.AppendLine($"{user}: {textProp.GetString()}");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching channel history");
                return "";
            }
        }

        private List<object> FormatSlackBlocks(ThreadCapsule capsule, SlackWorkspace workspace)
        {
            var blocks = new List<object>();
            var analysis = capsule.Analysis;
            var health = analysis?.ConversationHealth;

            // Header
            blocks.Add(new
            {
                type = "header",
                text = new { type = "plain_text", text = "📊 ThreadClear Analysis", emoji = true }
            });

            // Summary
            if (!string.IsNullOrEmpty(capsule.Summary))
            {
                blocks.Add(new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = $"*Summary*\n{capsule.Summary}" }
                });
                blocks.Add(new { type = "divider" });
            }

            // Health Score
            if (health != null)
            {
                var healthScore = (int)(health.HealthScore * 100);
                var riskLevel = health.RiskLevel ?? "Low";
                var riskEmoji = riskLevel.ToLower() switch
                {
                    "high" => "🔴",
                    "medium" => "🟡",
                    _ => "🟢"
                };

                blocks.Add(new
                {
                    type = "section",
                    fields = new[]
                    {
                        new { type = "mrkdwn", text = $"*Health Score*\n{healthScore}%" },
                        new { type = "mrkdwn", text = $"*Risk Level*\n{riskEmoji} {riskLevel}" }
                    }
                });

                blocks.Add(new
                {
                    type = "section",
                    fields = new[]
                    {
                        new { type = "mrkdwn", text = $"*Responsiveness*\n{(int)(health.ResponsivenessScore * 100)}%" },
                        new { type = "mrkdwn", text = $"*Clarity*\n{(int)(health.ClarityScore * 100)}%" },
                        new { type = "mrkdwn", text = $"*Alignment*\n{(int)(health.AlignmentScore * 100)}%" }
                    }
                });
            }

            // Unanswered Questions
            var unanswered = analysis?.UnansweredQuestions ?? new List<UnansweredQuestion>();
            if (unanswered.Any())
            {
                blocks.Add(new { type = "divider" });
                var questionsText = string.Join("\n", unanswered.Take(5).Select(q => $"• \"{q.Question}\" - asked by {q.AskedBy}"));
                blocks.Add(new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = $"*❓ Unanswered Questions ({unanswered.Count})*\n{questionsText}" }
                });
            }

            // Tension Points
            var tensions = analysis?.TensionPoints ?? new List<TensionPoint>();
            if (tensions.Any())
            {
                blocks.Add(new { type = "divider" });
                var tensionText = string.Join("\n", tensions.Take(5).Select(t =>
                {
                    var emoji = t.Severity?.ToLower() switch
                    {
                        "high" => "🔴",
                        "medium" => "🟡",
                        _ => "🟢"
                    };
                    return $"• {emoji} {t.Description}";
                }));
                blocks.Add(new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = $"*⚡ Tension Points ({tensions.Count})*\n{tensionText}" }
                });
            }

            // Misalignments
            var misalignments = analysis?.Misalignments ?? new List<Misalignment>();
            if (misalignments.Any())
            {
                blocks.Add(new { type = "divider" });
                var misalignText = string.Join("\n", misalignments.Take(3).Select(m => $"• {m.Description}"));
                blocks.Add(new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = $"*🔀 Misalignments ({misalignments.Count})*\n{misalignText}" }
                });
            }

            // Suggested Actions
            var actions = capsule.SuggestedActions ?? new List<SuggestedActionItem>();
            if (actions.Any())
            {
                blocks.Add(new { type = "divider" });
                var actionsText = string.Join("\n", actions.Take(3).Select(a => $"• {a.Action}"));
                blocks.Add(new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = $"*💡 Suggested Actions*\n{actionsText}" }
                });
            }

            // Footer with usage info
            blocks.Add(new { type = "divider" });
            var usageText = workspace.Tier == "free"
                ? $"Free tier: {workspace.GetRemainingAnalyses()} analyses remaining | <https://threadclear.com/pricing|Upgrade>"
                : "Pro tier: Unlimited analyses";

            blocks.Add(new
            {
                type = "context",
                elements = new[]
                {
                    new { type = "mrkdwn", text = $"<https://threadclear.com|ThreadClear> | {usageText}" }
                }
            });

            return blocks;
        }

        private async Task<HttpResponseData> CreateRedirectResponse(HttpRequestData req, string url)
        {
            var response = req.CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Add("Location", url);
            return response;
        }

        private async Task SendSlackResponse(string? responseUrl, string message, bool ephemeral)
        {
            if (string.IsNullOrEmpty(responseUrl)) return;

            var payload = new
            {
                response_type = ephemeral ? "ephemeral" : "in_channel",
                text = message
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            await _httpClient.PostAsync(responseUrl, content);
        }

        private async Task SendSlackBlockResponse(string? responseUrl, List<object> blocks, bool ephemeral)
        {
            if (string.IsNullOrEmpty(responseUrl)) return;

            var payload = new
            {
                response_type = ephemeral ? "ephemeral" : "in_channel",
                blocks = blocks
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            await _httpClient.PostAsync(responseUrl, content);
        }

        private class OAuthTokenResponse
        {
            public string AccessToken { get; set; } = "";
            public string TeamId { get; set; } = "";
            public string TeamName { get; set; } = "";
            public string Scope { get; set; } = "";
            public string? InstalledByUserId { get; set; }
        }
    }
}