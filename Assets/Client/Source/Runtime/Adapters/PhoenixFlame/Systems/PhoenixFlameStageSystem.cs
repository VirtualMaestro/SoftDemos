using Client.Adapters.Services;
using Client.Adapters.Stage;
using Client.Simulation.Menu;
using Client.Simulation.PhoenixFlame;
using Client.Simulation.Ports;
using DCFApixels.DragonECS;
using UnityEngine;
using UnityEngine.U2D;

namespace Client.Adapters.PhoenixFlame
{
    /// <summary>Drives the flame Animator. Loads the content and mirrors <see cref="FlameStateComp"/> onto the view.</summary>
    /// <remarks>
    /// The <c>Starting</c> state exists because the start costs one frame. This system runs in
    /// <c>LateRun</c> and <c>FlameSetupSystem</c> in <c>Run</c>. Read the state only after
    /// <c>IsActive</c> is true.
    /// </remarks>
    public sealed class PhoenixFlameStageSystem : IEcsLateRun, IEcsDestroy,
        IEcsInject<EcsWorld>, IEcsInject<ILog>, IEcsInject<AddressablesAssetService>,
        IEcsInject<StageReadyChannel>, IEcsInject<ScreenRegistryService>
    {
        private const string AtlasAddress = "art/phoenix-flame/atlas";
        private const string BackgroundAddress = "art/phoenix-flame/background";
        private const string SmokeSpriteName = "smoke";
        private const string SparkSpriteName = "spark";

        /// <summary>The four flame frames sliced from <c>flames_sheet.png</c>. Each particle gets one at random.</summary>
        private static readonly string[] FlameFrameSpriteNames = { "flame_0", "flame_1", "flame_2", "flame_3" };
        private const string OrangeLabel = "Orange";
        private const string GreenLabel = "Green";
        private const string BlueLabel = "Blue";
        private const string FailedLabel = "Load failed";
        private const string OrangeTrigger = "ToOrange";
        private const string GreenTrigger = "ToGreen";
        private const string BlueTrigger = "ToBlue";

        /// <summary>The blend length on every <c>AnyState</c> transition in <c>PhoenixFlame.controller</c>, in seconds.</summary>
        /// <remarks>
        /// This must agree with <see cref="FlameStateComp.TransitionDurationSeconds"/>, which the
        /// simulation counts down. <see cref="_ContinueStarting"/> logs a mismatch.
        /// </remarks>
        private const float AuthoredTransitionSeconds = 1f;
        private const string DemoName = "Phoenix Flame";
        private const int DemoIndex = 2;

        // Hash once. CrossFadeInFixedTime takes a state hash, and hashing per frame allocates.
        // Static: the labels and the triggers are constants, so every instance would hash the same.
        private static readonly int[] PhaseHashes = _HashPerPhase(OrangeLabel, GreenLabel, BlueLabel);
        private static readonly int[] PhaseTriggers =
            _HashPerPhase(OrangeTrigger, GreenTrigger, BlueTrigger);

        private EcsWorld _world;
        private ILog _log;
        private AddressablesAssetService _assets;
        private StageReadyChannel _stageReady;
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
        private FlamePhase _shownPhase;
        private FlamePhase _shownLabelPhase = (FlamePhase)(-1);
        // Nullable so the first write always reaches the button. The scene starts it interactable.
        private bool? _shownInteractable;
        private bool _advanceRequested;

        /// <summary>Indexes three hashed names by <see cref="FlamePhase"/>.</summary>
        private static int[] _HashPerPhase(string orange, string green, string blue)
        {
            var hashes = new int[FlamePhaseCycle.Count];
            hashes[(int)FlamePhase.Orange] = Animator.StringToHash(orange);
            hashes[(int)FlamePhase.Green] = Animator.StringToHash(green);
            hashes[(int)FlamePhase.Blue] = Animator.StringToHash(blue);
            return hashes;
        }

        public void LateRun()
        {
            if (_flameScreen != null && _state != StageState.Closing &&
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
                case StageState.Starting:
                    _ContinueStarting();
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
            _flameScreen.OnAdvancePressed += _OnRequestAdvance;
            // Disable the button for the whole load. A tap must not queue an advance.
            _ApplyInteractable(false);
            _atlasRequestId = _assets.BeginLoad(AtlasAddress);
            _backgroundRequestId = _assets.BeginLoad(BackgroundAddress);
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
            // The screen is covered now, so the shell can hand over. Starting waits only for the
            // simulation to take StartFlameCommand, which changes nothing on screen.
            _stageReady.MarkDemoReady();
            _flameScreen.FlameColor.SetSprites(_flameFrames, _smokeSprite, _sparkSprite);
            _RecalculateLayout();
            _world.WriteCommand<StartFlameCommand>();
            _TransitionTo(StageState.Starting);
        }

        private void _ContinueStarting()
        {
            ref readonly var flame = ref _world.Get<FlameStateComp>();

            // The simulation consumes StartFlameCommand on its next Run, so this waits one frame.
            if (flame.IsActive == false)
                return;

            // Discard a press made during the load. The screen was not running yet.
            _advanceRequested = false;
            // Play the configured phase. The controller default state does not matter.
            _shownPhase = flame.CurrentPhase;
            // Use Play, not a trigger. The start phase must snap, and a trigger would blend.
            _ResetPhaseTriggers();
            _flameScreen.FlameAnimator.Play(PhaseHashes[(int)flame.CurrentPhase], 0, 0f);

            if (Mathf.Approximately(flame.TransitionDurationSeconds, AuthoredTransitionSeconds) == false)
                _log.Error($"The flame transition is {AuthoredTransitionSeconds}s in " +
                    $"PhoenixFlame.controller but {flame.TransitionDurationSeconds}s in the " +
                    "simulation; the phase label and the colour will disagree.");
            _ApplyInteractable(true);
            _ApplyLabel(flame.CurrentPhase);
            _TransitionTo(StageState.Ready);
        }

        private void _RunReady()
        {
            if (Screen.width != _screenWidth || Screen.height != _screenHeight)
                _RecalculateLayout();

            ref readonly var flame = ref _world.Get<FlameStateComp>();
            _DriveAnimator(in flame);
            _ApplyInteractable(flame.IsTransitioning == false);
            _ApplyLabel(flame.CurrentPhase);

            if (_advanceRequested == false)
                return;

            _advanceRequested = false;
            _world.WriteCommand<AdvanceFlamePhaseCommand>();
        }

        private void _DriveAnimator(in FlameStateComp flame)
        {
            if (flame.IsTransitioning == false || _shownPhase == flame.NextPhase)
                return;

            _shownPhase = flame.NextPhase;
            // Triggers latch. Clear the other two before you set the one you want.
            _ResetPhaseTriggers();
            _flameScreen.FlameAnimator.SetTrigger(PhaseTriggers[(int)flame.NextPhase]);
        }

        private void _ResetPhaseTriggers()
        {
            // `?.` skips Unity's null overload. Teardown runs while the scene closes.
            if (_flameScreen == null || _flameScreen.FlameAnimator == null)
                return;

            foreach (var trigger in PhaseTriggers)
                _flameScreen.FlameAnimator.ResetTrigger(trigger);
        }

        private void _ApplyInteractable(bool interactable)
        {
            if (_shownInteractable == interactable)
                return;

            _shownInteractable = interactable;
            _flameScreen.AdvanceButton.interactable = interactable;
        }

        private void _ApplyLabel(FlamePhase phase)
        {
            if (_shownLabelPhase == phase)
                return;

            _shownLabelPhase = phase;
            _flameScreen.PhaseLabel.text = _GetPhaseLabel(phase);
        }

        private static string _GetPhaseLabel(FlamePhase phase)
        {
            if (phase == FlamePhase.Green)
                return GreenLabel;

            if (phase == FlamePhase.Blue)
                return BlueLabel;

            return OrangeLabel;
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

            _stageReady.ClearDemo();

            if (resetFlame)
                _world.WriteCommand<ResetFlameCommand>();

            // Keep this order. The view must release its sprite references before you destroy them.
            // Clear the triggers too. The Animator survives a reopen and a latched trigger fires again.
            _ResetPhaseTriggers();

            if (_flameScreen != null)
            {
                _flameScreen.FlameColor.ClearSprites();
                _flameScreen.OnAdvancePressed -= _OnRequestAdvance;
                _flameScreen.Background.sprite = null;
            }

            _DestroySpriteCopies();
            StageContent.DestroyOwnedSprite(ref _backgroundSprite, ref _ownsBackgroundSprite);
            _ReleaseRequests();

            _flameScreen = null;
            _camera = null;
            _shownPhase = FlamePhase.Orange;
            _shownLabelPhase = (FlamePhase)(-1);
            _shownInteractable = null;
            _screenWidth = -1;
            _screenHeight = -1;
            _advanceRequested = false;
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

        private void _OnRequestAdvance() => _advanceRequested = true;

        private void _TransitionTo(StageState next) => _state = next;

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILog obj) => _log = obj;
        public void Inject(AddressablesAssetService obj) => _assets = obj;
        public void Inject(StageReadyChannel obj) => _stageReady = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;
    }
}
