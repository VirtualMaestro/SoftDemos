using Client.Adapters.Shared.Services;
using Client.Simulation.Core.Ports;
using UnityEngine;
using UnityEngine.U2D;

namespace Client.Adapters.Shared.Stage
{
    /// <summary>
    /// Shared plumbing for the demo stage systems: asset resolution, background handling and
    /// request release. Keeps the per-demo systems down to their demo-specific logic — and
    /// touches no world: a system writes its command entities itself, in one visible line.
    /// </summary>
    public static class StageContent
    {
        public static T GetAsset<T>(AddressablesAssetService assets, int requestId)
            where T : Object
        {
            return assets.TryGetAsset(requestId, out var asset) ? asset as T : null;
        }

        /// <summary>
        /// Resolves a demo background, which can load as a <see cref="Sprite"/> or a
        /// <see cref="Texture2D"/> — the importer decides the type — and answers with the id the
        /// sprite is served under, never with the sprite. A background that loaded as a sprite is
        /// served under its own request id; one that loaded as a texture is cut once and served
        /// under a derived id the asset service owns. Either way the caller keeps an <c>int</c>,
        /// and nobody but the service destroys anything. 0 means it did not resolve.
        /// </summary>
        public static int ResolveBackground(
            AddressablesAssetService assets, int requestId, string demoName, ILogService log)
        {
            if (!assets.TryGetAsset(requestId, out var asset))
            {
                log.Error($"{demoName} background address did not resolve.");
                return 0;
            }

            if (asset is Sprite)
                return requestId;

            if (asset is not Texture2D)
            {
                log.Error($"{demoName} background resolved as {asset.GetType().Name}, " +
                    "expected Sprite or Texture2D.");
                return 0;
            }

            return assets.Derive(requestId, parent =>
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
        /// question destroyed before returning. <paramref name="readCount"/> is what
        /// <c>GetSprites</c> actually filled, so a caller can compare it against
        /// <c>spriteCount</c>.
        /// </summary>
        /// <remarks>
        /// <c>GetSprites</c> is the only way to enumerate an atlas and it allocates a fresh copy
        /// per sprite. Those copies are the engine's answer to a question, not content anybody
        /// owns; the sprites that LIVE are cut one at a time through
        /// <c>AddressablesAssetService.Derive</c>, which owns them. Names are what cross from here,
        /// which is why this returns strings and not sprites.
        /// </remarks>
        public static string[] ReadAtlasNames(SpriteAtlas atlas, out int readCount)
        {
            var clones = new Sprite[atlas.spriteCount];
            readCount = atlas.GetSprites(clones);

            var names = new string[clones.Length];

            for (var index = 0; index < clones.Length; index++)
            {
                if (clones[index] == null)
                {
                    names[index] = string.Empty;
                    continue;
                }

                // GetSprites names each clone "<name>(Clone)".
                names[index] = clones[index].name.Replace("(Clone)", string.Empty).Trim();
                Object.Destroy(clones[index]);
            }

            return names;
        }

        /// <summary>
        /// Cuts one named sprite out of an atlas under an id of its own, which the asset service
        /// owns. 0 when the atlas carries no such name.
        /// </summary>
        /// <remarks>
        /// The rename is the reason this is one method instead of four copies of the same lambda:
        /// <c>GetSprite</c> names its copy <c>&lt;name&gt;(Clone)</c>, and every caller wants the
        /// name it asked for — the shell asserts on it, and a reader looking at the hierarchy reads
        /// it.
        /// </remarks>
        public static int DeriveFromAtlas(
            AddressablesAssetService assets, int atlasRequestId, string spriteName)
        {
            return assets.Derive(atlasRequestId, asset =>
            {
                var sprite = ((SpriteAtlas)asset).GetSprite(spriteName);

                if (sprite != null)
                    sprite.name = spriteName;

                return sprite;
            });
        }

        /// <summary>
        /// Cover-fits the background to the current screen size, falling back to the main camera
        /// when the screen carries none.
        /// </summary>
        /// <remarks>
        /// It used to hand the camera back so the caller could cache it. Nobody caches it now: a
        /// camera is a <c>UnityEngine.Object</c> and a system holds none (DEU0146), so the screen
        /// serializes it and this runs on a layout change, which is rare enough that the fallback
        /// lookup costs nothing.
        /// </remarks>
        public static void FitBackground(
            Camera camera, Transform background, Sprite sprite, string demoName, ILogService log,
            out float orthographicSize)
        {
            if (camera == null)
                camera = Camera.main;

            orthographicSize = 5f;

            if (camera != null)
                orthographicSize = camera.orthographicSize;
            else
                log.Error($"No MainCamera found for {demoName} layout; using orthographic size 5.");

            BackgroundFitter.CoverFit(background, sprite, camera, orthographicSize,
                Screen.width, Screen.height);
        }

        /// <summary>Releases the request if it is open. Returns 0 so the caller can clear its id.</summary>
        public static int Release(AddressablesAssetService assets, int requestId)
        {
            if (requestId != 0)
                assets.Release(requestId);

            return 0;
        }
    }
}
