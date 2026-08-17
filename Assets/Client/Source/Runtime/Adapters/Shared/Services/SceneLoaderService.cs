using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Client.Simulation.Ports;
using MyGameDevTools.SceneLoading;

namespace Client.Adapters.Services
{
    /// <summary><see cref="ISceneService"/> on top of <c>com.mygamedevtools.scene-loader</c>.</summary>
    /// <remarks>
    /// The library uses <see cref="Task"/>, but the simulation must not await. This adapter owns
    /// the task and the <see cref="CancellationTokenSource"/>, gives the caller a request id, and
    /// reports the task state as an <see cref="AsyncOpStatus"/>. No port method throws. A failed
    /// load becomes <see cref="AsyncOpStatus.Failed"/> and one logged error.
    /// </remarks>
    public sealed class SceneLoaderService : ISceneService, IDisposable
    {
        private readonly Dictionary<int, Request> _requests = new();
        private readonly ILog _log;
        private int _nextId;
        private bool _isDisposed;

        public SceneLoaderService(ILog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>Requests started but not yet released. Must reach 0 on a clean shutdown.</summary>
        public int OpenRequestCount => _requests.Count;

        public int BeginLoad(string sceneId) => _Begin(sceneId, isLoad: true);

        public int BeginUnload(string sceneId) => _Begin(sceneId, isLoad: false);

        public AsyncOpStatus Poll(int requestId)
        {
            // An unknown or released id reads as Pending. Do not throw at a late poll.
            if (_requests.TryGetValue(requestId, out var request) == false)
                return AsyncOpStatus.Pending;

            if (request.Status != AsyncOpStatus.Pending)
                return request.Status;

            var status = _Classify(request, out var failureDetail);

            if (status == AsyncOpStatus.Pending)
                return AsyncOpStatus.Pending;

            request.Status = status;

            if (status == AsyncOpStatus.Failed)
                _log.Error($"Request #{requestId} {request.Operation} address '{request.Address}': Pending -> Failed. {failureDetail}");

            return status;
        }

        public void Release(int requestId)
        {
            if (_requests.Remove(requestId, out var request) == false)
                return;

            request.Cancellation.Dispose();
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

            foreach (var entry in _requests)
            {
                var request = entry.Value;
                _CancelQuietly(request);
                request.Cancellation.Dispose();
            }

            _requests.Clear();
        }

        private int _Begin(string sceneId, bool isLoad)
        {
            var requestId = ++_nextId;
            var request = new Request(sceneId, isLoad);
            _requests.Add(requestId, request);

            if (_isDisposed)
            {
                request.Status = AsyncOpStatus.Failed;
                _log.Error($"Request #{requestId} {request.Operation} address '{sceneId}' rejected: the service is disposed.");
                return requestId;
            }

            try
            {
                var parameters = new SceneParameters(new LoadSceneInfoAddress(sceneId), setActive: false);
                request.Task = isLoad
                    ? MySceneManager.LoadAsync(parameters, progress: null, token: request.Cancellation.Token)
                    : MySceneManager.UnloadAsync(parameters, request.Cancellation.Token);
            }
            catch (Exception exception)
            {
                // Some loader failures throw here instead of faulting the task. Same result.
                request.Status = AsyncOpStatus.Failed;
                _log.Error($"Request #{requestId} {request.Operation} address '{sceneId}' failed to start: {exception}");
            }

            return requestId;
        }

        private static AsyncOpStatus _Classify(Request request, out string failureDetail)
        {
            failureDetail = string.Empty;

            if (request.Task == null)
            {
                failureDetail = "The operation was never started.";
                return AsyncOpStatus.Failed;
            }

            switch (request.Task.Status)
            {
                case TaskStatus.RanToCompletion:
                    return _ClassifyResult(request, out failureDetail);

                case TaskStatus.Faulted:
                    failureDetail = request.Task.Exception?.ToString() ?? "Faulted with no exception.";
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
        private static AsyncOpStatus _ClassifyResult(Request request, out string failureDetail)
        {
            failureDetail = string.Empty;

            if (request.IsLoad == false)
                return AsyncOpStatus.Done;

            try
            {
                if (request.Task.Result.GetScene().IsValid())
                    return AsyncOpStatus.Done;

                failureDetail = $"The loader completed but returned no valid scene for address '{request.Address}'.";
                return AsyncOpStatus.Failed;
            }
            catch (Exception exception)
            {
                failureDetail = $"Reading the scene result threw: {exception}";
                return AsyncOpStatus.Failed;
            }
        }

        private static void _CancelQuietly(Request request)
        {
            try
            {
                if (request.Cancellation.IsCancellationRequested == false)
                    request.Cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed. Nothing to cancel.
            }
        }

        private sealed class Request
        {
            public readonly string Address;
            public readonly bool IsLoad;
            public readonly CancellationTokenSource Cancellation = new();

            public Task<SceneResult> Task;
            public AsyncOpStatus Status = AsyncOpStatus.Pending;

            public Request(string sceneId, bool isLoad)
            {
                Address = sceneId;
                IsLoad = isLoad;
            }

            public string Operation => IsLoad ? "load" : "unload";
        }
    }
}
