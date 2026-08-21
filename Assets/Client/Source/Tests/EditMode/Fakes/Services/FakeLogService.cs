using System.Collections.Generic;
using System.Linq;
using Client.Simulation.Shared.Ports;

namespace Client.Simulation.Tests.Fakes.Services
{
    /// <summary>
    /// <see cref="ILogService"/> that records every message with its level, so a test can assert that a
    /// failure path actually reported itself instead of failing silently.
    /// </summary>
    public sealed class FakeLogService : ILogService
    {
        public enum Level { Info, Warn, Error }

        public readonly struct Entry
        {
            public readonly Level Level;
            public readonly string Message;

            public Entry(Level level, string message)
            {
                Level = level;
                Message = message;
            }

            public override string ToString() => $"{Level}: {Message}";
        }

        private readonly List<Entry> _entries = new();

        public IReadOnlyList<Entry> Entries => _entries;

        public IEnumerable<Entry> OfLevel(Level level) => _entries.Where(e => e.Level == level);

        public int CountOf(Level level) => _entries.Count(e => e.Level == level);

        public void Clear() => _entries.Clear();

        public void Info(string message) => _entries.Add(new Entry(Level.Info, message));
        public void Warn(string message) => _entries.Add(new Entry(Level.Warn, message));
        public void Error(string message) => _entries.Add(new Entry(Level.Error, message));

        public override string ToString() =>
            _entries.Count == 0
                ? "FakeLogService(empty)"
                : "FakeLogService:\n  " + string.Join("\n  ", _entries.Select(e => e.ToString()));
    }
}
