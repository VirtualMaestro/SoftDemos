using System;
using System.Collections.Generic;
using Client.Simulation.Ports;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Client.Adapters.Services
{
    /// <summary>
    /// <see cref="IAssetService"/> on top of Addressables, in the same handle-and-poll shape as
    /// <see cref="SceneLoaderService"/>.
    ///
    /// The loaded <see cref="Object"/> never crosses the port: it stays in this adapter's table
    /// and the simulation only ever sees the opaque handle id that indexes it. That is what lets
    /// a system decide *which* asset a view shows without <c>Client.Simulation</c> knowing that
    /// <c>UnityEngine.Object</c> exists.
    /// </summary>
    public sealed class AddressablesAssetService : IAssetService, IDisposable
    {
        private readonly Dictionary<int, Request> _requests = new();
        private readonly Dictionary<int, Object> _assets = new();
        private readonly ILog _log;
        private int _nextRequestId;
        private int _nextHandleId;
        private bool _isDisposed;

        public AddressablesAssetService(ILog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>Requests started but not yet released. Must reach 0 on a clean shutdown.</summary>
        public int OpenRequestCount => _requests.Count;

        /// <summary>Assets currently held. Must reach 0 on a clean shutdown.</summary>
        public int HeldAssetCount => _assets.Count;

        public int BeginLoad(string address)
        {
            var requestId = ++_nextRequestId;
            var request = new Request(address);
            _requests.Add(requestId, request);

            if (_isDisposed)
            {
                request.Status = AsyncOpStatus.Failed;
                _log.Error($"Request #{requestId} load '{address}' rejected: the source is disposed.");
                return requestId;
            }

            _log.Info($"Request #{requestId} load '{address}' started. Open requests: {_requests.Count}.");

            try
            {
                request.Handle = Addressables.LoadAssetAsync<Object>(address);
            }
            catch (Exception exception)
            {
                // An unknown key can fail here or inside the handle. Both become a Failed status.
                request.Status = AsyncOpStatus.Failed;
                _log.Error($"Request #{requestId} load '{address}' failed to start: {exception}");
            }

            return requestId;
        }

        public AsyncOpStatus Poll(int requestId)
        {
            if (!_requests.TryGetValue(requestId, out var request))
                return AsyncOpStatus.Pending;

            if (request.Status != AsyncOpStatus.Pending)
                return request.Status;

            var status = _Classify(request, out var failureDetail);

            if (status == AsyncOpStatus.Pending)
                return AsyncOpStatus.Pending;

            request.Status = status;

            if (status == AsyncOpStatus.Failed)
            {
                _log.Error($"Request #{requestId} load '{request.Address}': Pending -> Failed. {failureDetail}");
                return status;
            }

            request.HandleId = ++_nextHandleId;
            _assets.Add(request.HandleId, request.Handle.Result);
            _log.Info($"Request #{requestId} load '{request.Address}': Pending -> Done, " +
                      $"handle #{request.HandleId}. Held assets: {_assets.Count}.");

            return status;
        }

        public int ResolveHandle(int requestId)
        {
            if (_requests.TryGetValue(requestId, out var request) == false)
                return 0;

            return request.Status == AsyncOpStatus.Done ? request.HandleId : 0;
        }

        /// <summary>
        /// Turns a handle id back into the asset. Adapter-side only — this signature is exactly
        /// what the port is not allowed to expose.
        /// </summary>
        public bool TryGetAsset(int handleId, out Object asset) => _assets.TryGetValue(handleId, out asset);

        public void Release(int requestId)
        {
            if (_requests.TryGetValue(requestId, out var request) == false)
                return;

            _requests.Remove(requestId);
            _assets.Remove(request.HandleId);
            _ReleaseHandleQuietly(request);

            _log.Info($"Request #{requestId} load '{request.Address}' released in state {request.Status}. " +
                      $"Open requests: {_requests.Count}, held assets: {_assets.Count}.");
        }

        /// <summary>
        /// Releases everything still held. Called from <c>EntryPoint.OnDestroy</c> *after* the
        /// pipeline is destroyed, so no system can still be polling a request being torn down.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _log.Info($"Disposing. Releasing {_requests.Count} asset request(s).");

            foreach (var entry in _requests)
            {
                _ReleaseHandleQuietly(entry.Value);
                _log.Info($"Request #{entry.Key} load '{entry.Value.Address}' released on dispose.");
            }

            _requests.Clear();
            _assets.Clear();
        }

        private static AsyncOpStatus _Classify(Request request, out string failureDetail)
        {
            failureDetail = string.Empty;

            if (request.Handle.IsValid() == false)
            {
                failureDetail = "The Addressables handle is not valid — the load was never started.";
                return AsyncOpStatus.Failed;
            }

            switch (request.Handle.Status)
            {
                case AsyncOperationStatus.Succeeded:
                    if (request.Handle.Result != null)
                        return AsyncOpStatus.Done;

                    failureDetail = $"Addressables reported success but returned no asset for '{request.Address}'.";
                    return AsyncOpStatus.Failed;

                case AsyncOperationStatus.Failed:
                    failureDetail = request.Handle.OperationException?.ToString() ?? "Failed with no exception.";
                    return AsyncOpStatus.Failed;

                default:
                    return AsyncOpStatus.Pending;
            }
        }

        private void _ReleaseHandleQuietly(Request request)
        {
            try
            {
                if (request.Handle.IsValid())
                    Addressables.Release(request.Handle);
            }
            catch (Exception exception)
            {
                _log.Warn($"Releasing '{request.Address}' threw and was swallowed: {exception}");
            }
        }

        private sealed class Request
        {
            public readonly string Address;

            public AsyncOperationHandle<Object> Handle;
            public AsyncOpStatus Status = AsyncOpStatus.Pending;
            public int HandleId;

            public Request(string address) => Address = address;
        }
    }
}
