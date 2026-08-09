using UnityEngine;

namespace Game.Adapters.Layout
{
    /// <summary>
    /// Keeps a <see cref="RectTransform"/> inside <see cref="Screen.safeArea"/>, so shell UI does
    /// not end up under a notch or a home indicator.
    ///
    /// Driven by <see cref="ResponsiveCanvas.OnResolutionChanged"/> rather than by a per-frame poll:
    /// the safe area only moves when the surface it is measured against does, and this ships to
    /// WebGL next to a demo running 144 animated sprites. `Screen.safeArea` is a native call, and
    /// a shell that costs one every frame forever is a cost with no event behind it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private ResponsiveCanvas responsiveCanvas;

        private RectTransform _rectTransform;

        private void Awake() => _rectTransform = (RectTransform)transform;

        private void OnEnable()
        {
            if (responsiveCanvas == null)
                Debug.LogError($"[SafeAreaFitter] '{name}' has no ResponsiveCanvas assigned; the " +
                    "safe area will never be re-applied after a resolution change.", this);
            else
                responsiveCanvas.OnResolutionChanged += _OnApply;

            _OnApply();
        }

        private void OnDisable()
        {
            if (responsiveCanvas != null)
                responsiveCanvas.OnResolutionChanged -= _OnApply;
        }

        private void _OnApply()
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
