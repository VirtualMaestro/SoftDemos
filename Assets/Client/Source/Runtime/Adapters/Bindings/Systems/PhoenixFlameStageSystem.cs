using DCFApixels.DragonECS;
using Game.Adapters.Services;
using Game.Adapters.Views;
using Game.Simulation.Menu;
using Game.Simulation.PhoenixFlame;
using Game.Simulation.Ports;
using UnityEngine;
using UnityEngine.U2D;

namespace Game.Adapters.Bindings
{
    /// <summary>
    /// The only place the flame Animator is driven. It loads the Phoenix Flame content, starts and
    /// resets the simulation, turns button presses into <see cref="AdvanceFlamePhaseCommand"/> and
    /// mirrors <see cref="FlameStateComp"/> onto the Animator, the button and its label.
    ///
    /// The start costs a frame, which is why <c>Starting</c> exists: this system runs in
    /// <c>LateRun</c> and <c>FlameSetupSystem</c> in <c>Run</c>, so a command written here is
    /// consumed in the next frame's update and <see cref="FlameStateComp"/> is still
    /// <c>default</c> until then — reading <c>CurrentPhase</c> or
    /// <c>TransitionDurationSeconds</c> before <c>IsActive</c> turns true reads zeroes.
    /// </summary>
    public sealed class PhoenixFlameStageSystem : IEcsLateRun, IEcsDestroy,
        IEcsInject<EcsWorld>, IEcsInject<ILog>
    {
        private const string AtlasAddress = "art/phoenix-flame/atlas";
        private const string BackgroundAddress = "art/phoenix-flame/background";
        private const string SmokeSpriteName = "smoke";
        private const string SparkSpriteName = "spark";

        /// <summary>
        /// The 2x2 flame silhouette frames sliced from <c>flames_sheet.png</c>; the Texture Sheet
        /// Animation module on the flame system picks one at random per particle.
        /// </summary>
        private static readonly string[] FlameFrameSpriteNames =
            { "flame_0", "flame_1", "flame_2", "flame_3" };
        private const string OrangeLabel = "Orange";
        private const string GreenLabel = "Green";
        private const string BlueLabel = "Blue";
        private const string FailedLabel = "Load failed";
        private const string OrangeTrigger = "ToOrange";
        private const string GreenTrigger = "ToGreen";
        private const string BlueTrigger = "ToBlue";
        /// <summary>
        /// The blend length authored on every <c>AnyState</c> transition in
        /// <c>PhoenixFlame.controller</c>, in seconds.
        ///
        /// Once the graph owns the blend there are two numbers for one fact — this one and
        /// <see cref="FlameStateComp.TransitionDurationSeconds"/>, which is what the simulation
        /// counts down and what flips the phase label. Nothing makes them agree, so
        /// <see cref="_ContinueStarting"/> asserts it out loud rather than letting a silent
        /// disagreement show the new label over the old colour.
        /// </summary>
        private const float AuthoredTransitionSeconds = 1f;
        private const int PhoenixFlameDemoIndex = 2;
        // Starting waits for the simulation to consume StartFlameCommand, which normally costs one
        // frame. This is a deadlock guard, not a budget — it only fires when the command never lands.
        private const int StartTimeoutFrames = 300;

        private readonly AddressablesAssetService _assets;
        private readonly int[] _phaseHashes;
        private readonly int[] _phaseTriggers;

        private EcsWorld _world;
        private ILog _log;
        private StageState _state;
        private PhoenixFlameScreen _scene;
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
        // Nullable so the first write always reaches the button: the scene authors interactable=true
        // and a `false` seed would make the change check skip the write that switches it off.
        private bool? _shownInteractable;
        private bool _advanceRequested;
        private int _startFrames;
        private bool _loggedMissingCamera;
        private bool _loggedLoadFailure;

        public PhoenixFlameStageSystem(AddressablesAssetService assets)
        {
            _assets = assets;
            // Built once: CrossFadeInFixedTime takes a state hash, and hashing three strings per
            // frame would be an allocation on the presentation path.
            _phaseHashes = new int[FlamePhaseCycle.Count];
            _phaseHashes[(int)FlamePhase.Orange] = Animator.StringToHash(OrangeLabel);
            _phaseHashes[(int)FlamePhase.Green] = Animator.StringToHash(GreenLabel);
            _phaseHashes[(int)FlamePhase.Blue] = Animator.StringToHash(BlueLabel);

            _phaseTriggers = new int[FlamePhaseCycle.Count];
            _phaseTriggers[(int)FlamePhase.Orange] = Animator.StringToHash(OrangeTrigger);
            _phaseTriggers[(int)FlamePhase.Green] = Animator.StringToHash(GreenTrigger);
            _phaseTriggers[(int)FlamePhase.Blue] = Animator.StringToHash(BlueTrigger);
        }

        public void LateRun()
        {
            if (_scene != null && _state != StageState.Closing &&
                (_world.Get<ScreenStateComp>().Current == ScreenId.Unloading ||
                 PhoenixFlameScreen.Current == null))
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
            var current = PhoenixFlameScreen.Current;
            ref readonly var screen = ref _world.Get<ScreenStateComp>();

            if (current == null || current == _scene || screen.Current != ScreenId.Demo ||
                screen.ActiveDemoIndex != PhoenixFlameDemoIndex)
                return;

            _scene = current;
            _scene.OnAdvancePressed += _OnRequestAdvance;
            // The scene ships the button enabled; switch it off for the whole load so a tap cannot
            // queue an advance that would fire the moment the flame starts.
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
                _FailLoad("Phoenix Flame content load failed; retrying while the scene remains active.");
                return;
            }

            if (atlasStatus != AsyncOpStatus.Done || backgroundStatus != AsyncOpStatus.Done)
                return;

            if (_ResolveContent() == false)
            {
                _FailLoad("Phoenix Flame content did not resolve; retrying while the scene remains active.");
                return;
            }

            _loggedLoadFailure = false;
            _scene.Background.sprite = _backgroundSprite;
            _scene.FlameColor.SetSprites(_flameFrames, _smokeSprite, _sparkSprite);
            _RecalculateLayout();
            _WriteCommand<StartFlameCommand>();
            _log.Info("Phoenix Flame content loaded.");
            _TransitionTo(StageState.Starting);
        }

