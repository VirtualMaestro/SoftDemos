using Game.Simulation.Ports;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapters.Views
{
    /// <summary>
    /// Every sprite target the persistent shell owns, gathered in one serialized place.
    ///
    /// The shell's art is loaded by address like all other content, so the scene cannot hold the
    /// sprites — it holds the <see cref="Image"/> components that will receive them.
    /// <c>ShellStageSystem</c> is the one writer; this view only says where.
    /// </summary>
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

        /// <summary>How many demo icons this skin can paint. <c>EntryPoint</c> checks it against
        /// the demo list, so a fourth demo cannot be half-added.</summary>
        public int DemoIconCount => demoIcons == null ? 0 : demoIcons.Length;

        /// <summary>
        /// Reports every unassigned target through <paramref name="log"/> rather than throwing.
        /// A missing reference means one flat rectangle on screen, not a broken run — so the run
        /// continues and the console says which one.
        /// </summary>
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

        private bool _Check(ILog log, UnityEngine.Object reference, string fieldName)
        {
            // `?.` bypasses Unity's null overload, so a destroyed Image would slip past it.
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
