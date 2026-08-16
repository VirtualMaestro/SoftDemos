using System.Collections.Generic;
using Client.Simulation.MagicWords;
using NUnit.Framework;

namespace Client.Simulation.Tests.MagicWords
{
    public sealed class DialogueTokenizerTests
    {
        private static readonly HashSet<string> KnownTokens = new()
        {
            "satisfied",
            "intrigued",
            "neutral",
            "affirmative",
            "laughing",
            "win"
        };

        [TestCase(null)]
        [TestCase("")]
        public void EmptyInput_ReturnsEmptyArray(string text)
        {
            Assert.That(DialogueTokenizer.Tokenize(text, KnownTokens), Is.Empty);
        }

        [Test]
        public void TextWithoutBraces_RemainsOneLiteralSegment()
        {
            AssertSegments("plain text", KnownTokens, Text("plain text"));
        }

        [Test]
        public void KnownToken_PreservesSurroundingSpaces()
        {
            AssertSegments("a {win} b", KnownTokens, Text("a "), Emoji("win"), Text(" b"));
        }

        [Test]
        public void KnownTokenWithoutText_ReturnsOnlyEmoji()
        {
            AssertSegments("{win}", KnownTokens, Emoji("win"));
        }

        [Test]
        public void AdjacentKnownTokens_ReturnOnlyEmojis()
        {
            AssertSegments("{win}{laughing}", KnownTokens, Emoji("win"), Emoji("laughing"));
        }

        [Test]
        public void UnknownToken_RemainsLiteral()
        {
            AssertSegments("{wat}", KnownTokens, Text("{wat}"));
        }

        [Test]
        public void UnknownToken_MergesIntoLiteralRun()
        {
            AssertSegments("a {wat} b", KnownTokens, Text("a {wat} b"));
        }

        [Test]
        public void UnclosedToken_MergesIntoLiteralRun()
        {
            AssertSegments("a {win", KnownTokens, Text("a {win"));
        }

        [TestCase("{}")]
        [TestCase("{ }")]
        public void EmptyOrWhitespaceToken_RemainsLiteral(string text)
        {
            AssertSegments(text, KnownTokens, Text(text));
        }

        [Test]
        public void UnicodeApostrophe_SurvivesUnchanged()
        {
            AssertSegments("That’s {neutral}", KnownTokens, Text("That’s "), Emoji("neutral"));
        }

        [Test]
        public void MissingCatalog_LeavesTokensLiteral()
        {
            AssertSegments("{win}", null, Text("{win}"));
            AssertSegments("{win}", new HashSet<string>(), Text("{win}"));
        }

        private static DialogueSegment Text(string value) =>
            new() { Kind = SegmentKind.Text, Value = value };

        private static DialogueSegment Emoji(string value) =>
            new() { Kind = SegmentKind.Emoji, Value = value };

        private static void AssertSegments(
            string text,
            HashSet<string> knownTokens,
            params DialogueSegment[] expected)
        {
            var actual = DialogueTokenizer.Tokenize(text, knownTokens);
            Assert.That(actual, Has.Length.EqualTo(expected.Length));

            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].Kind, Is.EqualTo(expected[index].Kind), $"segment {index} kind");
                Assert.That(actual[index].Value, Is.EqualTo(expected[index].Value), $"segment {index} value");
            }
        }
    }
}