        private void _ContinueStarting()
        {
            ref readonly var flame = ref _world.Get<FlameStateComp>();

            if (flame.IsActive == false)
            {
                if (++_startFrames < StartTimeoutFrames)
                    return;

                _log.Error($"Phoenix Flame did not become active within {StartTimeoutFrames} frames; " +
                    "closing the stage.");
                _TransitionTo(StageState.Closing);
                return;
            }

            // A press landed during the load is never replayed — the user asked for a step on a
            // screen that had not started yet.
            _advanceRequested = false;
            // Play the configured phase explicitly. The controller's own default state is never
            // load-bearing, so changing PhoenixFlameConfig.StartPhase cannot desync the screen.
            _shownPhase = flame.CurrentPhase;
            // Still a Play, not a trigger: the start phase must snap. A trigger would route through
            // the AnyState transition and blend into it from whatever the graph happens to sit on.
            _ResetPhaseTriggers();
            _scene.FlameAnimator.Play(_phaseHashes[(int)flame.CurrentPhase], 0, 0f);

            if (Mathf.Approximately(flame.TransitionDurationSeconds, AuthoredTransitionSeconds) == false)
                _log.Error($"The flame transition is {AuthoredTransitionSeconds}s in " +
                    $"PhoenixFlame.controller but {flame.TransitionDurationSeconds}s in the " +
                    "simulation; the phase label and the colour will disagree.");
            _ApplyInteractable(true);
            _ApplyLabel(flame.CurrentPhase);
            _log.Info($"Phoenix Flame started in {flame.CurrentPhase}.");
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
            _WriteCommand<AdvanceFlamePhaseCommand>();
        }

