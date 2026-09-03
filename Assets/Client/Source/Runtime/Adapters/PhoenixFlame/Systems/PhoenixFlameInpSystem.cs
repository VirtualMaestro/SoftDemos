using System.Runtime.CompilerServices;
using Client.Simulation.Core.Phases;
using Client.Adapters.PhoenixFlame.Views;
using Client.Adapters.Shared.Components;
using Client.Adapters.Shared.Services;
using Client.Adapters.Shared.Stage;
using Client.Simulation.PhoenixFlame.Components;
using Client.Simulation.Core.Navigation;
using Client.Simulation.Core.Navigation.Components;
using Client.Simulation.Core.Ports;
using Client.Simulation.Core.Ports.Requests;
using DCFApixels.DragonECS;
using UnityEngine;

namespace Client.Adapters.PhoenixFlame.Systems
{
    /// <summary>
    /// Runs the flame demo's screen lifecycle: loads atlas+background, hands the particle sprites
    /// to the view, drains the advance button into a command, tears everything down on close.
    /// </summary>
    /// <remarks>
    /// The Animator, the phase label and the button state moved to
    /// <see cref="PhoenixFlamePreSystem"/>, which is the half that reads the world and draws.
    /// What is left is what the Input phase is for: the port polling, the recorded press, and every
    /// write into the world.
    /// <para>It holds no engine object. The atlas, the background and the 6 sprites cut out of the
    /// atlas belong to <see cref="AddressablesAssetService"/> under ids of their own; the screen
    /// belongs to <see cref="ScreenRegistryService"/> and is resolved per call. The sprites are
    /// resolved once, at the hand-over to the view that shows them
    /// (adr-an-engine-object-has-one-owner-per-kind, DEU0146).</para>
    /// </remarks>
    internal sealed class PhoenixFlameInpSystem : IEcsInput, IEcsDestroy,
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
        /// <see cref="PhoenixFlamePreSystem"/>, which owns the label otherwise, writes nothing.
        /// </remarks>
        private const string FailedLabel = "Load failed";
        private const int DemoIndex = 2;

        /// <summary>Ids of the four flame frames, handed out by the asset service.</summary>
        private readonly int[] _flameFrameIds = new int[FlameFrameSpriteNames.Length];

        private EcsWorld _world;
        private ILogService _log;
        private AddressablesAssetService _assets;
        private ScreenRegistryService _screens;
        private StageState _state;

        /// <summary>
        /// The instance id of the screen this system opened on, so a reopened scene reads as a
        /// different screen without a reference to the old one being kept.
        /// </summary>
        private int _screenInstanceId;

        private int _smokeId;
        private int _sparkId;
        private int _backgroundId;
        private int _atlasRequestId;
        private int _backgroundRequestId;
        private int _screenWidth = -1;
        private int _screenHeight = -1;

        public void Input()
        {
            // Not "the screen is gone": what must be torn down is this system's own state, and
            // that is what a non-Idle state says. The screen is a Unity object the scene unload can
            // destroy before this phase runs again — see AceOfShadowsInpSystem for the leak that
            // gating on it caused.
            if (_state != StageState.Idle && _state != StageState.Closing &&
                (_world.Get<ScreenStateComp>().Current == ScreenId.Unloading ||
                 !_screens.TryGet<PhoenixFlameScreen>(out _)))
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

            if (!_screens.TryGet(out PhoenixFlameScreen current) ||
                current.GetInstanceID() == _screenInstanceId ||
                screen.Current != ScreenId.Demo || screen.ActiveDemoIndex != DemoIndex)
                return;

            _screenInstanceId = current.GetInstanceID();
            _atlasRequestId = _assets.Request(new AssetLoadRequest(AtlasAddress));
            _backgroundRequestId = _assets.Request(new AssetLoadRequest(BackgroundAddress));
            _TransitionTo(StageState.Loading);
        }

        private void _ContinueLoading()
        {
            if (!_screens.TryGet(out PhoenixFlameScreen screen))
                return;

            var atlasStatus = _assets.Poll(_atlasRequestId);
            var backgroundStatus = _assets.Poll(_backgroundRequestId);

            if (atlasStatus == AsyncOpStatus.Failed || backgroundStatus == AsyncOpStatus.Failed)
            {
                _log.Error("Phoenix Flame content load failed; retrying while the scene remains active.");
                _FailLoad(screen);
                return;
            }

            if (atlasStatus != AsyncOpStatus.Done || backgroundStatus != AsyncOpStatus.Done)
                return;

            if (!_ResolveContent())
            {
                _FailLoad(screen);
                return;
            }

            if (_assets.TryGetAsset(_backgroundId, out var background))
                screen.Background.sprite = background as Sprite;

            // The screen is covered now, so the shell can hand over.
            _world.GetPool<DemoReadyTag>().Add(_world.NewEntity());
            _HandSpritesToView(screen);
            _RecalculateLayout(screen);
            // FlameSetupSimSystem takes this in the Sim phase, which is why there is no longer a
            // Starting state to wait in: the view half finds the flame already active.
            _world.GetPool<StartFlameCommand>().Add(_world.NewEntity());
            // Discard a press made during the load. The screen was not running yet.
            screen.AdvanceRequested = false;
            _TransitionTo(StageState.Ready);
        }

