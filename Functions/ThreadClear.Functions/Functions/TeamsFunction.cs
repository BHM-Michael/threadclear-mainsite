using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class TeamsFunction
    {
        private readonly ILogger _logger;
        private readonly IConversationParser _parser;
        private readonly IConversationAnalyzer _analyzer;
        private readonly IThreadCapsuleBuilder _builder;
        private readonly ITeamsWorkspaceRepository _workspaceRepo;
        private readonly IConfiguration _configuration;
        private static readonly HttpClient _httpClient = new();

        // Bot credentials from configuration
        private string MicrosoftAppId => _configuration["MicrosoftAppId"] ?? "";
        private string MicrosoftAppPassword => _configuration["MicrosoftAppPassword"] ?? "";
        private int FreeTierMonthlyLimit => int.TryParse(_configuration["TeamsFreeTierLimit"], out var limit) ? limit : 20;

        public TeamsFunction(
            ILoggerFactory loggerFactory,
            IConversationParser parser,
            IConversationAnalyzer analyzer,
            IThreadCapsuleBuilder builder,
            ITeamsWorkspaceRepository workspaceRepo,
            IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<TeamsFunction>();
            _parser = parser;
            _analyzer = analyzer;
            _builder = builder;
            _workspaceRepo = workspaceRepo;
            _configuration = configuration;
        }

        /// <summary>
        /// Main endpoint for Teams bot messages
        /// </summary>
        [Function("teams-messages")]
        public async Task<HttpResponseData> HandleTeamsMessage(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "teams-messages")]
            HttpRequestData req)
        {
            _logger.LogInformation("Teams bot message received");

            try
            {
                var body = await req.ReadAsStringAsync();
                _logger.LogInformation("Teams payload: {Body}", body);

                var activity = JsonSerializer.Deserialize<TeamsActivity>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (activity == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                // Handle different activity types
                switch (activity.Type?.ToLower())
                {
                    case "message":
                        await HandleMessage(activity);
                        break;

                    case "conversationupdate":
                        await HandleConversationUpdate(activity);
                        break;

                    case "invoke":
                        // Handle invoke activities (cards, etc.)
                        return await HandleInvoke(req, activity);
                }

                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Teams message");
                return req.CreateResponse(HttpStatusCode.OK); // Always return 200 to Teams
            }
        }

        private async Task HandleMessage(TeamsActivity activity)
        {
            var text = activity.Text?.Trim() ?? "";
            var tenantId = activity.Conversation?.TenantId ?? "";
            var serviceUrl = activity.ServiceUrl ?? "";

            _logger.LogInformation("Message from tenant {TenantId}: {Text}", tenantId, text);

            // Remove bot mention if present
            if (activity.Entities != null)
            {
                foreach (var entity in activity.Entities)
                {
                    if (entity.Type == "mention" && !string.IsNullOrEmpty(entity.Text))
                    {
                        text = text.Replace(entity.Text, "").Trim();
                    }
                }
            }

            // Get or create workspace
            var workspace = await GetOrCreateWorkspace(tenantId, serviceUrl);

            // Check for connect command
            if (text.ToLower() == "connect")
            {
                await SendConnectMessage(activity, workspace);
                return;
            }

            // Check for status command
            if (text.ToLower() == "status")
            {
                await SendStatusMessage(activity, workspace);
                return;
            }

            // Check for help command
            if (string.IsNullOrEmpty(text) || text.ToLower() == "help")
            {
                await SendHelpMessage(activity, workspace);
                return;
            }

            // Check usage limits
            if (workspace.HasExceededLimit())
            {
                await SendLimitExceededMessage(activity, workspace);
                return;
            }

            // Send typing indicator
            await SendTypingIndicator(activity);

            // Analyze the conversation
            try
            {
                var capsule = await _parser.ParseConversation(text, "teams", null);
                await _analyzer.AnalyzeConversation(capsule, null);
                await _builder.EnrichWithLinguisticFeatures(capsule);
                await _builder.CalculateMetadata(capsule);
                var summary = await _builder.GenerateSummary(capsule);
                capsule.Summary = summary;

                // Increment usage
                await _workspaceRepo.IncrementUsageAsync(tenantId);

                // Send results as Adaptive Card
                await SendAnalysisResults(activity, capsule, workspace);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing conversation");
                await SendErrorMessage(activity, "Analysis failed: " + ex.Message);
            }
        }

        private async Task HandleConversationUpdate(TeamsActivity activity)
        {
            // Bot was added to a conversation
            if (activity.MembersAdded != null)
            {
                foreach (var member in activity.MembersAdded)
                {
                    // Check if the bot was added (not a user)
                    if (member.Id == activity.Recipient?.Id)
                    {
                        var tenantId = activity.Conversation?.TenantId ?? "";
                        var serviceUrl = activity.ServiceUrl ?? "";

                        await GetOrCreateWorkspace(tenantId, serviceUrl);
                        await SendWelcomeMessage(activity);
                    }
                }
            }
        }

        private async Task<HttpResponseData> HandleInvoke(HttpRequestData req, TeamsActivity activity)
        {
            // Handle adaptive card actions, etc.
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { statusCode = 200 });
            return response;
        }

        private async Task<TeamsWorkspace> GetOrCreateWorkspace(string tenantId, string serviceUrl)
        {
            var workspace = await _workspaceRepo.GetByTenantIdAsync(tenantId);

            if (workspace == null)
            {
                workspace = new TeamsWorkspace
                {
                    TenantId = tenantId,
                    ServiceUrl = serviceUrl,
                    Tier = "free",
                    MonthlyAnalysisLimit = FreeTierMonthlyLimit
                };
                await _workspaceRepo.CreateAsync(workspace);
                _logger.LogInformation("Created new Teams workspace for tenant {TenantId}", tenantId);
            }
            else if (workspace.ServiceUrl != serviceUrl && !string.IsNullOrEmpty(serviceUrl))
            {
                workspace.ServiceUrl = serviceUrl;
                await _workspaceRepo.UpdateAsync(workspace);
            }

            return workspace;
        }

        private async Task SendWelcomeMessage(TeamsActivity activity)
        {
            var card = new
            {
                type = "AdaptiveCard",
                version = "1.4",
                body = new object[]
                {
                    new
                    {
                        type = "TextBlock",
                        text = "👋 Welcome to ThreadClear!",
                        weight = "bolder",
                        size = "large"
                    },
                    new
                    {
                        type = "TextBlock",
                        text = "I analyze conversations to find unanswered questions, tension points, and communication health.",
                        wrap = true
                    },
                    new
                    {
                        type = "TextBlock",
                        text = "**How to use:**\n- Paste a conversation and I'll analyze it\n- Or just say 'help' for more info",
                        wrap = true
                    }
                }
            };

            await SendAdaptiveCard(activity, card);
        }

        private async Task SendConnectMessage(TeamsActivity activity, TeamsWorkspace workspace)
        {
            var tenantId = workspace.TenantId;
            var connectUrl = $"https://app.threadclear.com/connect?platform=teams&id={tenantId}&name={Uri.EscapeDataString(workspace.TenantName ?? "Your Organization")}";

            var card = new
            {
                type = "AdaptiveCard",
                version = "1.4",
                body = new object[]
                {
                    new
                    {
                        type = "TextBlock",
                        text = "🔗 Connect to ThreadClear",
                        weight = "bolder",
                        size = "large"
                    },
                    new
                    {
                        type = "TextBlock",
                        text = "Link your paid ThreadClear subscription to unlock unlimited analyses for your entire organization.",
                        wrap = true
                    },
                    new
                    {
                        type = "FactSet",
                        facts = new object[]
                        {
                            new { title = "Tenant ID", value = tenantId }
                        }
                    }
                },
                actions = new object[]
                {
                    new
                    {
                        type = "Action.OpenUrl",
                        title = "Connect Organization",
                        url = connectUrl
                    }
                }
            };

            await SendAdaptiveCard(activity, card);
        }

        private async Task SendStatusMessage(TeamsActivity activity, TeamsWorkspace workspace)
        {
            var tierDisplay = workspace.Tier == "free" ? "Free" : workspace.Tier == "pro" ? "Pro ✨" : "Enterprise 🏢";
            var usageDisplay = workspace.Tier == "free"
                ? $"{workspace.MonthlyAnalysisCount} of {workspace.MonthlyAnalysisLimit} used"
                : "Unlimited";
            var linkedStatus = workspace.OrganizationId.HasValue ? "✅ Connected" : "❌ Not connected";

            var card = new
            {
                type = "AdaptiveCard",
                version = "1.4",
                body = new object[]
                {
                    new
                    {
                        type = "TextBlock",
                        text = "📊 Workspace Status",
                        weight = "bolder",
                        size = "large"
                    },
                    new
                    {
                        type = "FactSet",
                        facts = new object[]
                        {
                            new { title = "Organization", value = workspace.TenantName ?? workspace.TenantId },
                            new { title = "Tier", value = tierDisplay },
                            new { title = "Monthly Usage", value = usageDisplay },
                            new { title = "ThreadClear Link", value = linkedStatus }
                        }
                    }
                },
                actions = workspace.OrganizationId.HasValue ? new object[] { } : new object[]
                {
                    new
                    {
                        type = "Action.OpenUrl",
                        title = "Upgrade to Pro",
                        url = "https://threadclear.com/pricing"
                    }
                }
            };

            await SendAdaptiveCard(activity, card);
        }

        private async Task SendHelpMessage(TeamsActivity activity, TeamsWorkspace workspace)
        {
            var remaining = workspace.GetRemainingAnalyses();
            var usageText = remaining < int.MaxValue
                ? $"You have {remaining} free analyses remaining this month."
                : "You have unlimited analyses (Pro tier).";

            var card = new
            {
                type = "AdaptiveCard",
                version = "1.4",
                body = new object[]
                {
                    new
                    {
                        type = "TextBlock",
                        text = "📊 ThreadClear Help",
                        weight = "bolder",
                        size = "large"
                    },
                    new
                    {
                        type = "TextBlock",
                        text = "**How to analyze a conversation:**\n1. Copy a conversation (email thread, chat, etc.)\n2. Paste it here and send\n3. I'll analyze it for issues and insights",
                        wrap = true
                    },
                    new
                    {
                        type = "TextBlock",
                        text = "**What I detect:**\n• ❓ Unanswered questions\n• ⚡ Tension points\n• 🔀 Misalignments\n• 📊 Conversation health score",
                        wrap = true
                    },
                    new
                    {
                        type = "TextBlock",
                        text = usageText,
                        wrap = true,
                        color = remaining < 5 ? "attention" : "default"
                    }
                },
                actions = new object[]
                {
                    new
                    {
                        type = "Action.OpenUrl",
                        title = "Visit ThreadClear.com",
                        url = "https://threadclear.com"
                    }
                }
            };

            await SendAdaptiveCard(activity, card);
        }

        private async Task SendLimitExceededMessage(TeamsActivity activity, TeamsWorkspace workspace)
        {
            var card = new
            {
                type = "AdaptiveCard",
                version = "1.4",
                body = new object[]
                {
                    new
                    {
                        type = "TextBlock",
                        text = "⚠️ Monthly Limit Reached",
                        weight = "bolder",
                        size = "large",
                        color = "attention"
                    },
                    new
                    {
                        type = "TextBlock",
                        text = $"You've used all {workspace.MonthlyAnalysisLimit} free analyses for this month.",
                        wrap = true
                    },
                    new
                    {
                        type = "TextBlock",
                        text = "Upgrade to Pro for unlimited analyses!",
                        wrap = true
                    }
                },
                actions = new object[]
                {
                    new
                    {
                        type = "Action.OpenUrl",
                        title = "Upgrade to Pro",
                        url = "https://threadclear.com/pricing"
                    }
                }
            };

            await SendAdaptiveCard(activity, card);
        }

        private async Task SendAnalysisResults(TeamsActivity activity, ThreadCapsule capsule, TeamsWorkspace workspace)
        {
            var analysis = capsule.Analysis;
            var health = analysis?.ConversationHealth;

            var bodyItems = new List<object>
            {
                new
                {
                    type = "TextBlock",
                    text = "📊 ThreadClear Analysis",
                    weight = "bolder",
                    size = "large"
                }
            };

            // Summary
            if (!string.IsNullOrEmpty(capsule.Summary))
            {
                bodyItems.Add(new
                {
                    type = "TextBlock",
                    text = $"**Summary**\n{capsule.Summary}",
                    wrap = true
                });
            }

            // Health Score
            if (health != null)
            {
                var healthScore = (int)(health.HealthScore * 100);
                var riskEmoji = health.RiskLevel?.ToLower() switch
                {
                    "high" => "🔴",
                    "medium" => "🟡",
                    _ => "🟢"
                };

                bodyItems.Add(new
                {
                    type = "ColumnSet",
                    columns = new object[]
                    {
                        new
                        {
                            type = "Column",
                            width = "stretch",
                            items = new object[]
                            {
                                new { type = "TextBlock", text = "Health Score", weight = "bolder" },
                                new { type = "TextBlock", text = $"{healthScore}%", size = "extraLarge", color = healthScore >= 70 ? "good" : healthScore >= 40 ? "warning" : "attention" }
                            }
                        },
                        new
                        {
                            type = "Column",
                            width = "stretch",
                            items = new object[]
                            {
                                new { type = "TextBlock", text = "Risk Level", weight = "bolder" },
                                new { type = "TextBlock", text = $"{riskEmoji} {health.RiskLevel ?? "Low"}", size = "large" }
                            }
                        }
                    }
                });

                bodyItems.Add(new
                {
                    type = "FactSet",
                    facts = new object[]
                    {
                        new { title = "Responsiveness", value = $"{(int)(health.ResponsivenessScore * 100)}%" },
                        new { title = "Clarity", value = $"{(int)(health.ClarityScore * 100)}%" },
                        new { title = "Alignment", value = $"{(int)(health.AlignmentScore * 100)}%" }
                    }
                });
            }

            // Unanswered Questions
            var unanswered = analysis?.UnansweredQuestions ?? new List<UnansweredQuestion>();
            if (unanswered.Any())
            {
                var questionsText = string.Join("\n", unanswered.Take(5).Select(q => $"• \"{q.Question}\" - {q.AskedBy}"));
                bodyItems.Add(new
                {
                    type = "TextBlock",
                    text = $"**❓ Unanswered Questions ({unanswered.Count})**\n{questionsText}",
                    wrap = true
                });
            }

            // Tension Points
            var tensions = analysis?.TensionPoints ?? new List<TensionPoint>();
            if (tensions.Any())
            {
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
                bodyItems.Add(new
                {
                    type = "TextBlock",
                    text = $"**⚡ Tension Points ({tensions.Count})**\n{tensionText}",
                    wrap = true
                });
            }

            // Misalignments
            var misalignments = analysis?.Misalignments ?? new List<Misalignment>();
            if (misalignments.Any())
            {
                var misalignText = string.Join("\n", misalignments.Take(3).Select(m => $"• {m.Description}"));
                bodyItems.Add(new
                {
                    type = "TextBlock",
                    text = $"**🔀 Misalignments ({misalignments.Count})**\n{misalignText}",
                    wrap = true
                });
            }

            // Suggested Actions
            var actions = capsule.SuggestedActions ?? new List<SuggestedActionItem>();
            if (actions.Any())
            {
                var actionsText = string.Join("\n", actions.Take(3).Select(a => $"• {a.Action}"));
                bodyItems.Add(new
                {
                    type = "TextBlock",
                    text = $"**💡 Suggested Actions**\n{actionsText}",
                    wrap = true
                });
            }

            // Usage footer
            var remaining = workspace.GetRemainingAnalyses();
            var usageText = remaining < int.MaxValue
                ? $"{remaining} analyses remaining this month"
                : "Pro tier: Unlimited";

            bodyItems.Add(new
            {
                type = "TextBlock",
                text = usageText,
                size = "small",
                color = remaining < 5 ? "attention" : "default",
                horizontalAlignment = "right"
            });

            var card = new
            {
                type = "AdaptiveCard",
                version = "1.4",
                body = bodyItems.ToArray(),
                actions = new object[]
                {
                    new
                    {
                        type = "Action.OpenUrl",
                        title = "View Full Report",
                        url = "https://app.threadclear.com"
                    }
                }
            };

            await SendAdaptiveCard(activity, card);
        }

        private async Task SendErrorMessage(TeamsActivity activity, string message)
        {
            var card = new
            {
                type = "AdaptiveCard",
                version = "1.4",
                body = new object[]
                {
                    new
                    {
                        type = "TextBlock",
                        text = "❌ Error",
                        weight = "bolder",
                        color = "attention"
                    },
                    new
                    {
                        type = "TextBlock",
                        text = message,
                        wrap = true
                    }
                }
            };

            await SendAdaptiveCard(activity, card);
        }

        private async Task SendTypingIndicator(TeamsActivity activity)
        {
            try
            {
                var typingActivity = new
                {
                    type = "typing",
                    from = activity.Recipient,
                    recipient = activity.From,
                    conversation = activity.Conversation
                };

                await SendToConversation(activity, typingActivity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send typing indicator");
            }
        }

        private async Task SendAdaptiveCard(TeamsActivity activity, object card)
        {
            var replyActivity = new
            {
                type = "message",
                from = activity.Recipient,
                recipient = activity.From,
                conversation = activity.Conversation,
                attachments = new[]
                {
                    new
                    {
                        contentType = "application/vnd.microsoft.card.adaptive",
                        content = card
                    }
                }
            };

            await SendToConversation(activity, replyActivity);
        }

        private async Task SendToConversation(TeamsActivity activity, object replyActivity)
        {
            var serviceUrl = activity.ServiceUrl?.TrimEnd('/');
            var conversationId = activity.Conversation?.Id;

            if (string.IsNullOrEmpty(serviceUrl) || string.IsNullOrEmpty(conversationId))
            {
                _logger.LogWarning("Missing serviceUrl or conversationId");
                return;
            }

            // Get access token
            var token = await GetBotAccessToken();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Failed to get bot access token");
                return;
            }

            var url = $"{serviceUrl}/v3/conversations/{conversationId}/activities";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(JsonSerializer.Serialize(replyActivity), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to send message to Teams: {StatusCode} - {Error}", response.StatusCode, error);
            }
        }

        private async Task<string?> GetBotAccessToken()
        {
            try
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = MicrosoftAppId,
                    ["client_secret"] = MicrosoftAppPassword,
                    ["scope"] = "https://api.botframework.com/.default"
                });

                var response = await _httpClient.PostAsync(
                    "https://login.microsoftonline.com/40824f33-e126-4b84-b907-700549a0cb79/oauth2/v2.0/token",
                    content);

                var json = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Token response: {Response}", json);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get token: {StatusCode} - {Response}", response.StatusCode, json);
                    return null;
                }

                var data = JsonDocument.Parse(json);

                if (data.RootElement.TryGetProperty("access_token", out var tokenProp))
                {
                    return tokenProp.GetString();
                }

                _logger.LogError("No access_token in response: {Response}", json);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bot access token");
                return null;
            }
        }
    }

    // Teams Activity models
    public class TeamsActivity
    {
        public string? Type { get; set; }
        public string? Id { get; set; }
        public string? Timestamp { get; set; }
        public string? ServiceUrl { get; set; }
        public string? ChannelId { get; set; }
        public TeamsAccount? From { get; set; }
        public TeamsConversation? Conversation { get; set; }
        public TeamsAccount? Recipient { get; set; }
        public string? Text { get; set; }
        public List<TeamsEntity>? Entities { get; set; }
        public List<TeamsAccount>? MembersAdded { get; set; }
        public List<TeamsAccount>? MembersRemoved { get; set; }
    }

    public class TeamsAccount
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? AadObjectId { get; set; }
    }

    public class TeamsConversation
    {
        public string? Id { get; set; }
        public string? TenantId { get; set; }
        public string? ConversationType { get; set; }
    }

    public class TeamsEntity
    {
        public string? Type { get; set; }
        public string? Text { get; set; }
        public TeamsAccount? Mentioned { get; set; }
    }
}