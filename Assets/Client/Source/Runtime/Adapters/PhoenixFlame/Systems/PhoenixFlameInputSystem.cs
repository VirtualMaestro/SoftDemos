using Client.Simulation.Core.Phases;
using Client.Adapters.PhoenixFlame.Views;
using Client.Adapters.Shared.Services;
using Client.Adapters.Shared.Stage;
using Client.Simulation.PhoenixFlame.Components;
using Client.Simulation.Core.Navigation;
using Client.Simulation.Core.Navigation.Components;
using Client.Simulation.Core.Ports;
using DCFApixels.DragonECS;
using UnityEngine;
using UnityEngine.U2D;

namespace Client.Adapters.PhoenixFlame.Systems
{
    /// <summary>
    /// Runs the flame demo's screen lifecycle: loads atlas+background, hands the particle sprites
    /// to the view, drains the advance button into a command, tears everything down on close.
    /// </summary>
    /// <remarks>
    /// The Animator, the phase label and the button state moved to
    /// <see cref="PhoenixFlameViewSystem"/>, which is the half that reads the world and draws.
    /// What is left is what the Input phase is for: the port polling, the recorded press, the
    /// content this system owns and must destroy, and every write into the world.
    /// </remarks>
    public sealed class PhoenixFlameInputSystem : IEcsInput, IEcsDestroy,
        IEcsInject<EcsWorld>, IEcsInject<ILogService>, IEcsInject<AddressablesAssetService>,
        IEcsInject<ScreenRegistryService>
    {
        private const string AtlasAddress = "art/phoenix-flame/atlas";
        private const string BackgroundAddress = "art/phoenix-flame/background";
        private const string SmokeSpriteName = "smoke";
        private const string SparkSpriteName = "spark";

        /// <summary>The four flame frames sliced from <c>flames_sheet.png</c>. Each particle gets one at random.</summary>
        private static readonly string[] FlameFrameSpriteNames = { "flame_0", "flame_1", "flame_2", "flame_3" };

        /// <summary>Shown when the content fails to load, in place of a phase name.</summary>
        /// <remarks>
        /// The one thing this half draws, and only on the path where there is nothing else to say:
        /// a failed load never reaches <c>StartFlameCommand</c>, so the flame stays inactive and
        /// <see cref="PhoenixFlameViewSystem"/>, which owns the label otherwise, writes nothing.
        /// </remarks>
        private const string FailedLabel = "Load failed";
        private const string DemoName = "Phoenix Flame";
        private const int DemoIndex = 2;

        private EcsWorld _world;
        private ILogService _log;
        private AddressablesAssetService _assets;
        private ScreenRegistryService _screens;
        private StageState _state;
        private PhoenixFlameScreen _flameScreen;
        private Camera _camera;
        private Sprite[] _flameFrames;
        private Sprite _smokeSprite;
        private Sprite _sparkSprite;
        private Sprite _backgroundSprite;
        private bool _ownsBackgroundSprite;
        private int _atlasRequestId;
        private int _backgroundRequestId;
        private int _screenWidth = -1;
        private int _screenHeight = -1;

        public void Input()
        {
            // Not "_flameScreen != null": what must be torn down is this system's own state, and
            // that is what a non-Idle state says. The screen is a Unity object the scene unload can
            // destroy before this phase runs again — see AceOfShadowsInputSystem for the leak that
            // gating on it caused.
            if (_state != StageState.Idle && _state != StageState.Closing &&
                (_world.Get<ScreenStateComp>().Current == ScreenId.Unloading ||
                 _screens.TryGet<PhoenixFlameScreen>(out _) == false))
                _TransitionTo(StageState.Closing);

            switch (_state)
            {
                case StageState.Idle:
                    _BeginLoadingIfNeeded();
                    break;
                case StageState.Loading:
                    _ContinueLoading();
                    break;
                case StageState.Ready:
                    _RunReady();
                    break;
                case StageState.Closing:
                    _Teardown(true);
                    break;
            }
        }

        public void Destroy() => _Teardown(false);

        private void _BeginLoadingIfNeeded()
        {
            ref readonly var screen = ref _world.Get<ScreenStateComp>();

            if (_screens.TryGet(out PhoenixFlameScreen current) == false || current == _flameScreen ||
                screen.Current != ScreenId.Demo || screen.ActiveDemoIndex != DemoIndex)
                return;

            _flameScreen = current;
            _atlasRequestId = _assets.Request(new AssetLoadRequest(AtlasAddress));
            _backgroundRequestId = _assets.Request(new AssetLoadRequest(BackgroundAddress));
            _TransitionTo(StageState.Loading);
        }

        private void _ContinueLoading()
        {
            var atlasStatus = _assets.Poll(_atlasRequestId);
            var backgroundStatus = _assets.Poll(_backgroundRequestId);

            if (atlasStatus == AsyncOpStatus.Failed || backgroundStatus == AsyncOpStatus.Failed)
            {
                _log.Error("Phoenix Flame content load failed; retrying while the scene remains active.");
                _FailLoad();
                return;
            }

            if (atlasStatus != AsyncOpStatus.Done || backgroundStatus != AsyncOpStatus.Done)
                return;

            if (_ResolveContent() == false)
            {
                _FailLoad();
                return;
            }

            _flameScreen.Background.sprite = _backgroundSprite;
            // The screen is covered now, so the shell can hand over.
            _world.GetPool<DemoReadyTag>().Add(_world.NewEntity());
            _flameScreen.FlameColor.SetSprites(_flameFrames, _smokeSprite, _sparkSprite);
            _RecalculateLayout();
            // FlameSetupSystem takes this in the Sim phase, which is why there is no longer a
            // Starting state to wait in: the view half finds the flame already active.
            _world.GetPool<StartFlameCommand>().Add(_world.NewEntity());
            // Discard a press made during the load. The screen was not running yet.
            _flameScreen.AdvanceRequested = false;
            _TransitionTo(StageState.Ready);
        }

        private void _RunReady()
        {
            if (Screen.width != _screenWidth || Screen.height != _screenHeight)
                _RecalculateLayout();

            if (_flameScreen.AdvanceRequested == false)
                return;

            _flameScreen.AdvanceRequested = false;
            _world.GetPool<AdvanceFlamePhaseCommand>().Add(_world.NewEntity());
        }

        /// <summary>Shows the failure label and returns to <c>Idle</c>, which retries while the scene is open.</summary>
        private void _FailLoad()
        {
            _flameScreen.PhaseLabel.text = FailedLabel;
            _Teardown(false);
        }

        private bool _ResolveContent()
        {
            var atlasAsset = StageContent.GetAsset<SpriteAtlas>(_assets, _atlasRequestId);

            if (atlasAsset == null)
            {
                _log.Error("Phoenix Flame atlas address did not resolve to a SpriteAtlas.");
                return false;
            }

            _backgroundSprite = StageContent.ResolveBackground(
                _assets, _backgroundRequestId, DemoName, _log, out _ownsBackgroundSprite);

            if (_backgroundSprite == null)
                return false;

            // GetSprite returns a copy this system owns. Teardown destroys all of them.
            _flameFrames = new Sprite[FlameFrameSpriteNames.Length];
            var hasEveryFrame = true;

            for (var i = 0; i < FlameFrameSpriteNames.Length; i++)
            {
                _flameFrames[i] = atlasAsset.GetSprite(FlameFrameSpriteNames[i]);
                hasEveryFrame &= _flameFrames[i] != null;
            }

            _smokeSprite = atlasAsset.GetSprite(SmokeSpriteName);
            _sparkSprite = atlasAsset.GetSprite(SparkSpriteName);

            if (hasEveryFrame && _smokeSprite != null && _sparkSprite != null)
                return true;

            _log.Error("Phoenix Flame atlas is missing one of " +
                $"'{string.Join("', '", FlameFrameSpriteNames)}', '{SmokeSpriteName}' or '{SparkSpriteName}'.");
            return false;
        }

        private void _RecalculateLayout()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            _camera = StageContent.FitBackground(_camera, _flameScreen.Background.transform,
                _backgroundSprite, DemoName, _log, out _);
        }

