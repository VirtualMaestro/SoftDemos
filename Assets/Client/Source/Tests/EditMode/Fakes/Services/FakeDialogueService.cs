using System;
using System.Collections.Generic;
using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords.Ports;
using Client.Simulation.MagicWords.Payload;

namespace Client.Simulation.Tests.Fakes.Services
{
    /// <summary><see cref="IDialogueService"/> backed by <see cref="FakeAsyncRequests"/>.</summary>
    /// <remarks>
    /// Disposable because the real port is: the composition root disposes it and its
    /// <c>Dispose</c> releases every open request. No system releases on destroy
    /// (adr-data-placement-is-decided-on-three-axes rule 8), so a fake that could not be disposed
    /// would leave the shutdown claim with no actor to make it about.
    /// </remarks>
    public sealed class FakeDialogueService : IDialogueService, IDisposable
    {
        private readonly FakeAsyncRequests _requests = new();
        private readonly List<int> _loadCalls = new();

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

        public DialoguePayload Payload { get; set; }
        public IReadOnlyList<int> LoadCalls => _loadCalls;
        public int OpenRequestCount => _requests.OpenRequestCount;

        public int Request()
        {
            var id = _requests.Begin();
            _loadCalls.Add(id);
            return id;
        }

        public AsyncOpStatus Poll(int requestId) => _requests.Poll(requestId);

        public DialoguePayload Resolve(int requestId)
        {
            var isDone = _requests.TerminalStatus == AsyncOpStatus.Done && _requests.IsSettled(requestId);
            return isDone ? Payload : null;
        }

        public void Release(int requestId)
        {
            _requests.Release(requestId);
        }

        public void Dispose() => _requests.ReleaseAll();

        public override string ToString() =>
            $"FakeDialogueService({_requests}, loads={_loadCalls.Count})";
    }
}
