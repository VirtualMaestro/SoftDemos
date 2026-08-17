using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.Shell
{
    public sealed class DemoHudView : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text title;

        private IReadOnlyList<DemoEntry> _demos;

        /// <summary>Raised when the back button is clicked. A system turns this into a
        /// <c>CloseDemoCommand</c>; the view knows nothing about the world.</summary>
        public event Action OnClosePressed;

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

        private void _OnBackPressed() => OnClosePressed?.Invoke();
    }
}
