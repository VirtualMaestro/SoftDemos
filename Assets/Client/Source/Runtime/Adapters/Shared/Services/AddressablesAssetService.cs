using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Client.Adapters.Shared.Async;
using Client.Simulation.Core.Ports;
using Client.Simulation.Core.Ports.Requests;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;
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
    ///
    /// This is also the owner of every object DERIVED from a loaded asset — a sprite cut from an
    /// atlas, a sprite created from a texture — because a copy lives no longer than the handle its
    /// parent was loaded under. <see cref="Derive"/> cuts it once, keys it by an id of its own, and
    /// serves it through the same <see cref="TryGetAsset"/>; releasing the parent destroys it. The
    /// alternative is what this project used to do: every system that cut a copy kept it and
    /// destroyed it, and the same bookkeeping stood in four places.
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

        public int Request(in AssetLoadRequest request)
        {
            var entry = new Entry(request.Address, parentId: 0);
            var requestId = _Add(entry);

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

        /// <summary>
        /// Cuts one object out of a loaded one and keeps it under an id of its own — a sprite from
        /// an atlas, a sprite from a texture. Adapter-side only, like <see cref="TryGetAsset"/>.
        ///
        /// <paramref name="cut"/> is the caller's factory: it takes a parent id and a way to make
        /// the copy, runs it ONCE, and owns the result from then on. Returns 0 — the "no request"
        /// sentinel every id carries — when the parent is not Done or the factory produced nothing.
        ///
        /// This stays the GENERAL cut. <see cref="DeriveSprite"/>, <see cref="ResolveSprite"/> and
        /// <see cref="ReadAtlasNames"/> are the sprite-shaped ones every 2D consumer needs, and
        /// they live here rather than in a static helper that took this service as its first
        /// parameter: the service already stands on Addressables and <see cref="Object"/>, so a
        /// <see cref="SpriteAtlas"/> and a <see cref="Texture2D"/> are engine knowledge and not
        /// project knowledge. adr-an-engine-object-has-one-owner-per-kind is unchanged — every one
        /// of them wraps this method or <see cref="TryGetAsset"/>, and this service still owns
        /// every result.
        /// </summary>
        public int Derive(int parentId, Func<Object, Object> cut)
        {
            if (cut is null)
                throw new ArgumentNullException(nameof(cut));

            if (!TryGetAsset(parentId, out var parent))
            {
                _log.Error($"Derive from #{parentId} rejected: the parent is not Done.");
                return 0;
            }

            var derived = cut(parent);

            if (derived == null)
            {
                _log.Error($"Derive from #{parentId} produced nothing.");
                return 0;
            }

            var entry = new Entry($"derived from #{parentId}", parentId)
            {
                Status = AsyncOpStatus.Done
            };

            var requestId = _Add(entry);
            _assets.Add(requestId, derived);
            return requestId;
        }

        /// <summary>
        /// Cuts one named sprite out of an atlas under an id of its own, which this service owns.
        /// 0 when the request is not an atlas, or the atlas carries no such name.
        /// </summary>
        /// <remarks>
        /// The rename is the reason this is one method instead of a copy of the same lambda per
        /// caller: <c>GetSprite</c> names its copy <c>&lt;name&gt;(Clone)</c>, and every caller
        /// wants the name it asked for — the shell asserts on it, and a reader looking at the
        /// hierarchy reads it.
        /// </remarks>
        public int DeriveSprite(int atlasRequestId, string spriteName)
        {
            // Checked here rather than in the factory: a cast that throws inside `cut` would take
            // the caller down for what is a content error the service can report.
            if (TryGetAsset(atlasRequestId, out var parent) && parent is not SpriteAtlas)
            {
                _log.Error(
                    $"Request #{atlasRequestId} is not a SpriteAtlas; cannot cut '{spriteName}'.");
                return 0;
            }

            return Derive(atlasRequestId, asset =>
            {
                var sprite = ((SpriteAtlas)asset).GetSprite(spriteName);

                if (sprite != null)
                    sprite.name = spriteName;

                return sprite;
            });
        }

        /// <summary>
        /// Resolves a request that can have loaded as a <see cref="Sprite"/> or a
        /// <see cref="Texture2D"/> — the importer decides the type — and answers with the id the
        /// sprite is served under, never with the sprite. One that loaded as a sprite is served
        /// under its own request id; one that loaded as a texture is cut once and served under a
        /// derived id this service owns. Either way the caller keeps an <c>int</c>. 0 means it did
        /// not resolve.
        /// </summary>
        public int ResolveSprite(int requestId)
        {
            if (!TryGetAsset(requestId, out var asset))
            {
                _log.Error($"Request #{requestId} did not resolve.");
                return 0;
            }

            if (asset is Sprite)
                return requestId;

            if (asset is not Texture2D)
            {
                _log.Error($"Request #{requestId} resolved as {asset.GetType().Name}, " +
                    "expected Sprite or Texture2D.");
                return 0;
            }

            return Derive(requestId, parent =>
            {
                var texture = (Texture2D)parent;

                var created = Sprite.Create(texture,
                    new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 150f);

                created.name = texture.name;
                return created;
            });
        }

        /// <summary>
        /// The names of the sprites in an atlas, with the clones the engine minted to answer the
        /// question destroyed before returning. The array is sized to what <c>GetSprites</c>
        /// actually filled, so its length IS the sprite count a caller compares against. Null when
        /// the request is not a loaded <see cref="SpriteAtlas"/>.
        /// </summary>
        /// <remarks>
        /// <c>GetSprites</c> is the only way to enumerate an atlas and it allocates a fresh copy
        /// per sprite. Those copies are the engine's answer to a question, not content anybody
        /// owns; the sprites that LIVE are cut one at a time through <see cref="DeriveSprite"/>,
        /// which this service owns. Names are what cross from here, which is why this returns
        /// strings and not sprites.
        /// </remarks>
        public string[] ReadAtlasNames(int atlasRequestId)
        {
            if (!TryGetAsset(atlasRequestId, out var asset) || asset is not SpriteAtlas atlas)
            {
                _log.Error($"Request #{atlasRequestId} is not a SpriteAtlas.");
                return null;
            }

            var clones = new Sprite[atlas.spriteCount];
            var readCount = atlas.GetSprites(clones);
            var names = new string[readCount];

            for (var index = 0; index < clones.Length; index++)
            {
                if (clones[index] == null)
                {
                    if (index < readCount)
                        names[index] = string.Empty;

                    continue;
                }

                if (index < readCount)
                    // GetSprites names each clone "<name>(Clone)".
                    names[index] = clones[index].name.Replace("(Clone)", string.Empty).Trim();

                Object.Destroy(clones[index]);
            }

            return names;
        }

        /// <summary>
        /// Releases the request and clears the caller's id in one call — "release and forget".
        /// </summary>
        /// <remarks>
        /// No <c>!= 0</c> guard: releasing 0 is already a no-op, because the request table has no
        /// row under it.
        /// </remarks>
        public void Release(ref int requestId)
        {
            Release(requestId);
            requestId = 0;
        }

        /// <summary>
        /// Releases the request, and every object derived from it first — a copy cut from an asset
        /// cannot outlive the handle that asset was loaded under.
        /// </summary>
        public void Release(int requestId)
        {
            if (!_requests.Remove(requestId, out var entry))
                return;

            // The parent leaves the table before the walk, so a child never finds it and the
            // recursion is one level deep whatever the caller passes.
            _ReleaseDerivedChildrenOf(requestId);
            _ReleaseOwn(requestId, entry);
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

            // Derived copies first: each one is a live object cut from an asset whose handle the
            // next loop releases, and the engine destroys a copy on its own for nobody.
            foreach (var entry in _requests.Values)
            {
                if (entry.ParentId != 0)
                    _DestroyDerived(entry.Id);
            }

            foreach (var entry in _requests.Values)
            {
                if (entry.ParentId == 0)
                    _ReleaseHandleQuietly(entry);
            }

            _requests.Clear();
            _assets.Clear();
        }

        /// <summary>Files the entry and stamps it with the id the table minted for it.</summary>
        private int _Add(Entry entry)
        {
            var requestId = _requests.Add(entry);
            entry.Id = requestId;
            return requestId;
        }

        /// <summary>
        /// Releases every entry derived from <paramref name="parentId"/>. The ids are collected
        /// first: <see cref="RequestTable{TRequest}.Values"/> is the dictionary's own live
        /// collection, and releasing while enumerating it throws.
        /// </summary>
        private void _ReleaseDerivedChildrenOf(int parentId)
        {
            List<int> children = null;

            foreach (var candidate in _requests.Values)
            {
                if (candidate.ParentId != parentId)
                    continue;

                children ??= new List<int>();
                children.Add(candidate.Id);
            }

            if (children is null)
                return;

            foreach (var childId in children)
                Release(childId);
        }

        /// <summary>
        /// Frees what one entry held: a derived entry owns a copy this service cut and destroys it,
        /// a loaded entry owns an Addressables handle and releases it. A derived entry has no
        /// handle, so <see cref="_ReleaseHandleQuietly"/> is not a path it can take.
        /// </summary>
        private void _ReleaseOwn(int requestId, Entry entry)
        {
            if (entry.ParentId != 0)
            {
                _DestroyDerived(requestId);
                return;
            }

            _assets.Remove(requestId);
            _ReleaseHandleQuietly(entry);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _DestroyDerived(int requestId)
        {
            if (_assets.TryGetValue(requestId, out var derived) && derived != null)
                Object.Destroy(derived);

            _assets.Remove(requestId);
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

        // Properties are used only for tests
        /// <summary>
        /// Loads started but not yet released. Must reach 0 on a clean shutdown.
        /// </summary>
        /// <remarks>
        /// Derived entries are deliberately not counted: <see cref="Derive"/> starts nothing, it
        /// cuts a copy out of something already loaded, and a caller checking that every REQUEST it
        /// made came back is asking about loads. <see cref="HeldAssetCount"/> is the count that
        /// includes them.
        /// </remarks>
        internal int OpenRequestCount
        {
            get
            {
                var count = 0;

                foreach (var entry in _requests.Values)
                    if (entry.ParentId == 0)
                        count++;

                return count;
            }
        }

        /// <summary>
        /// Assets currently held, loaded and derived alike — a sprite cut from an atlas counts as
        /// its own row. Must reach 0 on a clean shutdown.
        /// </summary>
        internal int HeldAssetCount => _assets.Count;

        private sealed class Entry
        {
            public readonly string Address;

            /// <summary>The entry this one was cut from, or 0 when Addressables loaded it. A
            /// derived entry carries no handle and its object is destroyed rather than
            /// released.</summary>
            public readonly int ParentId;

            /// <summary>The id the table minted, so a parent can find its children by it.</summary>
            public int Id;

            public AsyncOperationHandle<Object> Handle;
            public AsyncOpStatus Status = AsyncOpStatus.Pending;

            public Entry(string address, int parentId)
            {
                Address = address;
                ParentId = parentId;
            }
        }
    }
}
