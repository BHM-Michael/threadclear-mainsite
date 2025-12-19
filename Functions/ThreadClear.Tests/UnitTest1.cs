using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Implementations;
using ThreadClear.Functions.Services.Interfaces;
using Xunit;
//using Moq;

namespace ThreadClear.Tests
{
    /// <summary>
    /// Mock AI service for testing - returns predictable responses
    /// </summary>
    public class MockAIService : IAIService
    {
        public Task<string> GenerateResponseAsync(string prompt)
        {
            return Task.FromResult("Mock response");
        }

        public Task<string> GenerateStructuredResponseAsync(string prompt)
        {
            return Task.FromResult("{}");
        }

        public Task<string> AnalyzeTextAsync(string text)
        {
            return Task.FromResult("{\"sentiment\": \"neutral\", \"urgency\": \"low\"}");
        }

        public Task<string> AnalyzeConversation(string prompt, ThreadCapsule? capsule)
        {
            // Return empty JSON for basic testing
            if (prompt.Contains("unanswered"))
                return Task.FromResult("{\"unansweredQuestions\": []}");
            if (prompt.Contains("tension"))
                return Task.FromResult("{\"tensionPoints\": []}");
            if (prompt.Contains("misalignment"))
                return Task.FromResult("{\"misalignments\": []}");
            if (prompt.Contains("decision"))
                return Task.FromResult("{\"decisions\": []}");
            if (prompt.Contains("action"))
                return Task.FromResult("{\"actionItems\": []}");
            if (prompt.Contains("health"))
                return Task.FromResult("{\"health\": {\"riskLevel\": \"Low\", \"healthScore\": 75, \"issues\": [], \"strengths\": [], \"recommendations\": []}}");

            return Task.FromResult("{}");
        }

        public Task<List<string>> GenerateSuggestedActions(ThreadCapsule capsule)
        {
            return Task.FromResult(new List<string> { "Review conversation", "Follow up on items" });
        }

        public Task<string> ExtractTextFromImage(string base64Image, string mimeType)
        {
            // Return a sample conversation for testing
            return Task.FromResult("John: Hey, did you get my message?\nJane: Yes, I'll look into it today.\nJohn: Thanks!");
        }
    }

    public class ConversationAnalyzerTests
    {
        private readonly ConversationAnalyzer _analyzer;

        public ConversationAnalyzerTests()
        {
//            _analyzer = new ConversationAnalyzer(
//    new MockAIService(),
//    new Mock<ILogger<ConversationAnalyzer>>().Object
//);
        }

        [Fact]
        public async Task DetectUnansweredQuestions_FindsUnansweredQuestion()
        {
            // Arrange - Create a mock conversation with an unanswered question
            var capsule = CreateMockCapsule();

            // Add a question that's never answered
            var questionMsg = new Message
            {
                Id = "msg3",
                ParticipantId = "p1",
                Timestamp = DateTime.UtcNow.AddDays(-2),
                Content = "Can you send me the proposal by Friday?",
                LinguisticFeatures = new LinguisticFeatures
                {
                    ContainsQuestion = true,
                    Questions = new List<string> { "Can you send me the proposal by Friday?" }
                }
            };

            capsule.Messages.Add(questionMsg);

            // Act
            var unanswered = await _analyzer.DetectUnansweredQuestions(capsule);

            // Assert
            Assert.NotEmpty(unanswered);
            Assert.Contains(unanswered, q => q.Question.Contains("proposal"));
            Assert.True(unanswered[0].DaysUnanswered >= 2);
        }

        [Fact]
        public async Task DetectUnansweredQuestions_IgnoresAnsweredQuestion()
        {
            // Arrange
            var capsule = CreateMockCapsule();

            // Question
            var questionMsg = new Message
            {
                Id = "msg3",
                ParticipantId = "p1",
                Timestamp = DateTime.UtcNow.AddHours(-2),
                Content = "What time is the meeting?",
                LinguisticFeatures = new LinguisticFeatures
                {
                    ContainsQuestion = true,
                    Questions = new List<string> { "What time is the meeting?" }
                }
            };

            // Answer
            var answerMsg = new Message
            {
                Id = "msg4",
                ParticipantId = "p2",
                Timestamp = DateTime.UtcNow.AddHours(-1),
                Content = "The meeting is at 3pm",
                ResponseTo = "msg3"
            };

            capsule.Messages.Add(questionMsg);
            capsule.Messages.Add(answerMsg);

            // Act
            var unanswered = await _analyzer.DetectUnansweredQuestions(capsule);

            // Assert
            Assert.Empty(unanswered);
        }

        [Fact]
        public async Task DetectMisalignments_FindsExpectationMismatch()
        {
            // Arrange
            var capsule = CreateMockCapsule();

            var mismatchMsg = new Message
            {
                Id = "msg3",
                ParticipantId = "p1",
                Timestamp = DateTime.UtcNow.AddDays(-1),
                Content = "I thought the deadline was next week",
                LinguisticFeatures = new LinguisticFeatures()
            };

            capsule.Messages.Add(mismatchMsg);

            // Act
            var misalignments = await _analyzer.DetectMisalignments(capsule);

            // Assert
            Assert.NotEmpty(misalignments);
        }

