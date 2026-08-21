using UnityEngine;

namespace Client.Adapters.Shared.Layout
{
    /// <summary>Keeps a <see cref="RectTransform"/> inside <see cref="Screen.safeArea"/>.</summary>
    /// <remarks>
    /// <c>OnRectTransformDimensionsChange</c> drives it: a resolution or orientation change resizes
    /// the canvas, which resizes this stretched rect. <c>Screen.safeArea</c> is a native call, so it
    /// is not polled per frame.
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;

        private void Awake() => _rectTransform = (RectTransform)transform;

        private void OnEnable() => _Apply();

        private void OnRectTransformDimensionsChange()
        {
            // Unity calls this before Awake on scene load; OnEnable covers that pass.
            if (_rectTransform != null)
                _Apply();
        }

        private void _Apply()
        {
            var width = Screen.width;
            var height = Screen.height;

            if (width <= 0 || height <= 0)
                return;

            var safeArea = Screen.safeArea;
            _rectTransform.anchorMin = new Vector2(safeArea.xMin / width, safeArea.yMin / height);
            _rectTransform.anchorMax = new Vector2(safeArea.xMax / width, safeArea.yMax / height);
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
