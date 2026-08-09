using System.Collections.Generic;
using Game.Simulation.Ports;

namespace Game.Simulation.Tests.Fakes
{
    /// <summary>
    /// <see cref="IAssetService"/> backed by <see cref="FakeAsyncRequests"/>. Hands out an opaque
    /// handle id once a request settles as <see cref="AsyncOpStatus.Done"/> — the same contract
    /// the Addressables adapter honours, so a system written against the fake needs no change.
    /// </summary>
    public sealed class FakeAssetService : IAssetService
    {
        private readonly FakeAsyncRequests _requests = new();
        private readonly Dictionary<int, int> _handles = new();
        private readonly List<string> _loadCalls = new();
        private int _nextHandle;

        public int CompleteAfterPolls
        {
            get => _requests.CompleteAfterPolls;
            set => _requests.CompleteAfterPolls = value;
        }

        public AsyncOpStatus TerminalStatus
        {
            get => _requests.TerminalStatus;
            set => _requests.TerminalStatus = value;
        }

        public IReadOnlyList<string> LoadCalls => _loadCalls;
        public int OpenRequestCount => _requests.OpenRequestCount;

        public int BeginLoad(string address)
        {
            _loadCalls.Add(address);
            var id = _requests.Begin();
            _handles[id] = ++_nextHandle;
            return id;
        }

        public AsyncOpStatus Poll(int requestId) => _requests.Poll(requestId);

        public int ResolveHandle(int requestId)
        {
            var isDone = _requests.TerminalStatus == AsyncOpStatus.Done && _requests.IsSettled(requestId);
            return isDone && _handles.TryGetValue(requestId, out var handle) ? handle : 0;
        }

        public void Release(int requestId)
        {
            _requests.Release(requestId);
            _handles.Remove(requestId);
        }

        public override string ToString() =>
            $"FakeAssetService({_requests}, loads=[{string.Join(", ", _loadCalls)}])";
    }
}
