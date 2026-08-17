using System;
using System.Collections.Generic;
using Client.Simulation.Ports;
using UnityEngine;

namespace Client.Adapters.MagicWords
{
    public sealed class AvatarImageRouterService : IImageLoadService, IDisposable
    {
        private readonly AtlasImageLoaderService _local;
        private readonly WebImageLoaderService _remote;
        private readonly ILog _log;
        private readonly Dictionary<int, Route> _routes = new();

        private int _nextRequestId;
        private bool _isDisposed;

        public AvatarImageRouterService(AtlasImageLoaderService local, WebImageLoaderService remote, ILog log)
        {
            _local = local ?? throw new ArgumentNullException(nameof(local));
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public AvatarMode Mode { get; private set; } = AvatarMode.Local;
        public int OpenRequestCount => _routes.Count;
        public AtlasImageLoaderService Local => _local;
        public WebImageLoaderService Remote => _remote;

        /// <summary>Hands the local loader the sprite table it resolves speaker names against.</summary>
        /// <remarks>
        /// The atlas loader is reached through the router, never injected on its own. It also
        /// implements <see cref="IImageLoadService"/>, so injecting it would attach it to that
        /// port's injection node and displace this router there — the simulation would then talk
        /// to the atlas directly and the router would never see the request.
        /// </remarks>
        public void SetLocalSprites(IReadOnlyDictionary<string, Sprite> sprites)
        {
            _local.SetSprites(sprites);
        }

        public void ClearLocalSprites()
        {
            _local.ClearSprites();
        }

        public void SetMode(AvatarMode mode)
        {
            if (Mode == mode)
                return;

            Mode = mode;
            _log.Info($"Avatar mode changed to {mode}.");
        }

        public int BeginLoad(string speakerName, string url)
        {
            var requestId = ++_nextRequestId;
            var innerRequestId = Mode == AvatarMode.Local
                ? _local.BeginLoad(speakerName, url)
                : _remote.BeginLoad(speakerName, url);
            _routes.Add(requestId, new Route(Mode, innerRequestId));
            return requestId;
        }

        public AsyncOpStatus Poll(int requestId)
        {
            return _routes.TryGetValue(requestId, out var route)
                ? _Poll(route)
                : AsyncOpStatus.Pending;
        }

        public int ResolveHandle(int requestId)
        {
            if (_routes.TryGetValue(requestId, out var route) == false)
                return 0;

            return _ResolveInnerHandle(route) != 0 ? requestId : 0;
        }

        public bool TryGetSprite(int handleId, out Sprite sprite)
        {
            if (_routes.TryGetValue(handleId, out var route) == false)
            {
                sprite = null;
                return false;
            }

            var innerHandle = _ResolveInnerHandle(route);

            if (innerHandle == 0)
            {
                sprite = null;
                return false;
            }

            return route.Owner == AvatarMode.Local
                ? _local.TryGetSprite(innerHandle, out sprite)
                : _remote.TryGetSprite(innerHandle, out sprite);
        }

        public void Release(int requestId)
        {
            if (_routes.Remove(requestId, out var route) == false)
                return;

            if (route.Owner == AvatarMode.Local)
                _local.Release(route.InnerRequestId);
            else
                _remote.Release(route.InnerRequestId);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _routes.Clear();
            _local.Dispose();
            _remote.Dispose();
        }

        private AsyncOpStatus _Poll(Route route)
        {
            return route.Owner == AvatarMode.Local
                ? _local.Poll(route.InnerRequestId)
                : _remote.Poll(route.InnerRequestId);
        }

        private int _ResolveInnerHandle(Route route)
        {
            return route.Owner == AvatarMode.Local
                ? _local.ResolveHandle(route.InnerRequestId)
                : _remote.ResolveHandle(route.InnerRequestId);
        }

        private readonly struct Route
        {
            public readonly AvatarMode Owner;
            public readonly int InnerRequestId;

            public Route(AvatarMode owner, int innerRequestId)
            {
                Owner = owner;
                InnerRequestId = innerRequestId;
            }
        }
    }
}
