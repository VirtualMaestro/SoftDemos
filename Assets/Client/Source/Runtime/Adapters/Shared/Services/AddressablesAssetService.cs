using System;
using System.Collections.Generic;
using Client.Adapters.Shared.Async;
using Client.Simulation.Core.Ports;
using Client.Simulation.Core.Ports.Requests;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Client.Adapters.Shared.Services
{
    /// <summary>
    /// <see cref="IAssetService"/> on top of Addressables, in the same request-and-poll shape as
    /// <see cref="SceneLoaderService"/>.
    ///
    /// The loaded <see cref="Object"/> never crosses the port: it stays in this adapter's table
    /// under the request id, and <see cref="TryGetAsset"/> — a signature the port is not allowed to
    /// carry — is the only way back to it. That is what lets a system decide *which* asset a view
    /// shows without <c>Client.Simulation</c> knowing that <c>UnityEngine.Object</c> exists.
    /// </summary>
    public sealed class AddressablesAssetService : IAssetService, IDisposable
    {
        private readonly RequestTable<Entry> _requests = new();
        private readonly Dictionary<int, Object> _assets = new();
        private readonly ILogService _log;
        private bool _isDisposed;

        public AddressablesAssetService(ILogService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>Requests started but not yet released. Must reach 0 on a clean shutdown.</summary>
        public int OpenRequestCount => _requests.Count;

        /// <summary>Assets currently held. Must reach 0 on a clean shutdown.</summary>
        public int HeldAssetCount => _assets.Count;

        public int Request(in AssetLoadRequest request)
        {
            var entry = new Entry(request.Address);
            var requestId = _requests.Add(entry);

            if (_isDisposed)
            {
                entry.Status = AsyncOpStatus.Failed;
                _log.Error($"Request #{requestId} load '{entry.Address}' rejected: the source is disposed.");
                return requestId;
            }

            try
            {
                entry.Handle = Addressables.LoadAssetAsync<Object>(entry.Address);
            }
            catch (Exception exception)
            {
                // An unknown key can fail here or inside the handle. Both become a Failed status.
                entry.Status = AsyncOpStatus.Failed;
                _log.Error($"Request #{requestId} load '{entry.Address}' failed to start: {exception}");
            }

            return requestId;
        }

        public AsyncOpStatus Poll(int requestId)
        {
            if (!_requests.TryGet(requestId, out var entry))
                return AsyncOpStatus.Pending;

            if (entry.Status != AsyncOpStatus.Pending)
                return entry.Status;

            var status = _Classify(entry, out var failureDetail);

            if (status == AsyncOpStatus.Pending)
                return AsyncOpStatus.Pending;

            entry.Status = status;

            if (status == AsyncOpStatus.Failed)
            {
                _log.Error($"Request #{requestId} load '{entry.Address}': Pending -> Failed. {failureDetail}");
                return status;
            }

            _assets.Add(requestId, entry.Handle.Result);
            return status;
        }

        /// <summary>
        /// Turns a request id back into the asset it loaded. Adapter-side only — this signature is
        /// exactly what the port is not allowed to expose. False until the request is Done, and
        /// false again the moment it is released.
        /// </summary>
        public bool TryGetAsset(int requestId, out Object asset) => _assets.TryGetValue(requestId, out asset);

        public void Release(int requestId)
        {
            if (!_requests.Remove(requestId, out var entry))
                return;

            _assets.Remove(requestId);
            _ReleaseHandleQuietly(entry);
        }

        /// <summary>
        /// Releases everything still held. Called from <c>Boot.OnDestroy</c> *after* the
        /// pipeline is destroyed, so no system can still be polling a request being torn down.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            foreach (var entry in _requests.Values)
                _ReleaseHandleQuietly(entry);

            _requests.Clear();
            _assets.Clear();
        }

        private static AsyncOpStatus _Classify(Entry entry, out string failureDetail)
        {
            failureDetail = string.Empty;

            if (!entry.Handle.IsValid())
            {
                failureDetail = "The Addressables handle is not valid — the load was never started.";
                return AsyncOpStatus.Failed;
            }

            switch (entry.Handle.Status)
            {
                case AsyncOperationStatus.Succeeded:
                    if (entry.Handle.Result != null)
                        return AsyncOpStatus.Done;

                    failureDetail = $"Addressables reported success but returned no asset for '{entry.Address}'.";
                    return AsyncOpStatus.Failed;

                case AsyncOperationStatus.Failed:
                    failureDetail = entry.Handle.OperationException?.ToString() ?? "Failed with no exception.";
                    return AsyncOpStatus.Failed;

                default:
                    return AsyncOpStatus.Pending;
            }
        }

        private void _ReleaseHandleQuietly(Entry entry)
        {
            try
            {
                if (entry.Handle.IsValid())
                    Addressables.Release(entry.Handle);
            }
            catch (Exception exception)
            {
                _log.Warn($"Releasing '{entry.Address}' threw and was swallowed: {exception}");
            }
        }

        private sealed class Entry
        {
            public readonly string Address;

            public AsyncOperationHandle<Object> Handle;
            public AsyncOpStatus Status = AsyncOpStatus.Pending;

            public Entry(string address) => Address = address;
        }
    }
}
