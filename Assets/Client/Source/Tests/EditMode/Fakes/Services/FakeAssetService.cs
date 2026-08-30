using System.Collections.Generic;
using Client.Simulation.Core.Ports;

namespace Client.Simulation.Tests.Fakes.Services
{
    /// <summary>
    /// <see cref="IAssetService"/> backed by <see cref="FakeAsyncRequests"/>. There is nothing to
    /// resolve: the port carries no result, and a real adapter would keep the asset under the
    /// request id on its own side of the boundary.
    /// </summary>
    public sealed class FakeAssetService : IAssetService
    {
        private readonly FakeAsyncRequests _requests = new();
        private readonly List<string> _loadCalls = new();

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

        public int Request(in AssetLoadRequest request)
        {
            _loadCalls.Add(request.Address);
            return _requests.Begin();
        }

        public AsyncOpStatus Poll(int requestId) => _requests.Poll(requestId);

        public void Release(int requestId) => _requests.Release(requestId);

        public override string ToString() =>
            $"FakeAssetService({_requests}, loads=[{string.Join(", ", _loadCalls)}])";
    }
}
