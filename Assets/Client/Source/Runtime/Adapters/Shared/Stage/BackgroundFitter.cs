using UnityEngine;

namespace Client.Adapters.Shared.Stage
{
    /// <summary>Cover-fit for a full-screen demo backdrop.</summary>
    /// <remarks>
    /// Scale is not enough. The camera in <c>Boot</c> sits above the origin, so a backdrop that is
    /// only scaled leaves a band of clear colour along the top. Always recentre it too.
    /// </remarks>
    public static class BackgroundFitter
    {
        /// <summary>
        /// Scales the backdrop to cover the viewport, recentres it on the camera, and hands back
        /// the orthographic size it fitted to — a caller that lays other things out on the same
        /// stage needs the same number.
        /// </summary>
        /// <remarks>
        /// A null <paramref name="camera"/> falls back to <see cref="Camera.main"/>: the screens
        /// serialize theirs as <c>[Optional]</c>. No camera at all is a scene error rather than a
        /// runtime condition, so it asserts instead of logging — and the fit still runs, at the
        /// default size, so a broken scene shows a stretched backdrop and not a blank one.
        /// </remarks>
        public static float CoverFit(Transform background, Sprite sprite, Camera camera,
            int screenWidth, int screenHeight)
        {
            if (camera == null)
                camera = Camera.main;

            Debug.Assert(camera != null,
                "No camera for the stage backdrop: assign the screen's StageCamera or tag one MainCamera.");

            var orthographicSize = camera != null ? camera.orthographicSize : 5f;

            // `?.` skips Unity's null overload, so check destroyed objects here.
            if (background == null || sprite == null || screenHeight <= 0)
                return orthographicSize;

            var spriteSize = sprite.bounds.size;
            var viewportHeight = orthographicSize * 2f;
            var viewportWidth = viewportHeight * screenWidth / screenHeight;
            var scale = Mathf.Max(viewportWidth / spriteSize.x, viewportHeight / spriteSize.y);
            background.localScale = new Vector3(scale, scale, 1f);

            if (camera == null)
                return orthographicSize;

            var cameraPosition = camera.transform.position;
            background.position =
                new Vector3(cameraPosition.x, cameraPosition.y, background.position.z);

            return orthographicSize;
        }
    }
}
