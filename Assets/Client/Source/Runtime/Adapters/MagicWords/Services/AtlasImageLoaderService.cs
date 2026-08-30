using System;
using System.Collections.Generic;
using Client.Adapters.Shared.Async;
using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords.Ports;
using UnityEngine;

namespace Client.Adapters.MagicWords.Services
{
    public sealed class AtlasImageLoaderService : IImageLoadService, IDisposable
    {
        private const string PlaceholderKey = "mw-avatar-placeholder";

        private readonly RequestTable<Entry> _requests = new();
        private readonly Dictionary<int, Sprite> _resolved = new();
        private readonly ILogService _log;

        private IReadOnlyDictionary<string, Sprite> _sprites;
        private bool _isDisposed;

        public AtlasImageLoaderService(ILogService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public int OpenRequestCount => _requests.Count;
        public int HeldSpriteCount => _resolved.Count;

        public void SetSprites(IReadOnlyDictionary<string, Sprite> sprites)
        {
            _sprites = sprites ?? throw new ArgumentNullException(nameof(sprites));
            _resolved.Clear();
        }

        public void ClearSprites()
        {
            _sprites = null;
            _resolved.Clear();
        }

        public int Request(ImageLoadRequest request)
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

            if (_sprites == null)
                return AsyncOpStatus.Pending;

            var spriteKey = entry.SpriteKey;

            if (!_sprites.TryGetValue(spriteKey, out var sprite))
            {
                spriteKey = PlaceholderKey;

                if (!_sprites.TryGetValue(spriteKey, out sprite))
                {
                    entry.Status = AsyncOpStatus.Failed;
                    _log.Error(
                        $"Avatar atlas has no '{PlaceholderKey}' sprite for '{entry.SpeakerName}'.");
                    return entry.Status;
                }
            }

            entry.Status = AsyncOpStatus.Done;
            _resolved.Add(requestId, sprite);
            return entry.Status;
        }

        /// <summary>
        /// Turns a request id back into the atlas sprite it resolved. Adapter-side only — this
        /// signature is exactly what the port is not allowed to expose.
        /// </summary>
        public bool TryGetSprite(int requestId, out Sprite sprite) =>
            _resolved.TryGetValue(requestId, out sprite);

        public void Release(int requestId)
        {
            if (_requests.Remove(requestId, out _))
                _resolved.Remove(requestId);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _requests.Clear();
            ClearSprites();
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
