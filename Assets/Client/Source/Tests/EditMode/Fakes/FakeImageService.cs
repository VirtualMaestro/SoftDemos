using System.Collections.Generic;
using Client.Simulation.Core.Ports;

namespace Client.Simulation.Tests.Fakes
{
    /// <summary><see cref="IImageLoadService"/> backed by <see cref="FakeAsyncRequests"/>.</summary>
    public sealed class FakeImageService : IImageLoadService
    {
        private readonly FakeAsyncRequests _requests = new();
        private readonly Dictionary<int, int> _handles = new();
        private readonly List<(string SpeakerName, string Url)> _loadCalls = new();
        private readonly List<int> _releaseCalls = new();
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

        public IReadOnlyList<(string SpeakerName, string Url)> LoadCalls => _loadCalls;
        public IReadOnlyList<int> ReleaseCalls => _releaseCalls;
        public int OpenRequestCount => _requests.OpenRequestCount;
        public bool ReturnZeroHandle { get; set; }

        public int BeginLoad(string speakerName, string url)
        {
            _loadCalls.Add((speakerName, url));
            var id = _requests.Begin();
            _handles[id] = ++_nextHandle;
            return id;
        }

        public AsyncOpStatus Poll(int requestId) => _requests.Poll(requestId);

        public int ResolveHandle(int requestId)
        {
            var isDone = _requests.TerminalStatus == AsyncOpStatus.Done && _requests.IsSettled(requestId);

            if (isDone && ReturnZeroHandle)
                return 0;

            return isDone && _handles.TryGetValue(requestId, out var handle) ? handle : 0;
        }

        public void Release(int requestId)
        {
            _releaseCalls.Add(requestId);
            _requests.Release(requestId);
            _handles.Remove(requestId);
        }

        public override string ToString() =>
            $"FakeImageService({_requests}, loads={_loadCalls.Count})";
    }
}
