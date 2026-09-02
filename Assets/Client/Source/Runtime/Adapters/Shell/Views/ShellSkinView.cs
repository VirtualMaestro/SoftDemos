using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.Shell.Views
{
    /// <summary>Every sprite target of the persistent shell, in one serialized place.</summary>
    /// <remarks>
    /// The shell art loads by address, so the scene holds the <see cref="Image"/> components and
    /// not the sprites. <c>ShellStageInpSystem</c> is the only writer. This view says where.
    /// </remarks>
    public sealed class ShellSkinView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image panel;
        [SerializeField] private Image[] buttons;
        [SerializeField] private Image[] demoIcons;
        [SerializeField] private Image backIcon;
        [SerializeField] private Image spinner;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private CanvasGroup loadingGroup;

        public Image Background => background;
        public Image Panel => panel;
        public Image[] Buttons => buttons;
        public Image[] DemoIcons => demoIcons;
        public Image BackIcon => backIcon;
        public Image Spinner => spinner;

        /// <summary>
        /// The loading indicator the shell shows between screens, and the group it fades in with.
        /// They live here rather than on the composition root because a system resolves this view
        /// through the screen registry and holds no engine object of its own (DEU0146). The
        /// indicator is the spinner's PARENT: the spinner turns, the parent is what appears.
        /// </summary>
        public GameObject LoadingIndicator => loadingIndicator;

        public CanvasGroup LoadingGroup => loadingGroup;

        /// <summary>How many demo icons this skin can paint. <c>Boot</c> checks it against the demo list.</summary>
        public int DemoIconCount => demoIcons?.Length ?? 0;

        private void OnValidate()
        {
            Debug.Assert(background != null, $"'{nameof(background)}' is not assigned on {nameof(ShellSkinView)}.", this);
            Debug.Assert(panel != null, $"'{nameof(panel)}' is not assigned on {nameof(ShellSkinView)}.", this);
            Debug.Assert(backIcon != null, $"'{nameof(backIcon)}' is not assigned on {nameof(ShellSkinView)}.", this);
            Debug.Assert(spinner != null, $"'{nameof(spinner)}' is not assigned on {nameof(ShellSkinView)}.", this);
            Debug.Assert(loadingIndicator != null, $"'{nameof(loadingIndicator)}' is not assigned on {nameof(ShellSkinView)}.", this);
            Debug.Assert(loadingGroup != null, $"'{nameof(loadingGroup)}' is not assigned on {nameof(ShellSkinView)}.", this);
            Debug.Assert(buttons != null && buttons.Length > 0,
                $"'{nameof(buttons)}' is empty on {nameof(ShellSkinView)}.", this);
            Debug.Assert(demoIcons != null && demoIcons.Length > 0,
                $"'{nameof(demoIcons)}' is empty on {nameof(ShellSkinView)}.", this);

            for (var i = 0; buttons != null && i < buttons.Length; i++)
                Debug.Assert(buttons[i] != null,
                    $"'{nameof(buttons)}[{i}]' is not assigned on {nameof(ShellSkinView)}.", this);

            for (var i = 0; demoIcons != null && i < demoIcons.Length; i++)
                Debug.Assert(demoIcons[i] != null,
                    $"'{nameof(demoIcons)}[{i}]' is not assigned on {nameof(ShellSkinView)}.", this);
        }
    }
}
