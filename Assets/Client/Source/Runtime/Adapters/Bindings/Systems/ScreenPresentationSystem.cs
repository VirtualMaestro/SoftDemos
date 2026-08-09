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
        /// <summary>
        /// The shell backdrop, which lives outside <c>SafeArea</c> and so is not a child of any
        /// screen — nothing would ever hide it. On a Screen Space Overlay canvas that means it
        /// draws over the demo's world sprites and the demo is simply not there.
        /// </summary>
        private readonly GameObject _menuBackground;
        private readonly TweenPlayer _tweens;
        private readonly CanvasGroup _menuGroup;
        private readonly CanvasGroup _demoHudGroup;
        private readonly CanvasGroup _loadingGroup;

        private EcsWorld _world;
        private ILog _log;
        private ScreenId _lastScreen;
        private int _lastDemoIndex;
        private bool _hasState;

        public ScreenPresentationSystem(
            MenuScreen menu,
            DemoHudView demoHud,
            GameObject loadingIndicator,
            ShellSkinView shellSkin,
            TweenPlayer tweens)
        {
            _menu = menu;
            _demoHud = demoHud;
            _loadingIndicator = loadingIndicator;
            _menuBackground = shellSkin.Background.gameObject;
            _tweens = tweens;

            // Resolved once here rather than per transition: GetComponent on a screen that never
            // changes is a constant, and LateRun already runs on every frame of a demo.
            _menuGroup = menu.GetComponent<CanvasGroup>();
            _demoHudGroup = demoHud.GetComponent<CanvasGroup>();
            _loadingGroup = loadingIndicator.GetComponent<CanvasGroup>();
        }

        public void LateRun()
        {
            ref readonly var state = ref _world.Get<ScreenStateComp>();

            if (_hasState &&
                state.Current == _lastScreen &&
                state.ActiveDemoIndex == _lastDemoIndex)
                return;

            var menuVisible = state.Current == ScreenId.Menu;
            var demoVisible = state.Current == ScreenId.Demo;
            var loadingVisible =
                state.Current == ScreenId.Loading || state.Current == ScreenId.Unloading;

            _menu.gameObject.SetActive(menuVisible);
            _demoHud.gameObject.SetActive(demoVisible);
            _loadingIndicator.SetActive(loadingVisible);
            // The backdrop stays up through the load so the transition is not a black flash, and
            // goes away for the demo, which brings its own.
            _menuBackground.SetActive(demoVisible == false);
            _demoHud.SetDemoIndex(state.ActiveDemoIndex);

            // Only the appearing screen fades. A fade-out would need the object to outlive the
            // transition that hid it, which is a state machine rather than a transition — and the
            // early-out above already guarantees this runs once per change, not once per frame.
            _FadeInIfVisible(_menuGroup, menuVisible);
            _FadeInIfVisible(_demoHudGroup, demoVisible);
            _FadeInIfVisible(_loadingGroup, loadingVisible);

            _lastScreen = state.Current;
            _lastDemoIndex = state.ActiveDemoIndex;
            _hasState = true;

            _log.Info($"Screen presentation: menu={menuVisible}, demo={demoVisible}, " +
                $"loading={loadingVisible}, demoIndex={state.ActiveDemoIndex}.");
        }

        private void _FadeInIfVisible(CanvasGroup group, bool isVisible)
        {
            // `?.` bypasses Unity's null overload, so a destroyed group would slip past it.
            if (isVisible == false || group == null)
                return;

            _tweens.FadeIn(group, FadeSeconds);
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILog obj) => _log = obj;
    }
}
