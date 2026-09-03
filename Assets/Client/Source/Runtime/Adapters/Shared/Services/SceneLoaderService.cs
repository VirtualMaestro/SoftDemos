using System;
using System.Threading;
using System.Threading.Tasks;
using Client.Adapters.Shared.Async;
using Client.Simulation.Core.Ports;
using Client.Simulation.Core.Ports.Requests;
using MyGameDevTools.SceneLoading;

namespace Client.Adapters.Shared.Services
{
    /// <summary><see cref="ISceneService"/> on top of <c>com.mygamedevtools.scene-loader</c>.</summary>
    /// <remarks>
    /// The library uses <see cref="Task"/>, but the simulation must not await. This adapter owns
    /// the task and the <see cref="CancellationTokenSource"/>, gives the caller a request id, and
    /// reports the task state as an <see cref="AsyncOpStatus"/>. No port method throws. A failed
    /// load becomes <see cref="AsyncOpStatus.Failed"/> and one logged error.
    /// <para>Loading and unloading share one id space and one table: they are two kinds of work,
    /// not two services.</para>
    /// </remarks>
    public sealed class SceneLoaderService : ISceneService, IDisposable
    {
        private readonly RequestTable<Entry> _requests = new();
        private readonly ILogService _log;
        private bool _isDisposed;

        public SceneLoaderService(ILogService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public int Request(in SceneLoadRequest request) => _Begin(request.SceneId, isLoad: true);

        public int Request(in SceneUnloadRequest request) => _Begin(request.SceneId, isLoad: false);

        public AsyncOpStatus Poll(int requestId)
        {
            // An unknown or released id reads as Pending. Do not throw at a late poll.
            if (!_requests.TryGet(requestId, out var entry))
                return AsyncOpStatus.Pending;

            if (entry.Status != AsyncOpStatus.Pending)
                return entry.Status;

            // Get status of async operation for loading or unloading a scene
            var status = _GetAsyncOpStatus(entry, out var failureDetail);

            if (status == AsyncOpStatus.Pending)
                return AsyncOpStatus.Pending;

            entry.Status = status;

            if (status == AsyncOpStatus.Failed)
                _log.Error($"Request #{requestId} {entry.Operation} address '{entry.Address}': Pending -> Failed. {failureDetail}");

            return status;
        }

        public void Release(int requestId)
        {
            if (!_requests.Remove(requestId, out var entry))
                return;

            entry.Cancellation.Dispose();
        }

        /// <summary>Cancels and drops every open request. Call it after the pipeline is destroyed.</summary>
        /// <remarks>
        /// This is why each request has a <see cref="CancellationTokenSource"/>. Without the token,
        /// a load that is still running completes into a game that no longer exists.
        /// </remarks>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            foreach (var entry in _requests.Values)
            {
                _CancelQuietly(entry);
                entry.Cancellation.Dispose();
            }

            _requests.Clear();
        }

        private int _Begin(string sceneId, bool isLoad)
        {
            var entry = new Entry(sceneId, isLoad);
            var requestId = _requests.Add(entry);

            if (_isDisposed)
            {
                entry.Status = AsyncOpStatus.Failed;
                _log.Error($"Request #{requestId} {entry.Operation} address '{sceneId}' rejected: the service is disposed.");
                return requestId;
            }

            try
            {
                var parameters = new SceneParameters(new LoadSceneInfoAddress(sceneId), setActive: false);
                entry.Task = isLoad
                    ? MySceneManager.LoadAsync(parameters, progress: null, token: entry.Cancellation.Token)
                    : MySceneManager.UnloadAsync(parameters, entry.Cancellation.Token);
            }
            catch (Exception exception)
            {
                // Some loader failures throw here instead of faulting the task. Same result.
                entry.Status = AsyncOpStatus.Failed;
                _log.Error($"Request #{requestId} {entry.Operation} address '{sceneId}' failed to start: {exception}");
            }

            return requestId;
        }

        private static AsyncOpStatus _GetAsyncOpStatus(Entry entry, out string failureDetail)
        {
            failureDetail = string.Empty;

            if (entry.Task == null)
            {
                failureDetail = "The operation was never started.";
                return AsyncOpStatus.Failed;
            }

            switch (entry.Task.Status)
            {
                case TaskStatus.RanToCompletion:
                    return _ClassifyResult(entry, out failureDetail);

                case TaskStatus.Faulted:
                    failureDetail = entry.Task.Exception?.ToString() ?? "Faulted with no exception.";
                    return AsyncOpStatus.Failed;

                case TaskStatus.Canceled:
                    failureDetail = "The operation was cancelled.";
                    return AsyncOpStatus.Failed;

                default:
                    return AsyncOpStatus.Pending;
            }
        }

        /// <summary>A completed task is not always a success. A load can return an invalid scene.</summary>
        /// <remarks>Only a load is checked. After an unload the scene is invalid by design.</remarks>
        private static AsyncOpStatus _ClassifyResult(Entry entry, out string failureDetail)
        {
            failureDetail = string.Empty;

            if (!entry.IsLoad)
                return AsyncOpStatus.Done;

            try
            {
                if (entry.Task.Result.GetScene().IsValid())
                    return AsyncOpStatus.Done;

                failureDetail = $"The loader completed but returned no valid scene for address '{entry.Address}'.";
                return AsyncOpStatus.Failed;
            }
            catch (Exception exception)
            {
                failureDetail = $"Reading the scene result threw: {exception}";
                return AsyncOpStatus.Failed;
            }
        }

        private static void _CancelQuietly(Entry entry)
        {
            try
            {
                if (!entry.Cancellation.IsCancellationRequested)
                    entry.Cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed. Nothing to cancel.
            }
        }

        // Properties are used only for tests
        /// <summary>Requests started but not yet released. Must reach 0 on a clean shutdown.</summary>
        internal int OpenRequestCount => _requests.Count;

        private sealed class Entry
        {
            public readonly string Address;
            public readonly bool IsLoad;
            public readonly CancellationTokenSource Cancellation = new();

            public Task<SceneResult> Task;
            public AsyncOpStatus Status = AsyncOpStatus.Pending;

            public Entry(string sceneId, bool isLoad)
            {
                Address = sceneId;
                IsLoad = isLoad;
            }

            public string Operation => IsLoad ? "load" : "unload";
        }
    }
}
