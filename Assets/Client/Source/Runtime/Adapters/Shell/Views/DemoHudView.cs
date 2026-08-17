using System.Collections.Generic;
using Client.Simulation.Menu;
using DCFApixels.DragonECS;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.Shell
{
    public sealed class DemoHudView : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text title;

        private EcsWorld _world;
        private IReadOnlyList<DemoEntry> _demos;

        /// <summary>Same contract as <see cref="MenuScreen.Bind"/>: binding is what makes the back
        /// button live, and the titles come from the one authored list.</summary>
        public void Bind(EcsWorld world, IReadOnlyList<DemoEntry> demos)
        {
            _world = world;
            _demos = demos;
            backButton.onClick.AddListener(_OnCloseDemo);
            backButton.interactable = true;
        }

        public void SetDemoIndex(int demoIndex)
        {
            title.text = _demos != null && demoIndex >= 0 && demoIndex < _demos.Count
                ? _demos[demoIndex].Title
                : string.Empty;
        }

        // See the note in MenuScreen.OnDestroy: `?.` does not respect Unity's null overload.
        private void OnDestroy()
        {
            if (backButton != null)
                backButton.onClick.RemoveListener(_OnCloseDemo);
        }

        private void _OnCloseDemo()
        {
            var entityId = _world.NewEntity();
            _world.GetPool<CloseDemoCommand>().Add(entityId);
            Debug.Log($"[DemoHudView] Wrote CloseDemoCommand on entity {entityId}.");
        }
    }
}
