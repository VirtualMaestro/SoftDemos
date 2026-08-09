using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapters.Layout
{
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class ResponsiveCanvas : MonoBehaviour
    {
        private static readonly Vector2 PortraitResolution = new(1080f, 1920f);
        private static readonly Vector2 LandscapeResolution = new(1920f, 1080f);

        [SerializeField] private CanvasScaler scaler;

        private int _width = -1;
        private int _height = -1;
        private LayoutMode _mode;
        private bool _hasMode;

        /// <summary>Raised on every resolution change, mode flip or not. This is the shell's single
        /// "the window changed" signal, so nothing else has to poll <c>Screen</c> per frame.</summary>
        public event Action OnResolutionChanged;

        public LayoutMode Mode => _mode;

        private void Awake()
        {
            if (scaler == null)
                scaler = GetComponent<CanvasScaler>();
        }

        private void OnEnable()
        {
            _width = -1;
            _height = -1;
            _ApplyIfResolutionChanged();
        }

        private void Update() => _ApplyIfResolutionChanged();

        private void _ApplyIfResolutionChanged()
        {
            var width = Screen.width;
            var height = Screen.height;

            if (width == _width && height == _height)
                return;

            _width = width;
            _height = height;

            if (width <= 0 || height <= 0)
                return;

            var aspect = width / (float)height;
            var mode = LayoutModes.FromAspect(aspect);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution =
                mode == LayoutMode.Portrait ? PortraitResolution : LandscapeResolution;
            scaler.matchWidthOrHeight = mode == LayoutMode.Portrait ? 0f : 1f;

            OnResolutionChanged?.Invoke();

            if (_hasMode && mode == _mode)
                return;

            _mode = mode;
            _hasMode = true;
            var scale = mode == LayoutMode.Portrait
                ? width / PortraitResolution.x
                : height / LandscapeResolution.y;
            Debug.Log($"[ResponsiveCanvas] Mode={mode}, resolution={width}x{height}, " +
                $"aspect={aspect:0.###}, scale={scale:0.###}.");
        }
    }
}
