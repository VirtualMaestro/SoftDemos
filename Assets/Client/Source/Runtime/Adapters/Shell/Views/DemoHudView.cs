using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.Shell.Views
{
    public sealed class DemoHudView : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text title;

        private IReadOnlyList<DemoEntry> _demos;

        /// <summary>Set when the back button is clicked, cleared by the system that drains it.</summary>
        /// <remarks>
        /// <c>ShellInpSystem</c> turns this into a <c>CloseDemoCommand</c> inside its phase; the
        /// view knows nothing about the world. A pending press is not world state — it has no
        /// lifetime and no owner — and holding it here is what makes the frame the command lands
        /// in readable.
        /// </remarks>
        public bool CloseRequested { get; set; }

        /// <summary>
        /// The group the shell fades this HUD in with. It sits on this object, so the view resolves
        /// it once and the system that fades reads it here — a system holds no engine object of its
        /// own (DEU0146).
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
            _demos = demos;
            backButton.onClick.AddListener(_OnBackPressed);
            backButton.interactable = true;
        }

        public void SetDemoIndex(int demoIndex)
        {
            title.text = _demos != null && demoIndex >= 0 && demoIndex < _demos.Count
                ? _demos[demoIndex].Title
                : string.Empty;
        }

        private void OnDestroy()
        {
            if (backButton != null)
                backButton.onClick.RemoveListener(_OnBackPressed);
        }

        private void _OnBackPressed() => CloseRequested = true;

        private void OnValidate()
        {
            Debug.Assert(backButton != null, $"'{nameof(backButton)}' is not assigned on {nameof(DemoHudView)}.", this);
            Debug.Assert(title != null, $"'{nameof(title)}' is not assigned on {nameof(DemoHudView)}.", this);
        }
    }
}
