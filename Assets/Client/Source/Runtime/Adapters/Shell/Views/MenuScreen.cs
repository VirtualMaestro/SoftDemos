using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.Shell
{
    public sealed class MenuScreen : MonoBehaviour
    {
        [SerializeField] private Button[] demoButtons;

        /// <summary>Raised with the demo index when its button is clicked. A system turns this
        /// into an <c>OpenDemoCommand</c>; the view knows nothing about the world.</summary>
        public event Action<int> OnDemoPressed;

        /// <summary>How many entries this screen can show. <c>EntryPoint</c> checks it against the catalog.</summary>
        public int ButtonCount => demoButtons?.Length ?? 0;

        public void SetDemos(IReadOnlyList<DemoEntry> demos)
        {
            for (var i = 0; i < demoButtons.Length; i++)
            {
                var demoIndex = i;
                var button = demoButtons[i];
                button.onClick.AddListener(() => OnDemoPressed?.Invoke(demoIndex));
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
    }
}