        private void _DriveAnimator(in FlameStateComp flame)
        {
            if (flame.IsTransitioning == false || _shownPhase == flame.NextPhase)
                return;

            _log.Info($"Flame animator triggering {flame.CurrentPhase} -> {flame.NextPhase} " +
                $"over {AuthoredTransitionSeconds}s.");
            _shownPhase = flame.NextPhase;
            // Triggers latch: one left set from an earlier phase would fire the moment any other
            // transition finished, so the two that are not wanted are cleared before the one that is.
            _ResetPhaseTriggers();
            _scene.FlameAnimator.SetTrigger(_phaseTriggers[(int)flame.NextPhase]);
        }

        private void _ResetPhaseTriggers()
        {
            // `?.` bypasses Unity's null overload, and teardown runs while the scene is going away.
            if (_scene == null || _scene.FlameAnimator == null)
                return;

            foreach (var trigger in _phaseTriggers)
                _scene.FlameAnimator.ResetTrigger(trigger);
        }

        private void _ApplyInteractable(bool interactable)
        {
            if (_shownInteractable == interactable)
                return;

            _shownInteractable = interactable;
            _scene.AdvanceButton.interactable = interactable;
        }

        private void _ApplyLabel(FlamePhase phase)
        {
            if (_shownLabelPhase == phase)
                return;

            _shownLabelPhase = phase;
            _scene.PhaseLabel.text = _GetPhaseLabel(phase);
        }

        private static string _GetPhaseLabel(FlamePhase phase)
        {
            if (phase == FlamePhase.Green)
                return GreenLabel;

            if (phase == FlamePhase.Blue)
                return BlueLabel;

            return OrangeLabel;
        }

        /// <summary>
        /// Reports a failed load once per visit and tells the user on screen, then drops back to
        /// <c>Idle</c> so the next frame retries while the scene is still open.
        /// </summary>
        private void _FailLoad(string message)
        {
            _LogLoadFailureOnce(message);

            if (_scene != null)
                _scene.PhaseLabel.text = FailedLabel;

            _Teardown(false);
        }

        private void _LogLoadFailureOnce(string message)
        {
            if (_loggedLoadFailure)
                return;

            _loggedLoadFailure = true;
            _log.Error(message);
        }

        private bool _ResolveContent()
        {
            var atlasAsset = _GetAsset<SpriteAtlas>(_atlasRequestId);
            var backgroundHandle = _assets.ResolveHandle(_backgroundRequestId);

            if (atlasAsset == null ||
                _assets.TryGetAsset(backgroundHandle, out var backgroundAsset) == false)
            {
                _LogLoadFailureOnce("Phoenix Flame addresses did not resolve to a SpriteAtlas and a background.");
                return false;
            }

            if (backgroundAsset is Sprite background)
                _backgroundSprite = background;
            else if (backgroundAsset is Texture2D texture)
            {
                _backgroundSprite = Sprite.Create(texture,
                    new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 150f);
                _backgroundSprite.name = texture.name;
                _ownsBackgroundSprite = true;
            }
            else
            {
                _LogLoadFailureOnce($"Phoenix Flame background resolved as {backgroundAsset.GetType().Name}, " +
                    "expected Sprite or Texture2D.");
                return false;
            }

            // GetSprite hands back a copy this system owns; teardown destroys every one of them.
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

            _LogLoadFailureOnce("Phoenix Flame atlas is missing one of " +
                $"'{string.Join("', '", FlameFrameSpriteNames)}', '{SmokeSpriteName}' or '{SparkSpriteName}'.");
            return false;
        }

        private T _GetAsset<T>(int requestId) where T : UnityEngine.Object
        {
            var handleId = _assets.ResolveHandle(requestId);
            return _assets.TryGetAsset(handleId, out var asset) ? asset as T : null;
        }

