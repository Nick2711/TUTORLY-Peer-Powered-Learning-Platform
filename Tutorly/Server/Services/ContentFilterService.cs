using Tutorly.Server.Services;

namespace Tutorly.Server.Services
{
    public class ContentFilterService : IContentFilterService
    {
        private readonly ILogger<ContentFilterService> _logger;
        private readonly HashSet<string> _profanityWords;

        public ContentFilterService(ILogger<ContentFilterService> logger)
        {
            _logger = logger;
            _profanityWords = InitializeProfanityList();
        }

        public async Task<ContentFilterResult> CheckContentAsync(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new ContentFilterResult { IsClean = true };
            }

            var result = new ContentFilterResult();
            var foundWords = new List<string>();
            var warnings = new List<string>();

            // Convert to lowercase for case-insensitive matching
            var lowerContent = content.ToLowerInvariant();

            // Check for profanity
            foreach (var word in _profanityWords)
            {
                if (lowerContent.Contains(word))
                {
                    foundWords.Add(word);
                }
            }

            if (foundWords.Any())
            {
                result.IsClean = false;
                result.BlockedWords = foundWords;
                result.Warnings.Add($"Content contains inappropriate language: {string.Join(", ", foundWords)}");
                result.Warnings.Add("Please revise your content to maintain a respectful learning environment.");
            }
            else
            {
                result.IsClean = true;
            }

            // Additional content checks
            await PerformAdditionalChecks(content, result);

            return result;
        }

        private async Task PerformAdditionalChecks(string content, ContentFilterResult result)
        {
            // Check for excessive caps (shouting)
            var capsCount = content.Count(char.IsUpper);
            var totalChars = content.Count(char.IsLetter);

            if (totalChars > 0 && (double)capsCount / totalChars > 0.7)
            {
                result.Warnings.Add("Please avoid excessive use of capital letters.");
            }

            // Check for excessive punctuation
            var punctuationCount = content.Count(char.IsPunctuation);
            if (content.Length > 0 && (double)punctuationCount / content.Length > 0.3)
            {
                result.Warnings.Add("Please avoid excessive use of punctuation marks.");
            }

            // Check for spam-like patterns (repeated characters)
            if (HasRepeatedCharacters(content))
            {
                result.Warnings.Add("Please avoid repeated characters or spam-like content.");
            }

            await Task.CompletedTask;
        }

        private static bool HasRepeatedCharacters(string content)
        {
            if (string.IsNullOrEmpty(content))
                return false;

            var maxRepeats = 0;
            var currentRepeats = 1;
            var currentChar = content[0];

            for (int i = 1; i < content.Length; i++)
            {
                if (content[i] == currentChar)
                {
                    currentRepeats++;
                }
                else
                {
                    maxRepeats = Math.Max(maxRepeats, currentRepeats);
                    currentRepeats = 1;
                    currentChar = content[i];
                }
            }

            maxRepeats = Math.Max(maxRepeats, currentRepeats);
            return maxRepeats >= 4; // Allow up to 3 repeated characters
        }

        private static HashSet<string> InitializeProfanityList()
        {
            // Basic profanity filter - in production, this would be more comprehensive
            // and potentially loaded from a database or external service
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // Common profanity words
                "damn", "hell", "crap", "stupid", "idiot", "moron",
                "hate", "kill", "die", "death", "suicide",
                "fuck", "shit", "bitch", "asshole", "bastard",
                "nigger", "faggot", "retard", "gay", "lesbian",
                "sex", "porn", "nude", "naked", "breast", "penis", "vagina",
                "drug", "cocaine", "marijuana", "weed", "alcohol",
                "violence", "murder", "rape", "abuse", "torture",
                "nazi", "hitler", "fascist", "racist", "sexist",
                "terrorist", "bomb", "weapon", "gun", "knife",
                "scam", "fraud", "steal", "rob", "cheat", "lie"
            };
        }
    }
}

