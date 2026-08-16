using System.Linq;
using Client.Simulation.Ports;

namespace Client.Simulation.Tests.Fakes
{
    /// <summary>
    /// <see cref="IRandomService"/> that replays a scripted sequence and then degenerates to
    /// <c>minInclusive</c>. Falling back to a fixed value rather than wrapping keeps an
    /// over-consuming system deterministic instead of quietly cycling.
    /// </summary>
    public sealed class FakeRandom : IRandomService
    {
        private readonly int[] _sequence;
        private int _cursor;

        public FakeRandom(params int[] sequence) => _sequence = sequence ?? new int[0];

        /// <summary>How many times <see cref="Range"/> has been called, scripted or not.</summary>
        public int CallCount { get; private set; }

        /// <summary>True once the scripted sequence is exhausted and the fallback is in use.</summary>
        public bool IsExhausted => _cursor >= _sequence.Length;

        public int Range(int minInclusive, int maxExclusive)
        {
            CallCount++;

            if (IsExhausted)
                return minInclusive;

            return _sequence[_cursor++];
        }

        public override string ToString() =>
            $"FakeRandom(sequence=[{string.Join(", ", _sequence.Select(v => v.ToString()))}], " +
            $"cursor={_cursor}, calls={CallCount})";
    }
}
