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
    /// record are drained by <see cref="ShellInpSystem"/>. The <c>_last*</c> fields are a repaint
    /// cache — what is on screen right now — not state passed between systems.
    /// <para>The three views come from <see cref="ScreenRegistryService"/> per call, and each
    /// <see cref="CanvasGroup"/> comes off the view it sits on: a system holds no engine object
    /// (DEU0146). The backdrop and the loading indicator are the skin view's, which is where the
    /// scene already puts them.</para>
    /// </remarks>
    internal sealed class ScreenPreSystem : IEcsPresent, IEcsDestroy, IEcsInject<EcsWorld>,
        IEcsInject<FadePlayerService>, IEcsInject<ScreenRegistryService>
    {
        private const float FadeSeconds = 0.18f;

        private EcsWorld _world;
        private FadePlayerService _tweens;
        private ScreenRegistryService _screens;
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

        void IEcsDestroy.Destroy()
        {
            // The fade half of the safety net the old TweenPlayerService.KillAll carried: a fade
            // that outlives its world calls back into a destroyed pipeline.
            _tweens.KillFades();
        }

        public void Present()
        {
            if (!_screens.TryGet(out MenuScreen menu) ||
                !_screens.TryGet(out DemoHudView demoHud) ||
                !_screens.TryGet(out ShellSkinView skin))
                return;

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

            demoHud.SetDemoIndex(state.ActiveDemoIndex);
            // Keep the backdrop through the load, so the change is not a black flash. Remove it
            // for the demo, which brings its own by then. It sits outside SafeArea, so it belongs
            // to no screen.
            skin.Background.gameObject.SetActive(!demoVisible);

            _ApplyVisibility(menu.gameObject, menu.Group, menuVisible, ref _menuWasVisible);
            _ApplyVisibility(demoHud.gameObject, demoHud.Group, demoVisible, ref _demoWasVisible);

            _ApplyVisibility(
                skin.LoadingIndicator, skin.LoadingGroup, loadingVisible, ref _loadingWasVisible);

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
            if (target == null)
                return;

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
        public void Inject(ScreenRegistryService obj) => _screens = obj;
    }
}
