using System;
using System.Collections.Generic;
using Client.Simulation.Core.Ports;
using UnityEngine;

namespace Client.Adapters.MagicWords.Services
{
    public sealed class AtlasImageLoaderService : IImageLoadService, IDisposable
    {
        private const string PlaceholderKey = "mw-avatar-placeholder";

        private readonly Dictionary<int, Request> _requests = new();
        private readonly Dictionary<int, Sprite> _handles = new();
        private readonly ILog _log;

        private IReadOnlyDictionary<string, Sprite> _sprites;
        private int _nextRequestId;
        private int _nextHandleId;
        private bool _isDisposed;

        public AtlasImageLoaderService(ILog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public int OpenRequestCount => _requests.Count;
        public int HeldSpriteCount => _handles.Count;

        public void SetSprites(IReadOnlyDictionary<string, Sprite> sprites)
        {
            _sprites = sprites ?? throw new ArgumentNullException(nameof(sprites));
            _handles.Clear();
        }

        public void ClearSprites()
        {
            _sprites = null;
            _handles.Clear();
        }

        public int BeginLoad(string speakerName, string url)
        {
            var requestId = ++_nextRequestId;
            var request = new Request(
                speakerName ?? string.Empty,
                $"avatar-{(speakerName ?? string.Empty).ToLowerInvariant()}");

            if (_isDisposed)
                request.Status = AsyncOpStatus.Failed;

            _requests.Add(requestId, request);
            return requestId;
        }

        public AsyncOpStatus Poll(int requestId)
        {
            if (_requests.TryGetValue(requestId, out var request) == false)
                return AsyncOpStatus.Pending;

            if (request.Status != AsyncOpStatus.Pending)
                return request.Status;

            if (_sprites == null)
                return AsyncOpStatus.Pending;

            var spriteKey = request.SpriteKey;

            if (_sprites.TryGetValue(spriteKey, out var sprite) == false)
            {
                spriteKey = PlaceholderKey;

                if (_sprites.TryGetValue(spriteKey, out sprite) == false)
                {
                    request.Status = AsyncOpStatus.Failed;
                    _log.Error(
                        $"Avatar atlas has no '{PlaceholderKey}' sprite for '{request.SpeakerName}'.");
                    return request.Status;
                }
            }

            request.HandleId = ++_nextHandleId;
            request.Status = AsyncOpStatus.Done;
            _handles.Add(request.HandleId, sprite);
            return request.Status;
        }

        public int ResolveHandle(int requestId)
        {
            return _requests.TryGetValue(requestId, out var request) &&
                request.Status == AsyncOpStatus.Done
                    ? request.HandleId
                    : 0;
        }

        public bool TryGetSprite(int handleId, out Sprite sprite) =>
            _handles.TryGetValue(handleId, out sprite);

        public void Release(int requestId)
        {
            if (_requests.Remove(requestId, out var request) && request.HandleId != 0)
                _handles.Remove(request.HandleId);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _requests.Clear();
            ClearSprites();
        }

        private sealed class Request
        {
            public readonly string SpeakerName;
            public readonly string SpriteKey;

            public AsyncOpStatus Status;
            public int HandleId;

            public Request(string speakerName, string spriteKey)
            {
                SpeakerName = speakerName;
                SpriteKey = spriteKey;
            }
        }
    }
}
