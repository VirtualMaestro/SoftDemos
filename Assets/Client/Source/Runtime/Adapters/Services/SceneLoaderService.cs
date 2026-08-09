using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Simulation.Ports;
using MyGameDevTools.SceneLoading;

namespace Game.Adapters.Services
{
    /// <summary>
    /// <see cref="ISceneService"/> on top of <c>com.mygamedevtools.scene-loader</c>.
    ///
    /// The library is <see cref="Task"/>-based; the simulation is not allowed to await anything.
    /// This adapter is the translation: it owns the <see cref="Task{TResult}"/> and the
    /// <see cref="CancellationTokenSource"/>, hands the caller an <see cref="int"/> request id,
    /// and turns task state into an <see cref="AsyncOpStatus"/> whenever a system polls.
    ///
    /// No exception ever leaves a port method. A faulted load is <see cref="AsyncOpStatus.Failed"/>
    /// plus one logged error — data the simulation reacts to, never a throw into the pipeline.
    /// </summary>
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
            // An unknown or already released id reads as Pending. A system that polls after
            // releasing is confused, not broken — do not punish it with an exception.
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
            else
                _log.Info($"Request #{requestId} {request.Operation} address '{request.Address}': Pending -> Done.");

            return status;
        }

        public void Release(int requestId)
        {
            if (_requests.Remove(requestId, out var request) == false)
                return;

            request.Cancellation.Dispose();

            _log.Info($"Request #{requestId} {request.Operation} address '{request.Address}' released " +
                      $"in state {request.Status}. Open requests: {_requests.Count}.");
        }

        /// <summary>
        /// Cancels and drops every in-flight request. Called from <c>EntryPoint.OnDestroy</c>
        /// *after* the pipeline is destroyed — systems stop before their ports do.
        ///
        /// This is the scenario the per-request <see cref="CancellationTokenSource"/> exists for:
        /// leaving play mode, or unloading the boot scene, while a scene load is still running.
        /// Without the token the loader keeps going and completes into a torn-down game.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _log.Info($"Disposing. Cancelling {_requests.Count} in-flight scene request(s).");

            foreach (var entry in _requests)
            {
                var request = entry.Value;
                _CancelQuietly(request);
                request.Cancellation.Dispose();
                _log.Info($"Request #{entry.Key} {request.Operation} address '{request.Address}' cancelled on dispose.");
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

            _log.Info($"Request #{requestId} {request.Operation} address '{sceneId}' started. Open requests: {_requests.Count}.");

            try
            {
                var parameters = new SceneParameters(new LoadSceneInfoAddress(sceneId), setActive: false);
                request.Task = isLoad
                    ? MySceneManager.LoadAsync(parameters, progress: null, token: request.Cancellation.Token)
                    : MySceneManager.UnloadAsync(parameters, request.Cancellation.Token);
            }
            catch (Exception exception)
            {
                // Some loader failures surface synchronously rather than as a faulted task.
                // Both roads lead to the same place: a Failed status the caller polls for.
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

        /// <summary>
        /// A completed task is not automatically a success: an addressable load can finish while
        /// returning an invalid <see cref="UnityEngine.SceneManagement.Scene"/>.
        /// Only a load is checked — after an unload the scene is legitimately invalid.
        /// </summary>
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
                // Already disposed elsewhere — nothing left to cancel.
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
