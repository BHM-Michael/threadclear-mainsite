using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;
using ThreadClear.Functions.Helpers;

namespace ThreadClear.Functions.Functions
{
    public class AnalyzeConversation
    {
        private readonly ILogger _logger;
        private readonly IConversationParser _parser;
        private readonly IConversationAnalyzer _analyzer;
        private readonly IThreadCapsuleBuilder _builder;
        private readonly IUserService _userService;
        private readonly IOrganizationService _organizationService;
        private readonly IInsightService _insightService;
        private readonly IAIService _aiService;
        private readonly ITaxonomyService _taxonomyService;

        public AnalyzeConversation(
            ILoggerFactory loggerFactory,
            IConversationParser parser,
            IConversationAnalyzer analyzer,
            IThreadCapsuleBuilder builder,
            IUserService userService,
            IOrganizationService organizationService,
            IInsightService insightService,
            IAIService aiService,
            ITaxonomyService taxonomyService)
        {
            _logger = loggerFactory.CreateLogger<AnalyzeConversation>();
            _parser = parser;
            _analyzer = analyzer;
            _builder = builder;
            _userService = userService;
            _organizationService = organizationService;
            _insightService = insightService;
            _aiService = aiService;
            _taxonomyService = taxonomyService;
        }

        [Function("AnalyzeConversation")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "analyze")]
            HttpRequestData req)
        {
            if (req.Method == "OPTIONS")
            {
                var corsResponse = req.CreateResponse(HttpStatusCode.OK);
                return corsResponse;
            }

            _logger.LogInformation("Processing conversation analysis request");

            try
            {
                var authenticatedUser = await AuthenticateUser(req);

                var request = await req.ReadFromJsonAsync<AnalysisRequest>();

                if (request == null || string.IsNullOrWhiteSpace(request.ConversationText))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request");
                }

                ParsingMode? mode = request.ParsingMode;

                if (mode == null && !string.IsNullOrEmpty(request.PriorityLevel))
                {
                    mode = request.PriorityLevel?.ToLower() switch
                    {
                        "high" => ParsingMode.Advanced,
                        "low" => ParsingMode.Basic,
                        _ => null
                    };
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();

                var capsule = await _parser.ParseConversation(
                    request.ConversationText,
                    request.SourceType ?? "simple",
                    mode);

                _logger.LogInformation("TIMING: ParseConversation took {Ms}ms", sw.ElapsedMilliseconds);
                sw.Restart();

                // Load taxonomy for user's organization
                TaxonomyData? taxonomy = null;
                if (authenticatedUser != null)
                {
                    try
                    {
                        var userOrgs = await _organizationService.GetUserOrganizations(authenticatedUser.Id);
                        if (userOrgs?.Count > 0)
                        {
                            taxonomy = await _taxonomyService.GetTaxonomyForOrganization(userOrgs[0].Id);
                            _logger.LogInformation("Loaded taxonomy for org {OrgId}", userOrgs[0].Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load taxonomy, proceeding without it");
                    }
                }

                // Run analysis with taxonomy context
                var options = new AnalysisOptions
                {
                    EnableUnansweredQuestions = authenticatedUser?.Permissions?.UnansweredQuestions ?? true,
                    EnableTensionPoints = authenticatedUser?.Permissions?.TensionPoints ?? true,
                    EnableMisalignments = authenticatedUser?.Permissions?.Misalignments ?? true,
                    EnableConversationHealth = authenticatedUser?.Permissions?.ConversationHealth ?? true,
                    EnableSuggestedActions = authenticatedUser?.Permissions?.SuggestedActions ?? true
                };

                await _analyzer.AnalyzeConversation(capsule, options, taxonomy);

                var modeUsed = capsule.Metadata.TryGetValue("ParsingMode", out var pm) ? pm : "Advanced";

                _logger.LogInformation("Parsed conversation {Id} using {Mode} mode",
                    capsule.CapsuleId, modeUsed);

                _logger.LogInformation("Analysis complete - {Questions} questions, {Tensions} tensions, {Misalignments} misalignments",
                    capsule.Analysis?.UnansweredQuestions?.Count ?? 0,
                    capsule.Analysis?.TensionPoints?.Count ?? 0,
                    capsule.Analysis?.Misalignments?.Count ?? 0);

                await _builder.CalculateMetadata(capsule);

                _logger.LogInformation("TIMING: CalculateMetadata took {Ms}ms", sw.ElapsedMilliseconds);

                DraftAnalysis? draftAnalysis = null;
                if (!string.IsNullOrWhiteSpace(request.DraftMessage))
                {
                    _logger.LogInformation("Analyzing draft message");
                    draftAnalysis = await _analyzer.AnalyzeDraft(capsule, request.DraftMessage);
                }

                await StoreInsightAsync(authenticatedUser, capsule, request.SourceType ?? "simple");

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    capsule = capsule,
                    parsingMode = modeUsed,
                    draftAnalysis = draftAnalysis,
                    user = authenticatedUser?.Email
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing conversation");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError,
                    "An error occurred processing your request");
            }
        }

        /// <summary>
        /// Streaming endpoint for real-time analysis updates
        /// </summary>
        [Function("AnalyzeConversationStream")]
        public async Task<HttpResponseData> RunStream(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "analyze/stream")]
            HttpRequestData req)
        {
            if (req.Method == "OPTIONS")
            {
                var corsResponse = req.CreateResponse(HttpStatusCode.OK);
                corsResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                corsResponse.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                corsResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-User-Email, X-User-Password");
                return corsResponse;
            }

            _logger.LogInformation("Processing streaming conversation analysis request");

            try
            {
                var authenticatedUser = await AuthenticateUser(req);

                var request = await req.ReadFromJsonAsync<AnalysisRequest>();

                if (request == null || string.IsNullOrWhiteSpace(request.ConversationText))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request");
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/event-stream");
                response.Headers.Add("Cache-Control", "no-cache");
                response.Headers.Add("Connection", "keep-alive");
                response.Headers.Add("Access-Control-Allow-Origin", "*");

                var sourceType = DetectSourceType(request.ConversationText, request.SourceType);
                var prompt = BuildAnalysisPrompt(request.ConversationText, sourceType);

                // Send initial event
                var initialEvent = JsonSerializer.Serialize(new { status = "started", message = "Analysis started..." });
                var bodyBytes = new MemoryStream();
                var writer = new StreamWriter(bodyBytes, Encoding.UTF8) { AutoFlush = true };

                await writer.WriteAsync($"data: {initialEvent}\n\n");

                var fullResponse = new StringBuilder();
                var chunkCount = 0;

                await foreach (var chunk in _aiService.StreamResponseAsync(prompt))
                {
                    fullResponse.Append(chunk);
                    chunkCount++;

                    // Send chunk every few pieces to avoid overwhelming the client
                    if (chunkCount % 3 == 0)
                    {
                        var chunkEvent = JsonSerializer.Serialize(new
                        {
                            status = "streaming",
                            chunk = chunk,
                            totalLength = fullResponse.Length
                        });
                        await writer.WriteAsync($"data: {chunkEvent}\n\n");
                    }
                }

                // Parse the complete response
                var capsule = ParseCompleteResponse(fullResponse.ToString(), sourceType, request.ConversationText);

                // Calculate metadata
                await _builder.CalculateMetadata(capsule);

                // Store insight
                await StoreInsightAsync(authenticatedUser, capsule, sourceType);

                // Send final event with complete capsule
                var finalEvent = JsonSerializer.Serialize(new
                {
                    status = "complete",
                    capsule = capsule,
                    user = authenticatedUser?.Email
                });
                await writer.WriteAsync($"data: {finalEvent}\n\n");

                // Copy to response body
                bodyBytes.Position = 0;
                await bodyBytes.CopyToAsync(response.Body);

                _logger.LogInformation("Streaming analysis complete - {MsgCount} messages", capsule.Messages.Count);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing streaming conversation");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError,
                    "An error occurred processing your request");
            }
        }

        private async Task<User?> AuthenticateUser(HttpRequestData req)
        {
            User? authenticatedUser = null;

            if (req.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                var authHeader = authHeaders.FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring("Bearer ".Length);
                    authenticatedUser = await _userService.ValidateToken(token);

                    if (authenticatedUser != null)
                    {
                        _logger.LogInformation("Authenticated user via token: {Email}", authenticatedUser.Email);
                    }
                }
            }

            if (authenticatedUser == null)
            {
                var email = req.Headers.TryGetValues("X-User-Email", out var emailValues) ? emailValues.FirstOrDefault() : null;
                var password = req.Headers.TryGetValues("X-User-Password", out var passValues) ? passValues.FirstOrDefault() : null;

                if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
                {
                    authenticatedUser = await _userService.ValidateLogin(email, password);
                    if (authenticatedUser != null)
                    {
                        _logger.LogInformation("Authenticated user via headers: {Email}", authenticatedUser.Email);
                    }
                }
            }

            if (authenticatedUser == null)
            {
                _logger.LogInformation("Unauthenticated request - limited features, no insight storage");
            }

            return authenticatedUser;
        }

        private string DetectSourceType(string text, string? provided)
        {
            if (!string.IsNullOrEmpty(provided) && provided.ToLower() != "simple")
                return provided;

            if (Regex.IsMatch(text, @"^From:\s*.+", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                return "email";

            var outlookPattern = new Regex(@"^(You|\w+\s+\w+)\s*\n\w{3}\s+\d{1,2}/\d{1,2}/\d{4}\s+\d{1,2}:\d{2}\s*(AM|PM)",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (outlookPattern.Matches(text).Count >= 2)
                return "email";

            return "conversation";
        }

        private string BuildAnalysisPrompt(string conversationText, string sourceType)
        {
            return $@"Analyze this {sourceType} conversation completely. Return a single JSON object.

CONVERSATION:
{conversationText}

Return this exact JSON structure:
{{
  ""participants"": [{{""name"": ""Full Name"", ""email"": ""email or null""}}],
  ""messages"": [{{""sender"": ""Full Name"", ""timestamp"": ""ISO datetime"", ""content"": ""actual message text only""}}],
  ""summary"": ""2-3 sentence summary"",
  ""unansweredQuestions"": [{{""question"": ""exact question ending with ?"", ""askedBy"": ""name""}}],
  ""tensionPoints"": [{{""description"": ""what tension exists"", ""severity"": ""Low|Medium|High"", ""participants"": [""names""]}}],
  ""misalignments"": [{{""topic"": ""what they disagree about"", ""severity"": ""Low|Medium|High"", ""participantsInvolved"": [""names""]}}],
  ""conversationHealth"": {{""overallScore"": 75, ""riskLevel"": ""Low|Medium|High"", ""issues"": [], ""strengths"": []}},
  ""suggestedActions"": [{{""action"": ""specific next step"", ""priority"": ""Low|Medium|High""}}]
}}

RULES:
- participants: Extract real names, not email headers
- messages: Include ONLY actual message text. EXCLUDE signatures, job titles, phone numbers, disclaimers
- messages: Return in chronological order (oldest first)
- unansweredQuestions: Only DIRECT questions with '?' that got no response
- tensionPoints: Identify frustration, urgency, repeated requests, conflict
- JSON only, no markdown.";
        }

        private ThreadCapsule ParseCompleteResponse(string json, string sourceType, string rawText)
        {
            var capsule = new ThreadCapsule
            {
                CapsuleId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                SourceType = sourceType,
                RawText = rawText
            };

            capsule.Metadata["ParsingMode"] = "Advanced";
            capsule.Metadata["DetectedSourceType"] = sourceType;

            try
            {
                var cleanJson = JsonHelper.CleanJsonResponse(json);
                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                // Participants
                capsule.Participants = new System.Collections.Generic.List<Participant>();
                if (root.TryGetProperty("participants", out var participants))
                {
                    int pIndex = 1;
                    foreach (var p in participants.EnumerateArray())
                    {
                        var name = p.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        if (string.IsNullOrWhiteSpace(name) || IsEmailHeader(name)) continue;

                        capsule.Participants.Add(new Participant
                        {
                            Id = $"p{pIndex++}",
                            Name = name,
                            Email = p.TryGetProperty("email", out var e) ? e.GetString() : null
                        });
                    }
                }

                // Messages
                capsule.Messages = new System.Collections.Generic.List<Message>();
                if (root.TryGetProperty("messages", out var messages))
                {
                    int mIndex = 1;
                    foreach (var m in messages.EnumerateArray())
                    {
                        var content = m.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                        if (string.IsNullOrWhiteSpace(content)) continue;

                        var sender = m.TryGetProperty("sender", out var s) ? s.GetString() ?? "Unknown" : "Unknown";

                        var message = new Message
                        {
                            Id = $"msg{mIndex++}",
                            ParticipantId = sender,
                            Content = content,
                            Timestamp = DateTime.UtcNow
                        };

                        if (m.TryGetProperty("timestamp", out var ts) && DateTime.TryParse(ts.GetString(), out var parsed))
                            message.Timestamp = parsed;

                        capsule.Messages.Add(message);
                    }
                }

                LinkMessagesToParticipants(capsule);

                // Summary
                if (root.TryGetProperty("summary", out var summary))
                    capsule.Summary = summary.GetString() ?? "";

                // Analysis
                capsule.Analysis = new ConversationAnalysis();

                // Unanswered Questions
                if (root.TryGetProperty("unansweredQuestions", out var uq))
                {
                    capsule.Analysis.UnansweredQuestions = uq.EnumerateArray()
                        .Select(q => new UnansweredQuestion
                        {
                            Question = q.TryGetProperty("question", out var qt) ? qt.GetString() ?? "" : "",
                            AskedBy = q.TryGetProperty("askedBy", out var ab) ? ab.GetString() ?? "" : "",
                            TimesAsked = 1,
                            AskedAt = DateTime.UtcNow
                        })
                        .Where(q => !string.IsNullOrWhiteSpace(q.Question) && q.Question.Contains("?"))
                        .ToList();
                }
                else
                {
                    capsule.Analysis.UnansweredQuestions = new System.Collections.Generic.List<UnansweredQuestion>();
                }

                // Tension Points
                if (root.TryGetProperty("tensionPoints", out var tp))
                {
                    capsule.Analysis.TensionPoints = tp.EnumerateArray()
                        .Select(t => new TensionPoint
                        {
                            Description = t.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                            Severity = t.TryGetProperty("severity", out var sv) ? sv.GetString() ?? "Low" : "Low",
                            Type = "Detected",
                            Timestamp = DateTime.UtcNow,
                            DetectedAt = DateTime.UtcNow,
                            Participants = t.TryGetProperty("participants", out var ps)
                                ? ps.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                                : new System.Collections.Generic.List<string>()
                        })
                        .Where(t => !string.IsNullOrWhiteSpace(t.Description))
                        .ToList();
                }
                else
                {
                    capsule.Analysis.TensionPoints = new System.Collections.Generic.List<TensionPoint>();
                }

                // Misalignments
                if (root.TryGetProperty("misalignments", out var ma))
                {
                    capsule.Analysis.Misalignments = ma.EnumerateArray()
                        .Select(m => new Misalignment
                        {
                            Type = m.TryGetProperty("topic", out var t) ? t.GetString() ?? "" : "",
                            Severity = m.TryGetProperty("severity", out var sv) ? sv.GetString() ?? "Low" : "Low",
                            ParticipantsInvolved = m.TryGetProperty("participantsInvolved", out var pi)
                                ? pi.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                                : new System.Collections.Generic.List<string>()
                        })
                        .Where(m => !string.IsNullOrWhiteSpace(m.Type))
                        .ToList();
                }
                else
                {
                    capsule.Analysis.Misalignments = new System.Collections.Generic.List<Misalignment>();
                }

                // Conversation Health
                if (root.TryGetProperty("conversationHealth", out var ch))
                {
                    capsule.Analysis.ConversationHealth = new ConversationHealth
                    {
                        HealthScore = ch.TryGetProperty("overallScore", out var os) ? os.GetInt32() / 100.0 : 0.5,
                        RiskLevel = ch.TryGetProperty("riskLevel", out var rl) ? rl.GetString() ?? "Low" : "Low",
                        ClarityScore = 0.5,
                        ResponsivenessScore = 0.5,
                        AlignmentScore = 0.5,
                        Issues = ch.TryGetProperty("issues", out var iss)
                            ? iss.EnumerateArray().Select(x => x.GetString()).ToList()
                            : new System.Collections.Generic.List<string?>(),
                        Strengths = ch.TryGetProperty("strengths", out var str)
                            ? str.EnumerateArray().Select(x => x.GetString()).ToList()
                            : new System.Collections.Generic.List<string?>(),
                        Recommendations = new System.Collections.Generic.List<string?>()
                    };
                }
                else
                {
                    capsule.Analysis.ConversationHealth = new ConversationHealth
                    {
                        HealthScore = 0.5,
                        RiskLevel = "Unknown"
                    };
                }

                // Suggested Actions
                if (root.TryGetProperty("suggestedActions", out var sa))
                {
                    capsule.SuggestedActions = sa.EnumerateArray()
                        .Select(a => new SuggestedActionItem
                        {
                            Action = a.TryGetProperty("action", out var act) ? act.GetString() ?? "" : "",
                            Priority = a.TryGetProperty("priority", out var pr) ? pr.GetString() ?? "Medium" : "Medium",
                            Reasoning = a.TryGetProperty("reasoning", out var r) ? r.GetString() : null
                        })
                        .Where(a => !string.IsNullOrWhiteSpace(a.Action))
                        .ToList();
                }
                else
                {
                    capsule.SuggestedActions = new System.Collections.Generic.List<SuggestedActionItem>();
                }

                capsule.Analysis.Decisions = new System.Collections.Generic.List<DecisionPoint>();
                capsule.Analysis.ActionItems = new System.Collections.Generic.List<ActionItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing AI response");
                capsule.Participants = new System.Collections.Generic.List<Participant>();
                capsule.Messages = new System.Collections.Generic.List<Message>();
                capsule.Analysis = new ConversationAnalysis
                {
                    UnansweredQuestions = new System.Collections.Generic.List<UnansweredQuestion>(),
                    TensionPoints = new System.Collections.Generic.List<TensionPoint>(),
                    Misalignments = new System.Collections.Generic.List<Misalignment>(),
                    ConversationHealth = new ConversationHealth { HealthScore = 0.5, RiskLevel = "Unknown" },
                    Decisions = new System.Collections.Generic.List<DecisionPoint>(),
                    ActionItems = new System.Collections.Generic.List<ActionItem>()
                };
                capsule.SuggestedActions = new System.Collections.Generic.List<SuggestedActionItem>();
                capsule.Summary = "Unable to analyze conversation.";
            }

            return capsule;
        }

        private void LinkMessagesToParticipants(ThreadCapsule capsule)
        {
            foreach (var message in capsule.Messages)
            {
                var participant = capsule.Participants.FirstOrDefault(p =>
                    string.Equals(p.Name, message.ParticipantId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Email, message.ParticipantId, StringComparison.OrdinalIgnoreCase));

                if (participant != null)
                {
                    message.ParticipantId = participant.Id;
                }
            }
        }

        private bool IsEmailHeader(string name)
        {
            var headers = new[] { "From", "To", "Cc", "Bcc", "Subject", "Date", "Sent", "Re", "Fw", "Fwd", "Mobile" };
            return headers.Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        private async Task StoreInsightAsync(User? user, ThreadCapsule capsule, string sourceType)
        {
            if (user == null)
            {
                _logger.LogDebug("Skipping insight storage - no authenticated user");
                return;
            }

            try
            {
                var userOrgs = await _organizationService.GetUserOrganizations(user.Id);
                if (userOrgs == null || userOrgs.Count == 0)
                {
                    _logger.LogDebug("Skipping insight storage - user has no organization");
                    return;
                }

                var orgId = userOrgs[0].Id;

                await _insightService.StoreInsight(orgId, user.Id, capsule);

                _logger.LogInformation("Stored insight for user {UserId} in org {OrgId}", user.Id, orgId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store insight for user {UserId}", user?.Id);
            }
        }

        private async Task<HttpResponseData> CreateErrorResponse(
            HttpRequestData req,
            HttpStatusCode statusCode,
            string message)
        {
            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(new
            {
                success = false,
                error = message
            });
            return response;
        }
    }
}