using Client.Simulation.Core.Ports;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.Shell.Views
{
    /// <summary>Every sprite target of the persistent shell, in one serialized place.</summary>
    /// <remarks>
    /// The shell art loads by address, so the scene holds the <see cref="Image"/> components and
    /// not the sprites. <c>ShellStageSystem</c> is the only writer. This view says where.
    /// </remarks>
    public sealed class ShellSkinView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image panel;
        [SerializeField] private Image[] buttons;
        [SerializeField] private Image[] demoIcons;
        [SerializeField] private Image backIcon;
        [SerializeField] private Image spinner;

        public Image Background => background;
        public Image Panel => panel;
        public Image[] Buttons => buttons;
        public Image[] DemoIcons => demoIcons;
        public Image BackIcon => backIcon;
        public Image Spinner => spinner;

        /// <summary>How many demo icons this skin can paint. <c>EntryPoint</c> checks it against the demo list.</summary>
        public int DemoIconCount => demoIcons == null ? 0 : demoIcons.Length;

        /// <summary>Reports every unassigned target through <paramref name="log"/>. It does not throw.</summary>
        /// <remarks>A missing reference gives one flat rectangle on screen. The game keeps running.</remarks>
        public bool HasEveryReference(ILog log)
        {
            var isComplete = true;
            isComplete &= _Check(log, background, nameof(background));
            isComplete &= _Check(log, panel, nameof(panel));
            isComplete &= _Check(log, backIcon, nameof(backIcon));
            isComplete &= _Check(log, spinner, nameof(spinner));
            isComplete &= _CheckArray(log, buttons, nameof(buttons));
            isComplete &= _CheckArray(log, demoIcons, nameof(demoIcons));
            return isComplete;
        }

        private bool _Check(ILog log, Object reference, string fieldName)
        {
            // `?.` skips Unity's null overload, so a destroyed Image would pass the check.
            if (reference != null)
                return true;

            log.Error($"{fieldName} is not assigned on {nameof(ShellSkinView)}.");
            return false;
        }

        private bool _CheckArray(ILog log, Image[] references, string fieldName)
        {
            if (references == null || references.Length == 0)
            {
                log.Error($"{fieldName} is empty on {nameof(ShellSkinView)}.");
                return false;
            }

            var isComplete = true;
            for (var i = 0; i < references.Length; i++)
                isComplete &= _Check(log, references[i], $"{fieldName}[{i}]");

            return isComplete;
        }
    }
}
