using Client.Adapters.Services;
using Client.Adapters.Shared;
using Client.Adapters.Views;
using Client.Simulation.Menu;
using Client.Simulation.Ports;
using DCFApixels.DragonECS;
using UnityEngine;

namespace Client.Adapters.Systems
{
    public sealed class ScreenPresentationSystem : IEcsLateRun, IEcsInject<EcsWorld>,
        IEcsInject<ILog>, IEcsInject<TweenPlayerService>, IEcsInject<StageReadyChannel>
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
        private ILog _log;
        private TweenPlayerService _tweens;
        private StageReadyChannel _stageReady;
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

        public void LateRun()
        {
            ref readonly var state = ref _world.Get<ScreenStateComp>();
            var demoReady = _stageReady.IsDemoReady;
            var shellReady = _stageReady.IsShellReady;

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
                 (demoActive && demoReady == false));

            _demoHud.SetDemoIndex(state.ActiveDemoIndex);
            // Keep the backdrop through the load, so the change is not a black flash. Remove it
            // for the demo, which brings its own by then.
            _menuBackground.SetActive(demoVisible == false);

            _ApplyVisibility(_menu.gameObject, _menuGroup, menuVisible, ref _menuWasVisible);
            _ApplyVisibility(_demoHud.gameObject, _demoHudGroup, demoVisible, ref _demoWasVisible);
            _ApplyVisibility(_loadingIndicator, _loadingGroup, loadingVisible, ref _loadingWasVisible);

            _lastScreen = state.Current;
            _lastDemoIndex = state.ActiveDemoIndex;
            _lastDemoReady = demoReady;
            _lastShellReady = shellReady;
            _hasState = true;

            _log.Info($"Screen presentation: menu={menuVisible}, demo={demoVisible}, " +
                $"loading={loadingVisible}, shellReady={shellReady}, demoReady={demoReady}, " +
                $"demoIndex={state.ActiveDemoIndex}.");
        }

        /// <summary>Shows or hides one screen. It fades in only on the rising edge.</summary>
        private void _ApplyVisibility(
            GameObject target, CanvasGroup group, bool isVisible, ref bool wasVisible)
        {
            target.SetActive(isVisible);
            var isRisingEdge = isVisible && wasVisible == false;
            wasVisible = isVisible;

            if (isRisingEdge == false || group == null)
                return;

            _tweens.FadeIn(group, FadeSeconds);
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILog obj) => _log = obj;
        public void Inject(TweenPlayerService obj) => _tweens = obj;
        public void Inject(StageReadyChannel obj) => _stageReady = obj;
    }
}
