using System;
using System.Collections.Generic;
using Client.Adapters.Shared.Async;
using Client.Adapters.Shared.Services;
using Client.Adapters.Shared.Stage;
using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords.Ports;
using Client.Simulation.MagicWords.Ports.Requests;
using UnityEngine;
using UnityEngine.U2D;

namespace Client.Adapters.MagicWords.Services
{
    /// <summary>
    /// Serves avatars out of the demo's sprite atlas, by request id like every other loader.
    /// </summary>
    /// <remarks>
    /// It owns no sprite. The atlas belongs to <see cref="AddressablesAssetService"/> and so does
    /// every sprite cut from it: this service asks for a cut, keeps the <c>int</c> that comes back,
    /// and releases it through the same owner
    /// (adr-an-engine-object-has-one-owner-per-kind). It used to hold the copies the stage system
    /// had already cut, which made two holders of one set of sprites and left the destroying to
    /// whichever of them remembered.
    /// </remarks>
    public sealed class AtlasImageLoaderService : IImageLoadService, IDisposable
    {
        private const string PlaceholderKey = "mw-avatar-placeholder";

        private readonly RequestTable<Entry> _requests = new();

        /// <summary>Request id -> the derived sprite's id, which the asset service owns.</summary>
        private readonly Dictionary<int, int> _resolved = new();

        /// <summary>The names the atlas carries, read once when it is handed over.</summary>
        private readonly HashSet<string> _names = new(StringComparer.Ordinal);

        private readonly ILogService _log;
        private readonly AddressablesAssetService _assets;

        private int _atlasRequestId;
        private bool _isDisposed;

        public AtlasImageLoaderService(ILogService log, AddressablesAssetService assets)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        }

        public int OpenRequestCount => _requests.Count;

        /// <summary>Derived sprite ids currently held. Each one is a row in the asset service.</summary>
        public int HeldSpriteCount => _resolved.Count;

        /// <summary>Hands over the atlas the avatars are cut from, by the id it loaded under.</summary>
        public void SetAtlas(int atlasRequestId)
        {
            _atlasRequestId = atlasRequestId;
            _resolved.Clear();
            _names.Clear();

            if (!_assets.TryGetAsset(atlasRequestId, out var asset) || asset is not SpriteAtlas atlas)
            {
                _log.Error($"Avatar atlas #{atlasRequestId} did not resolve to a SpriteAtlas.");
                _atlasRequestId = 0;
                return;
            }

            foreach (var name in StageContent.ReadAtlasNames(atlas, out _))
                _names.Add(name);
        }

        /// <summary>
        /// Forgets the atlas. The sprites cut from it are released with it by the asset service,
        /// which is the owner — this only drops the ids naming them.
        /// </summary>
        public void ClearAtlas()
        {
            _atlasRequestId = 0;
            _resolved.Clear();
            _names.Clear();
        }

        public int Request(in ImageLoadRequest request)
        {
            var speakerName = request.SpeakerName ?? string.Empty;
            var entry = new Entry(speakerName, $"avatar-{speakerName.ToLowerInvariant()}");

            if (_isDisposed)
                entry.Status = AsyncOpStatus.Failed;

            return _requests.Add(entry);
        }

        public AsyncOpStatus Poll(int requestId)
        {
            if (!_requests.TryGet(requestId, out var entry))
                return AsyncOpStatus.Pending;

            if (entry.Status != AsyncOpStatus.Pending)
                return entry.Status;

            if (_atlasRequestId == 0)
                return AsyncOpStatus.Pending;

            var spriteKey = entry.SpriteKey;

            if (!_names.Contains(spriteKey))
            {
                spriteKey = PlaceholderKey;

                if (!_names.Contains(spriteKey))
                {
                    entry.Status = AsyncOpStatus.Failed;
                    _log.Error(
                        $"Avatar atlas has no '{PlaceholderKey}' sprite for '{entry.SpeakerName}'.");
                    return entry.Status;
                }
            }

            var derivedId = StageContent.DeriveFromAtlas(_assets, _atlasRequestId, spriteKey);

            if (derivedId == 0)
            {
                entry.Status = AsyncOpStatus.Failed;
                return entry.Status;
            }

            entry.Status = AsyncOpStatus.Done;
            _resolved.Add(requestId, derivedId);
            return entry.Status;
        }

        /// <summary>
        /// Turns a request id back into the sprite the asset service cut for it. Adapter-side only
        /// — this signature is exactly what the port is not allowed to expose.
        /// </summary>
        public bool TryGetSprite(int requestId, out Sprite sprite)
        {
            sprite = null;

            if (!_resolved.TryGetValue(requestId, out var derivedId) ||
                !_assets.TryGetAsset(derivedId, out var asset))
                return false;

            sprite = asset as Sprite;
            return sprite != null;
        }

        public void Release(int requestId)
        {
            if (!_requests.Remove(requestId, out _))
                return;

            if (_resolved.Remove(requestId, out var derivedId))
                _assets.Release(derivedId);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            // The asset service is disposed by the composition root too, and a released id reads as
            // unknown either way. Releasing here keeps the leak counters honest when a demo closes.
            foreach (var derivedId in _resolved.Values)
                _assets.Release(derivedId);

            _requests.Clear();
            ClearAtlas();
        }

        private sealed class Entry
        {
            public readonly string SpeakerName;
            public readonly string SpriteKey;

            public AsyncOpStatus Status;

            public Entry(string speakerName, string spriteKey)
            {
                SpeakerName = speakerName;
                SpriteKey = spriteKey;
            }
        }
    }
}
