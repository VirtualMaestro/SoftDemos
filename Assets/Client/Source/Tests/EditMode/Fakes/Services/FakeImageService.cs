using System.Collections.Generic;
using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords.Ports;
using Client.Simulation.MagicWords.Ports.Requests;

namespace Client.Simulation.Tests.Fakes.Services
{
    /// <summary><see cref="IImageLoadService"/> backed by <see cref="FakeAsyncRequests"/>.</summary>
    public sealed class FakeImageService : IImageLoadService
    {
        private readonly FakeAsyncRequests _requests = new();
        private readonly List<(string SpeakerName, string Url)> _loadCalls = new();
        private readonly List<int> _releaseCalls = new();

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

        public int Request(in ImageLoadRequest request)
        {
            _loadCalls.Add((request.SpeakerName, request.Url));
            return _requests.Begin();
        }

        public AsyncOpStatus Poll(int requestId) => _requests.Poll(requestId);

        public void Release(int requestId)
        {
            _releaseCalls.Add(requestId);
            _requests.Release(requestId);
        }

        public override string ToString() =>
            $"FakeImageService({_requests}, loads={_loadCalls.Count})";
    }
}
