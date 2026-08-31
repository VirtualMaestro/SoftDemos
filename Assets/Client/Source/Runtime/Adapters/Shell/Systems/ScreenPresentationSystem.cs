using Client.Simulation.Core.Phases;
using Client.Adapters.Shared.Components;
using Client.Adapters.Shared.Services;
using Client.Adapters.Shell.Views;
using Client.Simulation.Core.Navigation;
using Client.Simulation.Core.Navigation.Components;
using DCFApixels.DragonECS;
using UnityEngine;

namespace Client.Adapters.Shell.Systems
{
    /// <summary>
    /// Shows/hides the menu, demo HUD and loading spinner based on screen state and readiness
    /// flags, with a fade-in when a panel appears.
    /// </summary>
    /// <remarks>
    /// Drawing only: it reads the world and writes the screen, and the presses its two views
    /// record are drained by <see cref="ShellInputSystem"/>. The <c>_last*</c> fields are a repaint
    /// cache — what is on screen right now — not state passed between systems.
    /// </remarks>
    internal sealed class ScreenPresentationSystem : IEcsPresent, IEcsDestroy, IEcsInject<EcsWorld>,
        IEcsInject<FadePlayerService>
    {
        private const float FadeSeconds = 0.18f;

        private readonly MenuScreen _menu;
        private readonly DemoHudView _demoHud;
        private readonly GameObject _loadingIndicator;
        /// <summary>The shell backdrop. It sits outside SafeArea, so it belongs to no screen.</summary>
        private readonly GameObject _menuBackground;
        private readonly CanvasGroup _menuGroup;
        private readonly CanvasGroup _demoHudGroup;
        private readonly CanvasGroup _loadingGroup;

        private EcsWorld _world;
        private FadePlayerService _tweens;
        private EcsTagPool<DemoReadyTag> _demoReady;
        private EcsTagPool<ShellReadyTag> _shellReady;
        private ScreenId _lastScreen;
        private int _lastDemoIndex;
        private bool _lastDemoReady;
        private bool _lastShellReady;
        private bool _hasState;
        private bool _menuWasVisible;
        private bool _demoWasVisible;
        private bool _loadingWasVisible;

        public ScreenPresentationSystem(
            MenuScreen menu,
            DemoHudView demoHud,
            GameObject loadingIndicator,
            ShellSkinView shellSkin)
        {
            _menu = menu;
            _demoHud = demoHud;
            _loadingIndicator = loadingIndicator;
            _menuBackground = shellSkin.Background.gameObject;

            // Resolve once. The screen never changes, and LateRun runs on every frame.
            _menuGroup = menu.GetComponent<CanvasGroup>();
            _demoHudGroup = demoHud.GetComponent<CanvasGroup>();
            _loadingGroup = loadingIndicator.GetComponent<CanvasGroup>();
        }

        void IEcsDestroy.Destroy()
        {
            // The fade half of the safety net the old TweenPlayerService.KillAll carried: a fade
            // that outlives its world calls back into a destroyed pipeline.
            _tweens.KillFades();
        }

        public void Present()
        {
            ref readonly var state = ref _world.Get<ScreenStateComp>();
            var demoReady = _demoReady.Count > 0;
            var shellReady = _shellReady.Count > 0;

            if (_hasState &&
                state.Current == _lastScreen &&
                state.ActiveDemoIndex == _lastDemoIndex &&
                demoReady == _lastDemoReady &&
                shellReady == _lastShellReady)
                return;

            // Show nothing before the shell has its own art. The menu panel and its buttons carry
            // Images that cannot be disabled without losing their raycasts, so they would appear
            // as white boxes for the whole first load. That is seconds on a real host.
            var demoActive = state.Current == ScreenId.Demo;
            var menuVisible = shellReady && state.Current == ScreenId.Menu;
            var demoVisible = shellReady && demoActive && demoReady;
            var loadingVisible = shellReady &&
                (state.Current == ScreenId.Loading ||
                 state.Current == ScreenId.Unloading ||
                 (demoActive && !demoReady));

            _demoHud.SetDemoIndex(state.ActiveDemoIndex);
            // Keep the backdrop through the load, so the change is not a black flash. Remove it
            // for the demo, which brings its own by then.
            _menuBackground.SetActive(!demoVisible);

            _ApplyVisibility(_menu.gameObject, _menuGroup, menuVisible, ref _menuWasVisible);
            _ApplyVisibility(_demoHud.gameObject, _demoHudGroup, demoVisible, ref _demoWasVisible);
            _ApplyVisibility(_loadingIndicator, _loadingGroup, loadingVisible, ref _loadingWasVisible);

            _lastScreen = state.Current;
            _lastDemoIndex = state.ActiveDemoIndex;
            _lastDemoReady = demoReady;
            _lastShellReady = shellReady;
            _hasState = true;
        }

        /// <summary>Shows or hides one screen. It fades in only on the rising edge.</summary>
        private void _ApplyVisibility(
            GameObject target, CanvasGroup group, bool isVisible, ref bool wasVisible)
        {
            target.SetActive(isVisible);
            var isRisingEdge = isVisible && !wasVisible;
            wasVisible = isVisible;

            if (!isRisingEdge || group == null)
                return;

            _tweens.FadeIn(group, FadeSeconds);
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _demoReady = obj.GetPool<DemoReadyTag>();
            _shellReady = obj.GetPool<ShellReadyTag>();
        }

        public void Inject(FadePlayerService obj) => _tweens = obj;
    }
}
