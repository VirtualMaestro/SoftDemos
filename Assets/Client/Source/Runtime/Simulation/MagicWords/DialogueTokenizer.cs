using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Simulation.MagicWords
{
    public static class DialogueTokenizer
    {
        public static DialogueSegment[] Tokenize(
            string text, HashSet<string> knownTokens, out bool hasUnknownToken)
        {
            hasUnknownToken = false;

            if (string.IsNullOrEmpty(text))
                return Array.Empty<DialogueSegment>();

            var segments = new List<DialogueSegment>();
            var literal = new StringBuilder();
            var index = 0;

            while (index < text.Length)
            {
                if (text[index] != '{')
                {
                    literal.Append(text[index]);
                    index++;
                    continue;
                }

                var closeIndex = index + 1;
                while (closeIndex < text.Length && text[closeIndex] != '}')
                    closeIndex++;

                if (closeIndex == text.Length)
                {
                    literal.Append(text, index, text.Length - index);
                    break;
                }

                var tokenLength = closeIndex - index - 1;
                var token = tokenLength > 0 ? text.Substring(index + 1, tokenLength) : string.Empty;

                if (knownTokens != null && knownTokens.Contains(token))
                {
                    _AddLiteral(segments, literal);
                    segments.Add(new DialogueSegment { Kind = SegmentKind.Emoji, Value = token });
                }
                else
                {
                    hasUnknownToken = true;
                    literal.Append(text, index, closeIndex - index + 1);
                }

                index = closeIndex + 1;
            }

            _AddLiteral(segments, literal);
            return segments.ToArray();
        }

        private static void _AddLiteral(List<DialogueSegment> segments, StringBuilder literal)
        {
            if (literal.Length == 0)
                return;

            segments.Add(new DialogueSegment { Kind = SegmentKind.Text, Value = literal.ToString() });
            literal.Clear();
        }
    }
}
