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
            Assert.That(DialogueTokenizer.Tokenize(text, KnownTokens, out _), Is.Empty);
        }

        [Test]
        public void TextWithoutBraces_RemainsOneLiteralSegment()
        {
            _AssertSegments("plain text", KnownTokens, _Text("plain text"));
        }

        [Test]
        public void KnownToken_PreservesSurroundingSpaces()
        {
            _AssertSegments("a {win} b", KnownTokens, _Text("a "), _Emoji("win"), _Text(" b"));
        }

        [Test]
        public void KnownTokenWithoutText_ReturnsOnlyEmoji()
        {
            _AssertSegments("{win}", KnownTokens, _Emoji("win"));
        }

        [Test]
        public void AdjacentKnownTokens_ReturnOnlyEmojis()
        {
            _AssertSegments("{win}{laughing}", KnownTokens, _Emoji("win"), _Emoji("laughing"));
        }

        [Test]
        public void UnknownToken_RemainsLiteral()
        {
            _AssertSegments("{wat}", KnownTokens, _Text("{wat}"));
        }

        [Test]
        public void UnknownToken_MergesIntoLiteralRun()
        {
            _AssertSegments("a {wat} b", KnownTokens, _Text("a {wat} b"));
        }

        [Test]
        public void UnclosedToken_MergesIntoLiteralRun()
        {
            _AssertSegments("a {win", KnownTokens, _Text("a {win"));
        }

        [TestCase("{}")]
        [TestCase("{ }")]
        public void EmptyOrWhitespaceToken_RemainsLiteral(string text)
        {
            _AssertSegments(text, KnownTokens, _Text(text));
        }

        [Test]
        public void UnicodeApostrophe_SurvivesUnchanged()
        {
            _AssertSegments("That’s {neutral}", KnownTokens, _Text("That’s "), _Emoji("neutral"));
        }

        [Test]
        public void MissingCatalog_LeavesTokensLiteral()
        {
            _AssertSegments("{win}", null, _Text("{win}"));
            _AssertSegments("{win}", new HashSet<string>(), _Text("{win}"));
        }

        private static DialogueSegment _Text(string value) =>
            new() { Kind = SegmentKind.Text, Value = value };

        private static DialogueSegment _Emoji(string value) =>
            new() { Kind = SegmentKind.Emoji, Value = value };

        private static void _AssertSegments(
            string text,
            HashSet<string> knownTokens,
            params DialogueSegment[] expected)
        {
            var actual = DialogueTokenizer.Tokenize(text, knownTokens, out _);
            Assert.That(actual, Has.Length.EqualTo(expected.Length));

            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].Kind, Is.EqualTo(expected[index].Kind), $"segment {index} kind");
                Assert.That(actual[index].Value, Is.EqualTo(expected[index].Value), $"segment {index} value");
            }
        }
    }
}
