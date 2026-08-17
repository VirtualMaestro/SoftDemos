using UnityEngine;

namespace Client.Adapters.Stage
{
    /// <summary>Cover-fit for a full-screen demo backdrop.</summary>
    /// <remarks>
    /// Scale is not enough. The camera in <c>Boot</c> sits above the origin, so a backdrop that is
    /// only scaled leaves a band of clear colour along the top. Always recentre it too.
    /// </remarks>
    public static class BackgroundFitter
    {
        /// <summary>Scales the backdrop to cover the viewport and recentres it on the camera.</summary>
        /// <remarks>The size is a parameter because a caller can have a fallback for a missing camera.</remarks>
        public static void CoverFit(Transform background, Sprite sprite, Camera camera,
            float orthographicSize, int screenWidth, int screenHeight)
        {
            // `?.` skips Unity's null overload, so check destroyed objects here.
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