        private void _Teardown(bool resetFlame)
        {
            if (_state == StageState.Idle && _flameScreen == null && _atlasRequestId == 0 &&
                _backgroundRequestId == 0)
                return;

            foreach (var readyEntity in _world.Where(out SingleTagAspect<DemoReadyTag> _))
                _world.DelEntity(readyEntity);

            if (resetFlame)
                _world.GetPool<ResetFlameCommand>().Add(_world.NewEntity());

            // Keep this order. The view must release its sprite references before you destroy them.
            if (_flameScreen != null)
            {
                _flameScreen.FlameColor.ClearSprites();
                _flameScreen.AdvanceRequested = false;
                _flameScreen.Background.sprite = null;
            }

            _DestroySpriteCopies();
            StageContent.DestroyOwnedSprite(ref _backgroundSprite, ref _ownsBackgroundSprite);
            _ReleaseRequests();

            _flameScreen = null;
            _camera = null;
            _screenWidth = -1;
            _screenHeight = -1;
            _TransitionTo(StageState.Idle);
        }

        private void _DestroySpriteCopies()
        {
            if (_flameFrames != null)
                foreach (var frame in _flameFrames)
                    if (frame != null)
                        Object.Destroy(frame);

            if (_smokeSprite != null)
                Object.Destroy(_smokeSprite);

            if (_sparkSprite != null)
                Object.Destroy(_sparkSprite);

            _flameFrames = null;
            _smokeSprite = null;
            _sparkSprite = null;
        }

        private void _ReleaseRequests()
        {
            _atlasRequestId = StageContent.Release(_assets, _atlasRequestId);
            _backgroundRequestId = StageContent.Release(_assets, _backgroundRequestId);
        }

        private void _TransitionTo(StageState next) => _state = next;

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILogService obj) => _log = obj;
        public void Inject(AddressablesAssetService obj) => _assets = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;
    }
}
