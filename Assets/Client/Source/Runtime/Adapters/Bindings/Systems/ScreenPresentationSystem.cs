using DCFApixels.DragonECS;
using Game.Adapters.Views;
using Game.Simulation.Menu;
using Game.Simulation.Ports;
using UnityEngine;

namespace Game.Adapters.Bindings
{
    public sealed class ScreenPresentationSystem : IEcsLateRun, IEcsInject<EcsWorld>,
        IEcsInject<ILog>
    {
        private const float FadeSeconds = 0.18f;

        private readonly MenuScreen _menu;
        private readonly DemoHudView _demoHud;
        private readonly GameObject _loadingIndicator;
        /// <summary>The shell backdrop. It sits outside <c>SafeArea</c>, so it belongs to no screen.</summary>
        /// <remarks>Nothing else hides it. On an overlay canvas it would cover the whole demo.</remarks>
        private readonly GameObject _menuBackground;
        private readonly TweenPlayer _tweens;
        private readonly StageReadyChannel _stageReady;
        private readonly CanvasGroup _menuGroup;
        private readonly CanvasGroup _demoHudGroup;
        private readonly CanvasGroup _loadingGroup;

        private EcsWorld _world;
        private ILog _log;
        private ScreenId _lastScreen;
        private int _lastDemoIndex;
        private bool _lastStageReady;
        private bool _hasState;
        private bool _menuWasVisible;
        private bool _demoWasVisible;
        private bool _loadingWasVisible;

        public ScreenPresentationSystem(
            MenuScreen menu,
            DemoHudView demoHud,
            GameObject loadingIndicator,
            ShellSkinView shellSkin,
            TweenPlayer tweens,
            StageReadyChannel stageReady)
        {
            _menu = menu;
            _demoHud = demoHud;
            _loadingIndicator = loadingIndicator;
            _menuBackground = shellSkin.Background.gameObject;
            _tweens = tweens;
            _stageReady = stageReady;

            // Resolve once. The screen never changes, and LateRun runs on every frame.
            _menuGroup = menu.GetComponent<CanvasGroup>();
            _demoHudGroup = demoHud.GetComponent<CanvasGroup>();
            _loadingGroup = loadingIndicator.GetComponent<CanvasGroup>();
        }

        public void LateRun()
        {
            ref readonly var state = ref _world.Get<ScreenStateComp>();
            var stageReady = _stageReady.IsReady;

            if (_hasState &&
                state.Current == _lastScreen &&
                state.ActiveDemoIndex == _lastDemoIndex &&
                stageReady == _lastStageReady)
                return;

            // `ScreenId.Demo` means only that the scene landed. The stage system paints its
            // background a few frames later, and the demo draws nothing until then. Keep the
            // shell up for those frames, or the camera clear colour shows through.
            var demoActive = state.Current == ScreenId.Demo;
            var menuVisible = state.Current == ScreenId.Menu;
            var demoVisible = demoActive && stageReady;
            var loadingVisible = state.Current == ScreenId.Loading ||
                state.Current == ScreenId.Unloading ||
                (demoActive && stageReady == false);

            _demoHud.SetDemoIndex(state.ActiveDemoIndex);
            // Keep the backdrop through the load, so the change is not a black flash. Remove it
            // for the demo, which brings its own by then.
            _menuBackground.SetActive(demoVisible == false);

            _ApplyVisibility(_menu.gameObject, _menuGroup, menuVisible, ref _menuWasVisible);
            _ApplyVisibility(_demoHud.gameObject, _demoHudGroup, demoVisible, ref _demoWasVisible);
            _ApplyVisibility(_loadingIndicator, _loadingGroup, loadingVisible, ref _loadingWasVisible);

            _lastScreen = state.Current;
            _lastDemoIndex = state.ActiveDemoIndex;
            _lastStageReady = stageReady;
            _hasState = true;

            _log.Info($"Screen presentation: menu={menuVisible}, demo={demoVisible}, " +
                $"loading={loadingVisible}, stageReady={stageReady}, " +
                $"demoIndex={state.ActiveDemoIndex}.");
        }

        /// <summary>Shows or hides one screen. It fades in only on the rising edge.</summary>
        /// <remarks>
        /// The indicator spans two states: <c>Loading</c> and the unpainted part of <c>Demo</c>.
        /// A second fade at that boundary would add a blink. Only the screen that appears fades.
        /// A fade-out needs the object to outlive the change that hid it.
        /// </remarks>
        private void _ApplyVisibility(
            GameObject target, CanvasGroup group, bool isVisible, ref bool wasVisible)
        {
            target.SetActive(isVisible);
            var isRisingEdge = isVisible && wasVisible == false;
            wasVisible = isVisible;

            // `?.` skips Unity's null overload, so a destroyed group would pass the check.
            if (isRisingEdge == false || group == null)
                return;

            _tweens.FadeIn(group, FadeSeconds);
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILog obj) => _log = obj;
    }
}
