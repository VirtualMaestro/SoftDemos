using System.Collections.Generic;
using Game.Simulation.Ports;

namespace Game.Simulation.Tests.Fakes
{
    /// <summary>
    /// The handle-and-poll bookkeeping shared by <see cref="FakeSceneService"/> and
    /// <see cref="FakeAssetService"/>: incrementing request ids, a per-request poll countdown, and
    /// a configurable terminal status.
    ///
    /// Deterministic on purpose — no threads, no <c>Task</c>, no wall clock. A test that wants a
    /// request to take three ticks sets <see cref="CompleteAfterPolls"/> to 3 and gets exactly
    /// that, every run.
    /// </summary>
    public sealed class FakeAsyncRequests
    {
        private readonly Dictionary<int, int> _pollsLeft = new();
        private int _nextId;

        /// <summary>Polls a request needs before it reports <see cref="TerminalStatus"/>.</summary>
        public int CompleteAfterPolls { get; set; } = 1;

        /// <summary>Status every request settles on. Set to <c>Failed</c> to script a failure.</summary>
        public AsyncOpStatus TerminalStatus { get; set; } = AsyncOpStatus.Done;

        /// <summary>Requests started but not yet released. Must be 0 after a clean shutdown.</summary>
        public int OpenRequestCount => _pollsLeft.Count;

        public int Begin()
        {
            var id = ++_nextId;
            _pollsLeft[id] = CompleteAfterPolls;
            return id;
        }

        public AsyncOpStatus Poll(int requestId)
        {
            // An unknown or released id is Pending, never a throw — the port contract forbids
            // exceptions crossing the boundary, and the fake must not be more forgiving than
            // the real adapter.
            if (_pollsLeft.TryGetValue(requestId, out var left) == false)
                return AsyncOpStatus.Pending;

            if (left > 0)
            {
                left--;
                _pollsLeft[requestId] = left;
            }

            return left == 0 ? TerminalStatus : AsyncOpStatus.Pending;
        }

        public bool IsSettled(int requestId) =>
            _pollsLeft.TryGetValue(requestId, out var left) && left == 0;

        public bool Release(int requestId) => _pollsLeft.Remove(requestId);

        public override string ToString() =>
            $"completeAfterPolls={CompleteAfterPolls}, terminal={TerminalStatus}, " +
            $"openRequests={OpenRequestCount}";
    }
}
