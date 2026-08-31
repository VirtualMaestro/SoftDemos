using System;
using System.Collections.Generic;
using Client.Adapters.Shared.Async;
using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords.Ports;
using Client.Simulation.MagicWords.Ports.Requests;
using UnityEngine;

namespace Client.Adapters.MagicWords.Services
{
    public sealed class AvatarImageRouterService : IImageLoadService, IDisposable
    {
        private readonly AtlasImageLoaderService _local;
        private readonly WebImageLoaderService _remote;
        private readonly RequestTable<Route> _routes = new();

        private bool _isDisposed;

        public AvatarImageRouterService(AtlasImageLoaderService local, WebImageLoaderService remote)
        {
            _local = local ?? throw new ArgumentNullException(nameof(local));
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
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

        public void SetMode(AvatarMode mode) => Mode = mode;

        public int Request(in ImageLoadRequest request)
        {
            var innerRequestId = Mode == AvatarMode.Local
                ? _local.Request(request)
                : _remote.Request(request);

            return _routes.Add(new Route(Mode, innerRequestId));
        }

        public AsyncOpStatus Poll(int requestId)
        {
            return _routes.TryGet(requestId, out var route)
                ? _Poll(route)
                : AsyncOpStatus.Pending;
        }

        /// <summary>
        /// Turns a request id back into the sprite whichever loader owns it resolved. Adapter-side
        /// only — this signature is exactly what the port is not allowed to expose.
        /// </summary>
        public bool TryGetSprite(int requestId, out Sprite sprite)
        {
            if (!_routes.TryGet(requestId, out var route))
            {
                sprite = null;
                return false;
            }

            return route.Owner == AvatarMode.Local
                ? _local.TryGetSprite(route.InnerRequestId, out sprite)
                : _remote.TryGetSprite(route.InnerRequestId, out sprite);
        }

        public void Release(int requestId)
        {
            if (!_routes.Remove(requestId, out var route))
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
