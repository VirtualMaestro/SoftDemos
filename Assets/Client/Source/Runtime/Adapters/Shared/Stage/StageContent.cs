using Client.Adapters.Services;
using Client.Simulation.Ports;
using DCFApixels.DragonECS;
using UnityEngine;

namespace Client.Adapters.Stage
{
    /// <summary>
    /// Shared plumbing for the demo stage systems: command entities, asset resolution,
    /// background handling and request release. Keeps the per-demo systems down to their
    /// demo-specific logic.
    /// </summary>
    internal static class StageContent
    {
        /// <summary>Creates a one-component command entity for the simulation to consume.</summary>
        public static void WriteCommand<T>(this EcsWorld world) where T : struct, IEcsComponent
        {
            world.GetPool<T>().Add(world.NewEntity());
        }

        public static T GetAsset<T>(AddressablesAssetService assets, int requestId)
            where T : Object
        {
            var handleId = assets.ResolveHandle(requestId);
            return assets.TryGetAsset(handleId, out var asset) ? asset as T : null;
        }

        /// <summary>
        /// Resolves a demo background, which can load as a <see cref="Sprite"/> or a
        /// <see cref="Texture2D"/> — the importer decides the type. When a sprite had to be
        /// created here, <paramref name="ownsSprite"/> is true and the caller must destroy it.
        /// </summary>
        public static Sprite ResolveBackground(
            AddressablesAssetService assets, int requestId, string demoName, ILog log,
            out bool ownsSprite)
        {
            ownsSprite = false;
            var handleId = assets.ResolveHandle(requestId);

            if (assets.TryGetAsset(handleId, out var asset) == false)
            {
                log.Error($"{demoName} background address did not resolve.");
                return null;
            }

            if (asset is Sprite sprite)
                return sprite;

            if (asset is not Texture2D texture)
            {
                log.Error($"{demoName} background resolved as {asset.GetType().Name}, " +
                    "expected Sprite or Texture2D.");
                return null;
            }

            var created = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 150f);
            created.name = texture.name;
            ownsSprite = true;
            return created;
        }

        /// <summary>
        /// Finds the main camera if needed and cover-fits the background to the current screen
        /// size. Returns the camera so the caller can keep it cached.
        /// </summary>
        public static Camera FitBackground(
            Camera camera, Transform background, Sprite sprite, string demoName, ILog log,
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
            return camera;
        }

        /// <summary>Releases the request if it is open. Returns 0 so the caller can clear its id.</summary>
        public static int Release(AddressablesAssetService assets, int requestId)
        {
            if (requestId != 0)
                assets.Release(requestId);

            return 0;
        }

        public static void DestroyOwnedSprite(ref Sprite sprite, ref bool owns)
        {
            if (owns && sprite != null)
                Object.Destroy(sprite);

            owns = false;
            sprite = null;
        }
    }
}