        /// <summary>
        /// Resolves the 6 particle sprites and hands them to the view that shows them.
        /// </summary>
        /// <remarks>
        /// The view holds them for as long as it draws with them, which is what a view is for; the
        /// asset service still OWNS them and destroys them when the atlas is released, and the
        /// teardown tells the view to let go first. Nothing is resolved into a field here.
        /// </remarks>
        private void _HandSpritesToView(PhoenixFlameScreen screen)
        {
            var frames = new Sprite[_flameFrameIds.Length];

            for (var index = 0; index < _flameFrameIds.Length; index++)
                frames[index] = _Sprite(_flameFrameIds[index]);

            screen.FlameColor.SetSprites(frames, _Sprite(_smokeId), _Sprite(_sparkId));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _RunReady()
        {
            if (!_screens.TryGet(out PhoenixFlameScreen screen))
                return;

            if (Screen.width != _screenWidth || Screen.height != _screenHeight)
                _RecalculateLayout(screen);

            if (!screen.AdvanceRequested)
                return;

            screen.AdvanceRequested = false;
            _world.GetPool<AdvanceFlamePhaseCommand>().Add(_world.NewEntity());
        }

        /// <summary>Shows the failure label and returns to <c>Idle</c>, which retries while the scene is open.</summary>
        private void _FailLoad(PhoenixFlameScreen screen)
        {
            screen.PhaseLabel.text = FailedLabel;
            _Teardown(false);
        }

        private bool _ResolveContent()
        {
            _backgroundId = _assets.ResolveSprite(_backgroundRequestId);

            if (_backgroundId == 0)
                return false;

            // This system never reads the atlas' names, so it never asks whether the address is
            // one: a request that is not an atlas makes every cut below hand back 0, the service
            // names that cause in the log, and the guard at the end turns it into the one error
            // line this demo has always reported.
            //
            // Each cut runs once and the asset service owns the copy from then on; releasing the
            // atlas destroys all six, which is what _DestroySpriteCopies used to do by hand.
            var hasEveryFrame = true;

            for (var index = 0; index < FlameFrameSpriteNames.Length; index++)
            {
                _flameFrameIds[index] = _DeriveFromAtlas(FlameFrameSpriteNames[index]);
                hasEveryFrame &= _flameFrameIds[index] != 0;
            }

            _smokeId = _DeriveFromAtlas(SmokeSpriteName);
            _sparkId = _DeriveFromAtlas(SparkSpriteName);

            if (hasEveryFrame && _smokeId != 0 && _sparkId != 0)
                return true;

            _log.Error("Phoenix Flame atlas is missing one of " +
                $"'{string.Join("', '", FlameFrameSpriteNames)}', '{SmokeSpriteName}' or '{SparkSpriteName}'.");
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int _DeriveFromAtlas(string spriteName) =>
            _assets.DeriveSprite(_atlasRequestId, spriteName);

        /// <summary>The sprite an id names, resolved through its owner and kept by nobody here.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Sprite _Sprite(int requestId) =>
            _assets.TryGetAsset(requestId, out var asset) ? asset as Sprite : null;

        private void _RecalculateLayout(PhoenixFlameScreen screen)
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;

            _assets.TryGetAsset(_backgroundId, out var background);

            BackgroundFitter.CoverFit(screen.Background.transform, background as Sprite,
                screen.StageCamera, _screenWidth, _screenHeight);
        }

        private void _Teardown(bool resetFlame)
        {
            if (_state == StageState.Idle && _screenInstanceId == 0 && _atlasRequestId == 0 &&
                _backgroundRequestId == 0)
                return;

            foreach (var readyEntity in _world.Where(out SingleTagAspect<DemoReadyTag> _))
                _world.DelEntity(readyEntity);

            if (resetFlame)
                _world.GetPool<ResetFlameCommand>().Add(_world.NewEntity());

            // Keep this order. The view must release its sprite references before the owner
            // destroys them, which _ReleaseRequests below does by releasing the atlas.
            if (_screens.TryGet(out PhoenixFlameScreen screen))
            {
                screen.FlameColor.ClearSprites();
                screen.AdvanceRequested = false;
                screen.Background.sprite = null;
            }

            _ReleaseRequests();

            _smokeId = 0;
            _sparkId = 0;
            _backgroundId = 0;
            System.Array.Clear(_flameFrameIds, 0, _flameFrameIds.Length);
            _screenInstanceId = 0;
            _screenWidth = -1;
            _screenHeight = -1;
            _TransitionTo(StageState.Idle);
        }

        private void _ReleaseRequests()
        {
            _assets.Release(ref _atlasRequestId);
            _assets.Release(ref _backgroundRequestId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _TransitionTo(StageState next) => _state = next;

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILogService obj) => _log = obj;
        public void Inject(AddressablesAssetService obj) => _assets = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;
    }
}
