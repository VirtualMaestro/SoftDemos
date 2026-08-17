using System;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.Layout
{
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class ResponsiveCanvas : MonoBehaviour
    {
        private static readonly Vector2 PortraitResolution = new(1080f, 1920f);
        private static readonly Vector2 LandscapeResolution = new(1920f, 1080f);

        [SerializeField] private CanvasScaler scaler;

        private int _width = -1;
        private int _height = -1;

        /// <summary>Raised on every resolution change, mode flip or not. This is the shell's single
        /// "the window changed" signal, so nothing else has to poll <c>Screen</c> per frame.</summary>
        public event Action OnResolutionChanged;

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

            var isPortrait = width < height;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = isPortrait ? PortraitResolution : LandscapeResolution;
            scaler.matchWidthOrHeight = isPortrait ? 0f : 1f;

            OnResolutionChanged?.Invoke();
        }
    }
}
