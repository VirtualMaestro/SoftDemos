using UnityEngine;

namespace Game.Adapters.Bindings
{
    /// <summary>
    /// The one cover-fit for a full-screen demo backdrop.
    ///
    /// This used to be a private <c>_CoverFitBackground</c> copied verbatim into all three stage
    /// systems, and all three carried the same defect: they scaled the sprite but never moved it.
    /// Scaling alone centres the backdrop on <b>its own origin</b>, while <c>Boot</c>'s camera sits
    /// at <c>y = 1</c> — so a backdrop authored at <c>y = 0</c> covered world space −5..+5 while the
    /// camera looked at −4..+6, and every demo showed a one-world-unit band of camera clear colour
    /// along the top. One bug, three copies; hence one helper.
    /// </summary>
    public static class BackgroundFitter
    {
        /// <summary>
        /// Scales <paramref name="background"/> to cover the viewport and recentres it on the
        /// camera. <paramref name="orthographicSize"/> is passed in rather than read from
        /// <paramref name="camera"/> because the callers already resolve a fallback for a missing
        /// <c>MainCamera</c> — and still fit as well as they can when there is none.
        /// </summary>
        public static void CoverFit(Transform background, Sprite sprite, Camera camera,
            float orthographicSize, int screenWidth, int screenHeight)
        {
            // `?.` bypasses Unity's null overload, so destroyed objects have to be checked out loud.
            if (background == null || sprite == null || screenHeight <= 0)
                return;

            var spriteSize = sprite.bounds.size;
            var viewportHeight = orthographicSize * 2f;
            var viewportWidth = viewportHeight * screenWidth / screenHeight;
            var scale = Mathf.Max(viewportWidth / spriteSize.x, viewportHeight / spriteSize.y);
            background.localScale = new Vector3(scale, scale, 1f);

            if (camera == null)
                return;

            var cameraPosition = camera.transform.position;
            background.position =
                new Vector3(cameraPosition.x, cameraPosition.y, background.position.z);
        }
    }
}
