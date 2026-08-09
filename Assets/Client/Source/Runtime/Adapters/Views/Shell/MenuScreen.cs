using System.Collections.Generic;
using DCFApixels.DragonECS;
using Game.Simulation.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapters.Views
{
    public sealed class MenuScreen : MonoBehaviour
    {
        [SerializeField] private Button[] demoButtons;

        private EcsWorld _world;

        /// <summary>How many entries this screen can show. <c>EntryPoint</c> checks it against the
        /// catalog it was authored with, so a fourth demo cannot be half-added.</summary>
        public int ButtonCount => demoButtons?.Length ?? 0;

        /// <summary>
        /// Registers the click listeners and labels every button from <paramref name="demos"/>.
        ///
        /// Binding — not <c>Awake</c> — is what makes the buttons live: they ship
        /// <c>interactable = false</c>, because Unity runs every <c>Awake</c> before any
        /// <c>Start</c>, so a click landing before <c>EntryPoint.Start</c> would write into a null
        /// world. The labels come from the same list that supplies the scene addresses, so button
        /// order and scene order are one fact rather than two that can drift.
        /// </summary>
        public void Bind(EcsWorld world, IReadOnlyList<DemoEntry> demos)
        {
            _world = world;

            for (var i = 0; i < demoButtons.Length; i++)
            {
                var demoIndex = i;
                var button = demoButtons[i];
                button.onClick.AddListener(() => _WriteCommand(demoIndex));
                button.interactable = true;

                var label = button.GetComponentInChildren<TMP_Text>();

                if (label != null)
                    label.text = demos[i].Title;
            }
        }

        // A destroyed UnityEngine.Object is not null — it only compares equal to null through
        // Unity's overloaded operator. `?.` bypasses that overload and hands back a live-looking
        // reference, so the null check has to be written out (Unity analyzer UNT0008).
        private void OnDestroy()
        {
            if (demoButtons == null)
                return;

            foreach (var button in demoButtons)
                if (button != null)
                    button.onClick.RemoveAllListeners();
        }

        private void _WriteCommand(int demoIndex)
        {
            var entityId = _world.NewEntity();
            _world.GetPool<OpenDemoCommand>().Add(entityId).DemoIndex = demoIndex;
            Debug.Log($"[MenuScreen] Wrote OpenDemoCommand({demoIndex}) on entity {entityId}.");
        }
    }
}
