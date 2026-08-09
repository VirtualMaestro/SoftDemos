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

        /// <summary>How many entries this screen can show. <c>EntryPoint</c> checks it against the catalog.</summary>
        public int ButtonCount => demoButtons?.Length ?? 0;

        /// <summary>Adds the click listeners and labels every button from <paramref name="demos"/>.</summary>
        /// <remarks>
        /// The buttons start disabled and this call enables them. Unity runs every <c>Awake</c>
        /// before any <c>Start</c>, so an earlier click would write into a world that does not
        /// exist yet. The labels and the scene addresses come from the same list.
        /// </remarks>
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

        // A destroyed object is not null. It only compares equal to null through Unity's operator.
        // `?.` skips that operator, so write the check out (analyzer UNT0008).
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
