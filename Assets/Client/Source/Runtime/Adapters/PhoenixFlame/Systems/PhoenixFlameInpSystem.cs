using System.Runtime.CompilerServices;
using Client.Simulation.Core.Phases;
using Client.Adapters.PhoenixFlame.Components;
using Client.Adapters.PhoenixFlame.Views;
using Client.Adapters.Shared.Components;
using Client.Adapters.Shared.Services;
using Client.Adapters.Shared.Stage;
using Client.Simulation.PhoenixFlame.Components.Commands;
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
    /// <para>It owns nothing either. The stage's state and the two ids it holds open live in
    /// <see cref="PhoenixFlameStageComp"/>; the six sprite ids are locals of the hand-over, because
    /// nothing reads them afterwards; the last-drawn screen size is remembered and derived again
    /// (adr-data-placement-is-decided-on-three-axes rules 4 and 5). There is no
    /// <c>IEcsDestroy</c>: the asset service releases what it owns when the composition root
    /// disposes it.</para>
    /// </remarks>
    internal sealed class PhoenixFlameInpSystem : IEcsInput,
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

        /// <summary>No stage entity. <c>Idle</c> is recorded as the absence of one.</summary>
        private const int NoStage = 0;

        private EcsWorld _world;
        private ILogService _log;
        private AddressablesAssetService _assets;
        private ScreenRegistryService _screens;

        private int _screenWidth = -1;
        private int _screenHeight = -1;

        private EcsPool<ResetFlameCommand> _resetCommands;

        public void Input()
        {
            var stage = _world.Where(out SingleAspect<PhoenixFlameStageComp> stageAspect);
            var stageEntity = stage.Count > 0 ? stage[0] : NoStage;

            ref readonly var nav = ref _world.Get<ScreenStateComp>();
            var unloading = nav.Current == ScreenId.Unloading;
            var hasScreen = _screens.TryGet(out PhoenixFlameScreen screen);

            // Not "the screen is gone": what has to come down is the stage, and the navigation
            // state says whether this demo is still the one selected. The screen itself is a Unity
            // object the scene unload can destroy before this phase runs again — see
            // AceOfShadowsInpSystem for the leak that gating on it alone caused.
            var screenPresent = hasScreen && nav.Current == ScreenId.Demo &&
                                nav.ActiveDemoIndex == DemoIndex;

            if (stageEntity == NoStage)
            {
                // Idle has no component to read a state off, and no screen it has opened on yet.
                if (StageTransitions.Next(StageState.Idle, screenPresent, true, unloading,
                        AsyncOpStatus.Pending) == StageState.Loading)
                    _BeginLoading(screen);

                return;
            }

            var pool = stageAspect.pool;

            // Closing is never seen at the end of a frame: the exit and the teardown are one step,
            // exactly as they were when the guard at the top of this method wrote them by hand.
            while (true)
            {
                var comp = pool.Get(stageEntity);
                var screenChanged = hasScreen && screen.GetInstanceID() != comp.ScreenInstanceId;
                var next = StageTransitions.Next(comp.State, screenPresent, screenChanged,
                    unloading, _Poll(comp));

                if (next == comp.State)
                {
                    if (comp.State == StageState.Ready)
                        _RunReady(stageEntity, pool, screen);

                    return;
                }

                pool.Get(stageEntity).State = next;

                if (next == StageState.Closing)
                {
                    _resetCommands.Add(_world.NewEntity());
                    continue;
                }

                if (next == StageState.Ready && _HandOver(stageEntity, pool, screen))
                    return;

                // Only a load that failed lands here from Loading: the poll said Failed, or the
                // resolve did. Both show the label; only the first has a line of its own.
                if (comp.State == StageState.Loading)
                {
                    if (next == StageState.Idle)
                        _log.Error(
                            "Phoenix Flame content load failed; retrying while the scene remains active.");

                    if (screen != null)
                        screen.PhaseLabel.text = FailedLabel;
                }

                _Teardown(stageEntity, pool);
                return;
            }
        }

        /// <summary>The worst of the two content requests: a failure first, then a wait.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AsyncOpStatus _Poll(PhoenixFlameStageComp comp)
        {
            var atlas = _assets.Poll(comp.AtlasRequestId);
            var background = _assets.Poll(comp.BackgroundRequestId);

            if (atlas == AsyncOpStatus.Failed || background == AsyncOpStatus.Failed)
                return AsyncOpStatus.Failed;

            return atlas == AsyncOpStatus.Done && background == AsyncOpStatus.Done
                ? AsyncOpStatus.Done
                : AsyncOpStatus.Pending;
        }

        /// <summary>The <c>Idle -&gt; Loading</c> edge: the stage is born holding its requests.</summary>
        private void _BeginLoading(PhoenixFlameScreen screen)
        {
            var entity = _world.NewEntity();
            ref var comp = ref _world.GetPool<PhoenixFlameStageComp>().Add(entity);

            comp.State = StageState.Loading;
            comp.ScreenInstanceId = screen.GetInstanceID();
            comp.AtlasRequestId = _assets.Request(new AssetLoadRequest(AtlasAddress));
            comp.BackgroundRequestId = _assets.Request(new AssetLoadRequest(BackgroundAddress));
        }

        /// <summary>
        /// The <c>Loading -&gt; Ready</c> edge: resolve the content, dress the screen, hand the
        /// particle sprites over and start the flame. Reports whether the content resolved.
        /// </summary>
        private bool _HandOver(int stageEntity, EcsPool<PhoenixFlameStageComp> pool,
            PhoenixFlameScreen screen)
        {
            if (screen == null || !_ResolveContent(stageEntity, pool, screen))
                return false;

            if (_assets.TryGetAsset(pool.Get(stageEntity).BackgroundId, out var background))
                screen.Background.sprite = background as Sprite;

            // The screen is covered now, so the shell can hand over.
            _world.GetPool<DemoReadyTag>().Add(_world.NewEntity());
            _RecalculateLayout(stageEntity, pool, screen);
            // FlameSetupSimSystem takes this in the Sim phase, which is why there is no longer a
            // Starting state to wait in: the view half finds the flame already active.
            _world.GetPool<StartFlameCommand>().Add(_world.NewEntity());
            // Discard a press made during the load. The screen was not running yet.
            screen.AdvanceRequested = false;
            return true;
        }

        private void _RunReady(int stageEntity, EcsPool<PhoenixFlameStageComp> pool,
            PhoenixFlameScreen screen)
        {
            if (screen == null)
                return;

            if (Screen.width != _screenWidth || Screen.height != _screenHeight)
                _RecalculateLayout(stageEntity, pool, screen);

            if (!screen.AdvanceRequested)
                return;

            screen.AdvanceRequested = false;
            _world.GetPool<AdvanceFlamePhaseCommand>().Add(_world.NewEntity());
        }

        /// <summary>
        /// Resolves the background and the 6 particle sprites, and hands the particles to the view
        /// that shows them.
        /// </summary>
        /// <remarks>
        /// The view holds them for as long as it draws with them, which is what a view is for; the
        /// asset service still OWNS them and destroys them when the atlas is released, and the
        /// teardown tells the view to let go first. Nothing is resolved into a field here — the six
        /// ids are locals, because the hand-over is the only thing that ever reads them.
        /// </remarks>
        private bool _ResolveContent(int stageEntity, EcsPool<PhoenixFlameStageComp> pool,
            PhoenixFlameScreen screen)
        {
            var atlasRequestId = pool.Get(stageEntity).AtlasRequestId;
            ref var comp = ref pool.Get(stageEntity);
            comp.BackgroundId = _assets.ResolveSprite(comp.BackgroundRequestId);

            if (comp.BackgroundId == 0)
                return false;

            // This system never reads the atlas' names, so it never asks whether the address is
            // one: a request that is not an atlas makes every cut below hand back 0, the service
            // names that cause in the log, and the guard at the end turns it into the one error
            // line this demo has always reported.
            //
            // Each cut runs once and the asset service owns the copy from then on; releasing the
            // atlas destroys all six, which is what _DestroySpriteCopies used to do by hand.
            var frameIds = new int[FlameFrameSpriteNames.Length];
            var hasEveryFrame = true;

            for (var index = 0; index < FlameFrameSpriteNames.Length; index++)
            {
                frameIds[index] = _assets.DeriveSprite(atlasRequestId, FlameFrameSpriteNames[index]);
                hasEveryFrame &= frameIds[index] != 0;
            }

            var smokeId = _assets.DeriveSprite(atlasRequestId, SmokeSpriteName);
            var sparkId = _assets.DeriveSprite(atlasRequestId, SparkSpriteName);

            if (!hasEveryFrame || smokeId == 0 || sparkId == 0)
            {
                _log.Error("Phoenix Flame atlas is missing one of " +
                    $"'{string.Join("', '", FlameFrameSpriteNames)}', '{SmokeSpriteName}' or '{SparkSpriteName}'.");
                return false;
            }

            var frames = new Sprite[frameIds.Length];

            for (var index = 0; index < frameIds.Length; index++)
                frames[index] = _Sprite(frameIds[index]);

            screen.FlameColor.SetSprites(frames, _Sprite(smokeId), _Sprite(sparkId));
            return true;
        }

        /// <summary>The sprite an id names, resolved through its owner and kept by nobody here.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Sprite _Sprite(int requestId) =>
            _assets.TryGetAsset(requestId, out var asset) ? asset as Sprite : null;

        private void _RecalculateLayout(int stageEntity, EcsPool<PhoenixFlameStageComp> pool,
            PhoenixFlameScreen screen)
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;

            _assets.TryGetAsset(pool.Get(stageEntity).BackgroundId, out var background);

            BackgroundFitter.CoverFit(screen.Background.transform, background as Sprite,
                screen.StageCamera, _screenWidth, _screenHeight);
        }

        /// <summary>
        /// The edge back to <c>Idle</c>: hand everything back to its owner, then delete the stage.
        /// </summary>
        /// <remarks>
        /// No guard on "is there anything to tear down": there is a stage entity or there is not,
        /// which is the fact the four zeroed fields used to spell out. The entity goes LAST, after
        /// the ids it carries have been released — reading them off a deleted entity is what rule 7
        /// of adr-data-placement-is-decided-on-three-axes forbids.
        /// </remarks>
        private void _Teardown(int stageEntity, EcsPool<PhoenixFlameStageComp> pool)
        {
            foreach (var readyEntity in _world.Where(out SingleTagAspect<DemoReadyTag> _))
                _world.DelEntity(readyEntity);

            // Keep this order. The view must release its sprite references before the owner
            // destroys them, which releasing the atlas below does.
            if (_screens.TryGet(out PhoenixFlameScreen screen))
            {
                screen.FlameColor.ClearSprites();
                screen.AdvanceRequested = false;
                screen.Background.sprite = null;
            }

            _screenWidth = -1;
            _screenHeight = -1;

            ref var comp = ref pool.Get(stageEntity);
            _assets.Release(ref comp.AtlasRequestId);
            _assets.Release(ref comp.BackgroundRequestId);

            _world.DelEntity(stageEntity);
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _resetCommands = obj.GetPool<ResetFlameCommand>();
        }

        public void Inject(ILogService obj) => _log = obj;
        public void Inject(AddressablesAssetService obj) => _assets = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;
    }
}
