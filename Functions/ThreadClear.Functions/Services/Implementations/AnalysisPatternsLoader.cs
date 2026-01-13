using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace ThreadClear.Functions.Services.Implementations
{
    /// <summary>
    /// Loads analysis patterns from XML configuration file.
    /// Patterns are cached in memory for performance.
    /// </summary>
    public class AnalysisPatternsLoader
    {
        private readonly ILogger<AnalysisPatternsLoader>? _logger;
        private readonly string _patternsFilePath;
        private Dictionary<string, HashSet<string>>? _patterns;
        private DateTime _lastLoaded = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

        public AnalysisPatternsLoader(string patternsFilePath, ILogger<AnalysisPatternsLoader>? logger = null)
        {
            _patternsFilePath = patternsFilePath;
            _logger = logger;
        }

        /// <summary>
        /// Get patterns for a specific category (e.g., "FrustrationIndicators")
        /// </summary>
        public HashSet<string> GetPatterns(string category)
        {
            EnsurePatternsLoaded();
            
            if (_patterns != null && _patterns.TryGetValue(category, out var patterns))
            {
                return patterns;
            }

            _logger?.LogWarning("Pattern category not found: {Category}", category);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if text contains any word/phrase from a pattern category
        /// </summary>
        public bool ContainsPattern(string text, string category)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var patterns = GetPatterns(category);
            var lowerText = text.ToLower();

            return patterns.Any(pattern => ContainsWord(lowerText, pattern.ToLower()));
        }

        /// <summary>
        /// Find all matching patterns from a category in the text
        /// </summary>
        public List<string> FindMatchingPatterns(string text, string category)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            var patterns = GetPatterns(category);
            var lowerText = text.ToLower();

            return patterns
                .Where(pattern => ContainsWord(lowerText, pattern.ToLower()))
                .ToList();
        }

        /// <summary>
        /// Check if text starts with any question indicator word
        /// </summary>
        public bool StartsWithQuestionWord(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var questionWords = GetPatterns("QuestionIndicators");
            var trimmedLower = text.TrimStart().ToLower();

            return questionWords.Any(word => 
                trimmedLower.StartsWith(word.ToLower() + " ") || 
                trimmedLower.StartsWith(word.ToLower() + ",") ||
                trimmedLower.Equals(word.ToLower()));
        }

        /// <summary>
        /// Reload patterns from XML file (force refresh)
        /// </summary>
        public void ReloadPatterns()
        {
            _lastLoaded = DateTime.MinValue;
            EnsurePatternsLoaded();
        }

        /// <summary>
        /// Get all available pattern categories
        /// </summary>
        public IEnumerable<string> GetCategories()
        {
            EnsurePatternsLoaded();
            return _patterns?.Keys ?? Enumerable.Empty<string>();
        }

        #region Private Methods

        private void EnsurePatternsLoaded()
        {
            // Check if cache is still valid
            if (_patterns != null && DateTime.UtcNow - _lastLoaded < _cacheExpiry)
                return;

            LoadPatternsFromXml();
        }

        private void LoadPatternsFromXml()
        {
            _patterns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!File.Exists(_patternsFilePath))
                {
                    _logger?.LogWarning("Patterns file not found: {Path}. Using default patterns.", _patternsFilePath);
                    LoadDefaultPatterns();
                    return;
                }

                var doc = XDocument.Load(_patternsFilePath);
                var root = doc.Root;

                if (root == null)
                {
                    _logger?.LogWarning("Invalid XML in patterns file. Using default patterns.");
                    LoadDefaultPatterns();
                    return;
                }

                // Load each category
                foreach (var element in root.Elements())
                {
                    var categoryName = element.Name.LocalName;
                    var words = element.Elements("Word")
                        .Select(w => w.Value.Trim())
                        .Where(w => !string.IsNullOrEmpty(w))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    _patterns[categoryName] = words;
                    _logger?.LogDebug("Loaded {Count} patterns for category {Category}", words.Count, categoryName);
                }

                _lastLoaded = DateTime.UtcNow;
                _logger?.LogInformation("Loaded {Count} pattern categories from {Path}", _patterns.Count, _patternsFilePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading patterns from XML. Using default patterns.");
                LoadDefaultPatterns();
            }
        }

        private void LoadDefaultPatterns()
        {
            // Fallback patterns if XML file is not available
            _patterns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["QuestionIndicators"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "what", "when", "where", "who", "why", "how", "can", "could", 
                    "would", "should", "is", "are", "do", "does", "did", "will", "have", "has"
                },
                ["FrustrationIndicators"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "frustrated", "annoyed", "disappointed", "upset", "angry", 
                    "concerned", "worried", "confused", "ridiculous", "unacceptable"
                },
                ["UrgencyIndicators"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "asap", "urgent", "urgently", "immediately", "critical", 
                    "emergency", "now", "right away", "as soon as possible"
                },
                ["RepetitionIndicators"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "again", "already asked", "still waiting", "follow up", 
                    "following up", "reminder", "third time", "second time", "once more"
                },
                ["EscalationIndicators"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "escalate", "manager", "supervisor", "legal", "lawyer", 
                    "attorney", "complaint", "unacceptable", "last time"
                },
                ["DismissiveIndicators"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "whatever", "fine", "sure", "okay then", "if you say so", 
                    "not my problem", "don't care"
                },
                ["NegativeToneIndicators"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "never", "always", "worst", "terrible", "horrible", 
                    "awful", "hate", "stupid", "incompetent"
                },
                ["ActionRequestIndicators"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "can you", "could you", "please", "would you", "will you", 
                    "need you to", "should", "must", "have to"
                },
                ["CommitmentIndicators"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "I will", "I'll", "we will", "we'll", "I can", 
                    "I am going to", "I'm going to", "let me"
                },
                ["DecisionIndicators"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "decided", "agreed", "confirmed", "approved", "let's go with", 
                    "we'll use", "final decision", "settled on"
                },
                ["DisagreementIndicators"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "disagree", "don't think", "not sure about that", "I thought", 
                    "but I", "actually", "incorrect", "wrong", "that's not"
                },
                ["ConfusionIndicators"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "confused", "don't understand", "unclear", "what do you mean", 
                    "not following", "lost me", "clarify"
                },
                ["PositiveIndicators"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "great", "excellent", "perfect", "thank", "thanks", 
                    "appreciate", "happy", "glad", "awesome", "wonderful", "good job"
                },
                ["AssumptionIndicators"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "I thought", "I assumed", "I was under the impression", 
                    "my understanding was", "I believed", "I expected"
                }
            };

            _lastLoaded = DateTime.UtcNow;
        }

        /// <summary>
        /// Check if text contains a word/phrase with word boundary awareness
        /// </summary>
        private bool ContainsWord(string text, string word)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word))
                return false;

            var index = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            
            while (index >= 0)
            {
                // Check word boundaries
                var beforeOk = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
                var afterOk = index + word.Length >= text.Length || !char.IsLetterOrDigit(text[index + word.Length]);

                if (beforeOk && afterOk)
                    return true;

                // Look for next occurrence
                index = text.IndexOf(word, index + 1, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        #endregion
    }
}