        private void _RecalculateLayout()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;

            if (_camera == null)
                _camera = Camera.main;

            var orthographicSize = 5f;

            if (_camera != null)
                orthographicSize = _camera.orthographicSize;
            else if (_loggedMissingCamera == false)
            {
                _loggedMissingCamera = true;
                _log.Error("No MainCamera found for Phoenix Flame layout; using orthographic size 5.");
            }

            if (_scene != null)
                BackgroundFitter.CoverFit(_scene.Background.transform, _backgroundSprite, _camera,
                    orthographicSize, _screenWidth, _screenHeight);
            var mode = _screenWidth < _screenHeight ? "portrait" : "landscape";
            _log.Info($"Phoenix Flame layout recalculated for {_screenWidth}×{_screenHeight} ({mode}).");
        }

        private void _Teardown(bool resetFlame)
        {
            if (_state == StageState.Idle && _scene == null && _atlasRequestId == 0 &&
                _backgroundRequestId == 0)
                return;

            if (resetFlame)
                _WriteCommand<ResetFlameCommand>();

            // Order is load-bearing: the view drops the module and property-block references first,
            // and only then may the sprite copies be destroyed. The other way round leaves the
            // particle systems pointing at destroyed sprites.
            // Triggers latch, and the Animator outlives one visit to the demo when the scene is
            // reopened — a set trigger left behind fires on the next Play.
            _ResetPhaseTriggers();

            if (_scene != null)
            {
                _scene.FlameColor.ClearSprites();
                _scene.OnAdvancePressed -= _OnRequestAdvance;
                _scene.Background.sprite = null;
            }

            _DestroySpriteCopies();
            _DestroyBackgroundCopy();
            _ReleaseRequests();

            _scene = null;
            _camera = null;
            _shownPhase = FlamePhase.Orange;
            _shownLabelPhase = (FlamePhase)(-1);
            _shownInteractable = null;
            _screenWidth = -1;
            _screenHeight = -1;
            _advanceRequested = false;
            _startFrames = 0;
            _loggedMissingCamera = false;
            // Only a real close clears the once-flag; the retry path keeps it so a permanently
            // failing address logs one Error per visit instead of one per frame.
            if (resetFlame)
                _loggedLoadFailure = false;

            _TransitionTo(StageState.Idle);
        }

        private void _DestroySpriteCopies()
        {
            if (_flameFrames != null)
                foreach (var frame in _flameFrames)
                    if (frame != null)
                        UnityEngine.Object.Destroy(frame);

            if (_smokeSprite != null)
                UnityEngine.Object.Destroy(_smokeSprite);

            if (_sparkSprite != null)
                UnityEngine.Object.Destroy(_sparkSprite);

            _flameFrames = null;
            _smokeSprite = null;
            _sparkSprite = null;
        }

        private void _DestroyBackgroundCopy()
        {
            if (_ownsBackgroundSprite && _backgroundSprite != null)
                UnityEngine.Object.Destroy(_backgroundSprite);

            _ownsBackgroundSprite = false;
            _backgroundSprite = null;
        }

        private void _ReleaseRequests()
        {
            if (_atlasRequestId != 0)
                _assets.Release(_atlasRequestId);

            if (_backgroundRequestId != 0)
                _assets.Release(_backgroundRequestId);
            _atlasRequestId = 0;
            _backgroundRequestId = 0;
        }

        private void _OnRequestAdvance() => _advanceRequested = true;

        private void _WriteCommand<T>() where T : struct, IEcsComponent
        {
            var entityId = _world.NewEntity();
            _world.GetPool<T>().Add(entityId);
        }

        private void _TransitionTo(StageState next)
        {
            if (_state == next)
                return;

            _log.Info($"Phoenix Flame stage: {_state} -> {next}.");
            _state = next;
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILog obj) => _log = obj;

        private enum StageState
        {
            Idle,
            Loading,
            Starting,
            Ready,
            Closing,
        }
    }
}
