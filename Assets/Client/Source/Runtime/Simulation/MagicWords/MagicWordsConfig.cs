using System;
using System.Collections.Generic;

namespace Client.Simulation.MagicWords
{
    public sealed class MagicWordsConfig
    {
        // Keep these names aligned with the six sprites in the MagicWordsEmoji sprite asset.
        private static readonly string[] DefaultKnownTokens =
        {
            "satisfied",
            "intrigued",
            "neutral",
            "affirmative",
            "laughing",
            "win"
        };

        public MagicWordsConfig(float lineIntervalSeconds = 1f)
            : this(DefaultKnownTokens, lineIntervalSeconds)
        {
        }

        public MagicWordsConfig(IEnumerable<string> knownTokens, float lineIntervalSeconds = 2f)
        {
            if (knownTokens == null)
                throw new ArgumentNullException(nameof(knownTokens));

            if (lineIntervalSeconds <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(lineIntervalSeconds), lineIntervalSeconds, "Line interval must be positive.");

            KnownEmojiTokens = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in knownTokens)
            {
                if (token == null)
                    throw new ArgumentNullException(nameof(knownTokens), "Known tokens cannot contain null.");

                if (string.IsNullOrWhiteSpace(token))
                    throw new ArgumentOutOfRangeException(nameof(knownTokens), token, "Known tokens cannot be empty.");

                KnownEmojiTokens.Add(token);
            }

            if (KnownEmojiTokens.Count == 0)
                throw new ArgumentOutOfRangeException(nameof(knownTokens), "At least one known token is required.");

            LineIntervalSeconds = lineIntervalSeconds;
        }

        public HashSet<string> KnownEmojiTokens { get; }
        public float LineIntervalSeconds { get; }
    }
}
