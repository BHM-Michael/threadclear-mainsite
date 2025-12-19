using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    /// <summary>
    /// Builds and enriches ThreadCapsule with metadata and analysis
    /// </summary>
    public class ThreadCapsuleBuilder : IThreadCapsuleBuilder
    {
        private readonly IAIService? _aiService;

        public ThreadCapsuleBuilder(IAIService? aiService = null)
        {
            _aiService = aiService; // Optional - can work without AI
        }

        public async Task<ThreadCapsule> BuildCapsule(List<Message> messages, List<Participant> participants)
        {
            var capsule = new ThreadCapsule
            {
                CapsuleId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                Messages = messages,
                Participants = participants,
                Analysis = new ConversationAnalysis()
            };

            await CalculateMetadata(capsule);
            await EnrichWithLinguisticFeatures(capsule);

            if (_aiService != null)
            {
                capsule.Summary = await GenerateSummary(capsule);
                capsule.KeyPoints = await ExtractKeyPointsWithAI(capsule);
            }
            else
            {
                capsule.Summary = GenerateBasicSummary(capsule);
                capsule.KeyPoints = GenerateBasicKeyPoints(capsule);
            }

            return capsule;
        }

        public async Task EnrichWithLinguisticFeatures(ThreadCapsule capsule)
        {
            foreach (var message in capsule.Messages)
            {
                if (message.LinguisticFeatures == null)
                {
                    message.LinguisticFeatures = new LinguisticFeatures();
                }

                // Use basic linguistic analysis (AI analysis happens in ConversationAnalyzer)
                if (!string.IsNullOrEmpty(message.Content))
                {
                    // Basic question detection
                    message.LinguisticFeatures.ContainsQuestion = message.Content.Contains("?");
                    if (message.LinguisticFeatures.ContainsQuestion)
                    {
                        message.LinguisticFeatures.Questions = ExtractBasicQuestions(message.Content);
                    }

                    // Basic word/sentence count
                    message.LinguisticFeatures.WordCount = message.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                    message.LinguisticFeatures.SentenceCount = message.Content.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).Length;
                }
            }

            await Task.CompletedTask;
        }

        private List<string> ExtractBasicQuestions(string content)
        {
            var questions = new List<string>();
            var sentences = content.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var sentence in sentences)
            {
                var trimmed = sentence.Trim();
                // Check if original content had this sentence ending with ?
                var index = content.IndexOf(trimmed, StringComparison.Ordinal);
                if (index >= 0 && index + trimmed.Length < content.Length)
                {
                    var nextChar = content[index + trimmed.Length];
                    if (nextChar == '?')
                    {
                        questions.Add(trimmed + "?");
                    }
                }
            }

            return questions;
        }

        public async Task CalculateMetadata(ThreadCapsule capsule)
        {
            if (!capsule.Messages.Any())
            {
                return;
            }

            // Sort messages by timestamp
            capsule.Messages = capsule.Messages.OrderBy(m => m.Timestamp).ToList();

            // Calculate timeline
            var firstMessage = capsule.Messages.First();
            var lastMessage = capsule.Messages.Last();

            capsule.Metadata["StartDate"] = firstMessage.Timestamp.ToString("o");
            capsule.Metadata["EndDate"] = lastMessage.Timestamp.ToString("o");
            capsule.Metadata["DurationDays"] = (lastMessage.Timestamp - firstMessage.Timestamp).TotalDays.ToString("F2");
            capsule.Metadata["MessageCount"] = capsule.Messages.Count.ToString();
            capsule.Metadata["ParticipantCount"] = capsule.Participants.Count.ToString();

            // Calculate response times
            var responseTimes = CalculateResponseTimes(capsule);
            if (responseTimes.Any())
            {
                capsule.Metadata["AverageResponseTimeHours"] = responseTimes.Select(t => t.TotalHours).Average().ToString("F2");
                capsule.Metadata["MedianResponseTimeHours"] = GetMedian(responseTimes.Select(t => t.TotalHours)).ToString("F2");
            }

            // Calculate participant activity
            var participantActivity = capsule.Messages
                .GroupBy(m => m.ParticipantId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count()
                );

            capsule.Metadata["ParticipantActivity"] = System.Text.Json.JsonSerializer.Serialize(participantActivity);

            // Identify thread initiator
            capsule.Metadata["ThreadInitiator"] = firstMessage.ParticipantId;

            await Task.CompletedTask;
        }

        public async Task<string> GenerateSummary(ThreadCapsule capsule)
        {
            if (_aiService != null)
            {
                var prompt = $@"Provide a concise 2-3 sentence summary of this conversation.

Participants: {string.Join(", ", capsule.Participants.Select(p => p.Name))}
Messages: {capsule.Messages.Count}

Conversation:
{string.Join("\n", capsule.Messages.Select(m => $"{capsule.Participants.FirstOrDefault(p => p.Id == m.ParticipantId)?.Name ?? m.ParticipantId}: {m.Content}"))}

Return ONLY the summary text, no JSON formatting.";

                var result = await _aiService.AnalyzeConversation(prompt, capsule);
                return string.IsNullOrEmpty(result) ? GenerateBasicSummary(capsule) : result;
            }

            return GenerateBasicSummary(capsule);
        }

        private async Task<List<string>> ExtractKeyPointsWithAI(ThreadCapsule capsule)
        {
            if (_aiService == null) return GenerateBasicKeyPoints(capsule);

            var prompt = $@"Extract 3-5 key points from this conversation. Return as a JSON array of strings.

Conversation:
{string.Join("\n", capsule.Messages.Select(m => $"{capsule.Participants.FirstOrDefault(p => p.Id == m.ParticipantId)?.Name ?? m.ParticipantId}: {m.Content}"))}

Return ONLY a JSON array like: [""point 1"", ""point 2"", ""point 3""]";

            var result = await _aiService.AnalyzeConversation(prompt, capsule);

            try
            {
                var keyPoints = System.Text.Json.JsonSerializer.Deserialize<List<string>>(result);
                return keyPoints ?? GenerateBasicKeyPoints(capsule);
            }
            catch
            {
                return GenerateBasicKeyPoints(capsule);
            }
        }

        #region Private Helper Methods

        private string GenerateBasicSummary(ThreadCapsule capsule)
        {
            var participantCount = capsule.Participants.Count;
            var messageCount = capsule.Messages.Count;
            var questionCount = capsule.Messages.Sum(m => m.LinguisticFeatures?.Questions?.Count ?? 0);

            var summary = $"Conversation between {participantCount} participant(s) with {messageCount} message(s).";

            if (questionCount > 0)
            {
                summary += $" Contains {questionCount} question(s).";
            }

            var urgentMessages = capsule.Messages.Count(m =>
                m.LinguisticFeatures?.Urgency?.Equals("High", StringComparison.OrdinalIgnoreCase) == true);

            if (urgentMessages > 0)
            {
                summary += $" {urgentMessages} message(s) marked as urgent.";
            }

            return summary;
        }

        private List<string> GenerateBasicKeyPoints(ThreadCapsule capsule)
        {
            var keyPoints = new List<string>();

            // Add conversation duration
            if (capsule.Messages.Count > 1)
            {
                var duration = capsule.Messages.Last().Timestamp - capsule.Messages.First().Timestamp;
                if (duration.TotalDays > 1)
                {
                    keyPoints.Add($"Conversation spanned {duration.TotalDays:F1} days");
                }
                else if (duration.TotalHours > 1)
                {
                    keyPoints.Add($"Conversation lasted {duration.TotalHours:F1} hours");
                }
            }

            // Add participant info
            var mostActive = capsule.Messages
                .GroupBy(m => m.ParticipantId)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (mostActive != null)
            {
                var participant = capsule.Participants.FirstOrDefault(p => p.Id == mostActive.Key);
                if (participant != null)
                {
                    keyPoints.Add($"Most active: {participant.Name} ({mostActive.Count()} messages)");
                }
            }

            // Add question count
            var totalQuestions = capsule.Messages.Sum(m => m.LinguisticFeatures?.Questions?.Count ?? 0);
            if (totalQuestions > 0)
            {
                keyPoints.Add($"{totalQuestions} question(s) asked");
            }

            // Add sentiment summary
            var sentiments = capsule.Messages
                .Where(m => !string.IsNullOrEmpty(m.LinguisticFeatures?.Sentiment))
                .GroupBy(m => m.LinguisticFeatures.Sentiment)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (sentiments != null)
            {
                keyPoints.Add($"Overall tone: {sentiments.Key}");
            }

            return keyPoints;
        }

        private List<TimeSpan> CalculateResponseTimes(ThreadCapsule capsule)
        {
            var responseTimes = new List<TimeSpan>();

            for (int i = 1; i < capsule.Messages.Count; i++)
            {
                var currentMessage = capsule.Messages[i];
                var previousMessage = capsule.Messages[i - 1];

                // Only count as response if different participant
                if (currentMessage.ParticipantId != previousMessage.ParticipantId)
                {
                    var responseTime = currentMessage.Timestamp - previousMessage.Timestamp;
                    if (responseTime.TotalDays < 30) // Filter out unrealistic gaps
                    {
                        responseTimes.Add(responseTime);
                    }
                }
            }

            return responseTimes;
        }

        private double GetMedian(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            if (!sorted.Any()) return 0;

            int count = sorted.Count;
            if (count % 2 == 0)
            {
                return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
            }
            else
            {
                return sorted[count / 2];
            }
        }

        #endregion
    }
}
