using System.Collections.Generic;
using Client.Simulation.Ports;

namespace Client.Simulation.Tests.Fakes
{
    /// <summary>
    /// <see cref="ISceneService"/> backed by <see cref="FakeAsyncRequests"/>. Records what was
    /// asked for so a test can assert the scene ids a system requested, not just the outcome.
    /// </summary>
    public sealed class FakeSceneService : ISceneService
    {
        private readonly FakeAsyncRequests _requests = new();
        private readonly List<string> _loadCalls = new();
        private readonly List<string> _unloadCalls = new();

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
        public IReadOnlyList<string> UnloadCalls => _unloadCalls;
        public int OpenRequestCount => _requests.OpenRequestCount;

        public int BeginLoad(string sceneId)
        {
            _loadCalls.Add(sceneId);
            return _requests.Begin();
        }

        public int BeginUnload(string sceneId)
        {
            _unloadCalls.Add(sceneId);
            return _requests.Begin();
        }

        public AsyncOpStatus Poll(int requestId) => _requests.Poll(requestId);

        public void Release(int requestId) => _requests.Release(requestId);

        public override string ToString() =>
            $"FakeSceneService({_requests}, loads=[{string.Join(", ", _loadCalls)}], " +
            $"unloads=[{string.Join(", ", _unloadCalls)}])";
    }
}