        [Fact]
        public async Task IdentifyTensionPoints_FindsFrustration()
        {
            // Arrange
            var capsule = CreateMockCapsule();

            var tenseMsg = new Message
            {
                Id = "msg3",
                ParticipantId = "p1",
                Timestamp = DateTime.UtcNow,
                Content = "I'm still waiting for a response. This is urgent and I'm frustrated.",
                Sentiment = new Sentiment
                {
                    Polarity = SentimentPolarity.Negative,
                    Intensity = 0.8
                },
                LinguisticFeatures = new LinguisticFeatures
                {
                    Sentiment = "Negative",
                    Urgency = "High",
                    UrgencyMarkers = new List<string> { "urgent", "still waiting" }
                }
            };

            capsule.Messages.Add(tenseMsg);

            // Act
            var tensions = await _analyzer.IdentifyTensionPoints(capsule);

            // Assert
            Assert.NotEmpty(tensions);
            Assert.Contains(tensions, t => t.Severity == "High" || t.Severity == "Moderate");
        }

        [Fact]
        public async Task AssessConversationHealth_ReturnsValidScores()
        {
            // Arrange
            var capsule = CreateMockCapsule();

            // Add some analysis data
            capsule.Analysis.UnansweredQuestions = new List<UnansweredQuestion>
            {
                new UnansweredQuestion { Question = "Test question", DaysUnanswered = 1 }
            };

            // Act
            var health = await _analyzer.AssessConversationHealth(capsule);

            // Assert
            Assert.NotNull(health);
            Assert.InRange(health.ResponsivenessScore, 0, 1);
            Assert.InRange(health.ClarityScore, 0, 1);
            Assert.InRange(health.AlignmentScore, 0, 1);
            Assert.NotNull(health.RiskLevel); // Risk level should be set
        }

        [Fact]
        public async Task AnalyzeConversation_PopulatesAllFields()
        {
            // Arrange
            var capsule = CreateMockCapsule();

            // Add a complex conversation
            capsule.Messages.Add(new Message
            {
                Id = "msg3",
                ParticipantId = "p1",
                Timestamp = DateTime.UtcNow.AddDays(-2),
                Content = "Can you provide an update? This is urgent.",
                LinguisticFeatures = new LinguisticFeatures
                {
                    ContainsQuestion = true,
                    Questions = new List<string> { "Can you provide an update?" },
                    UrgencyMarkers = new List<string> { "urgent" }
                }
            });

            // Act
            await _analyzer.AnalyzeConversation(capsule);

            // Assert
            Assert.NotNull(capsule.Analysis);
            Assert.NotNull(capsule.Analysis.UnansweredQuestions);
            Assert.NotNull(capsule.Analysis.SilentAssumptions);
            Assert.NotNull(capsule.Analysis.TensionPoints);
            Assert.NotNull(capsule.Analysis.KeyMoments);
            Assert.NotNull(capsule.Analysis.ConversationHealth);
        }

        // Helper method to create a basic mock capsule
        private ThreadCapsule CreateMockCapsule()
        {
            var capsule = new ThreadCapsule
            {
                CapsuleId = Guid.NewGuid().ToString(),
                Version = "1.0.0",
                CreatedAt = DateTime.UtcNow,
                Source = new ConversationSource { Type = SourceType.Email },
                Participants = new List<Participant>
                {
                    new Participant { Id = "p1", Name = "Mike", InferredRole = ParticipantRole.Manager },
                    new Participant { Id = "p2", Name = "Vendor", InferredRole = ParticipantRole.Vendor }
                },
                Messages = new List<Message>
                {
                    new Message
                    {
                        Id = "msg1",
                        ParticipantId = "p1",
                        Timestamp = DateTime.UtcNow.AddDays(-3),
                        Content = "Hello, I wanted to follow up on our discussion.",
                        Sentiment = new Sentiment { Polarity = SentimentPolarity.Neutral }
                    },
                    new Message
                    {
                        Id = "msg2",
                        ParticipantId = "p2",
                        Timestamp = DateTime.UtcNow.AddDays(-2),
                        Content = "Thanks for reaching out.",
                        ResponseTo = "msg1",
                        Sentiment = new Sentiment { Polarity = SentimentPolarity.Positive }
                    }
                },
                ConversationGraph = new ConversationGraph
                {
                    Nodes = new List<string> { "msg1", "msg2" },
                    Edges = new List<GraphEdge>
                    {
                        new GraphEdge { From = "msg1", To = "msg2", Type = EdgeType.Response }
                    }
                },
                Analysis = new ConversationAnalysis()
            };

            return capsule;
        }
    }
}