using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.Shell.Views
{
    public sealed class MenuScreen : MonoBehaviour
    {
        [SerializeField] private Button[] demoButtons;

        /// <summary>The demo index whose button was clicked, or <c>-1</c> for none.</summary>
        /// <remarks>
        /// <c>ShellInpSystem</c> drains this into an <c>OpenDemoCommand</c> and puts it back to
        /// <c>-1</c>; the view knows nothing about the world. A pending press is not world state —
        /// it has no lifetime and no owner — and holding it here is what makes the frame the
        /// command lands in readable: a click that wrote the world from uGUI's callback landed in
        /// this frame or the next one, depending on when uGUI raised it.
        /// </remarks>
        public int RequestedDemoIndex { get; set; } = NoDemoRequested;

        /// <summary>The value <see cref="RequestedDemoIndex"/> holds while no button is pending.</summary>
        public const int NoDemoRequested = -1;

        /// <summary>How many entries this screen can show. <c>Boot</c> checks it against the catalog.</summary>
        public int ButtonCount => demoButtons?.Length ?? 0;

        /// <summary>
        /// The group the shell fades this screen in with. It sits on this object, so the view
        /// resolves it once and the system that fades reads it here — a system holds no engine
        /// object of its own (DEU0146).
        /// </summary>
        public CanvasGroup Group
        {
            get
            {
                if (_group == null)
                    _group = GetComponent<CanvasGroup>();

                return _group;
            }
        }

        private CanvasGroup _group;

        public void SetDemos(IReadOnlyList<DemoEntry> demos)
        {
            for (var i = 0; i < demoButtons.Length; i++)
            {
                var demoIndex = i;
                var button = demoButtons[i];
                button.onClick.AddListener(() => RequestedDemoIndex = demoIndex);
                button.interactable = true;

                var label = button.GetComponentInChildren<TMP_Text>();

                if (label != null)
                    label.text = demos[i].Title;
            }
        }

        private void OnDestroy()
        {
            if (demoButtons == null)
                return;

            foreach (var button in demoButtons)
                if (button != null)
                    button.onClick.RemoveAllListeners();
        }

        private void OnValidate()
        {
            Debug.Assert(demoButtons != null && demoButtons.Length > 0,
                $"'{nameof(demoButtons)}' is empty on {nameof(MenuScreen)}.", this);

            if (demoButtons == null)
                return;

            for (var i = 0; i < demoButtons.Length; i++)
                Debug.Assert(demoButtons[i] != null,
                    $"'{nameof(demoButtons)}[{i}]' is not assigned on {nameof(MenuScreen)}.", this);
        }
    }
}
