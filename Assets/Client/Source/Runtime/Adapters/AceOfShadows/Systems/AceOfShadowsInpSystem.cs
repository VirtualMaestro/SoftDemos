using System;
using Client.Simulation.Core.Phases;
using Client.Adapters.AceOfShadows.Components;
using Client.Adapters.AceOfShadows.Components.Events;
using Client.Adapters.AceOfShadows.Services;
using Client.Adapters.AceOfShadows.Views;
using Client.Adapters.Shared.Components;
using Client.Adapters.Shared.Services;
using Client.Adapters.Shared.Stage;
using Client.Simulation.AceOfShadows;
using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.AceOfShadows.Components.Commands;
using Client.Simulation.Core.Components.Commands;
using Client.Simulation.Core.Navigation;
using Client.Simulation.Core.Navigation.Components;
using Client.Simulation.Core.Ports;
using Client.Simulation.Core.Ports.Requests;
using DCFApixels.DragonECS;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.AceOfShadows.Systems
{
    /// <summary>
    /// Runs the card demo's screen lifecycle: loads atlas+background, spawns the card view pool,
    /// binds a view to every card the simulation dealt, drains the speed button and the finished
    /// tweens into commands, and tears everything down on close.
    /// </summary>
    /// <remarks>
    /// The demo's drawing lives in <see cref="CardBindingPreSystem"/>, <see cref="DeckHudPreSystem"/>
    /// and <see cref="TweenPlaybackPreSystem"/>: they are the half that reads the world and paints it,
    /// so this system keeps the decisions. What is here is what the Input phase is for — the port
    /// polling, the recorded press, the tween player's completion queue, and every write into the
    /// world.
    /// <para>It holds no engine object. The atlas, the background and the 14 sprites cut from the
    /// atlas belong to <see cref="AddressablesAssetService"/> under ids of their own; the card views
    /// belong to <see cref="ViewRegistryService"/> under handles; the screen belongs to
    /// <see cref="ScreenRegistryService"/> and is resolved per call
    /// (adr-an-engine-object-has-one-owner-per-kind, DEU0146).</para>
    /// <para>It owns nothing either. The stage's state, its ids and its two cursors live in
    /// <see cref="AceOfShadowsStageComp"/>, and the face table in <see cref="CardArtComp"/>; what
    /// stays here is config, two pools and the last-drawn screen size, all of which the next call
    /// can derive again (adr-data-placement-is-decided-on-three-axes rules 4 and 5). There is no
    /// <c>IEcsDestroy</c>: the asset service, the view registry and the tween player release what
    /// they own when the composition root disposes them.</para>
    /// <para>Binding lives here rather than in <see cref="CardBindingPreSystem"/> because the face
    /// a card shows is chosen by the BIND ORDER — <c>BoundCount % faces</c> — and that is a world
    /// write. The Present half keeps what it can answer from the world alone: the seating and the
    /// sorting order.</para>
    /// </remarks>
    internal sealed class AceOfShadowsInpSystem : IEcsInput,
        IEcsInject<EcsWorld>, IEcsInject<ILogService>, IEcsInject<ViewRegistryService>,
        IEcsInject<StackSlotLayoutService>, IEcsInject<AddressablesAssetService>,
        IEcsInject<CardMovePlayerService>, IEcsInject<ScreenRegistryService>
    {
        private const string AtlasAddress = "art/ace-of-shadows/atlas";
        private const string BackgroundAddress = "art/ace-of-shadows/background";
        private const string BackSpriteName = "card-back";
        private const int DemoIndex = 0;
        private const int FaceCount = 13;
        private const int SpawnPerFrame = 24;

        /// <summary>No stage entity. <c>Idle</c> is recorded as the absence of one.</summary>
        private const int NoStage = 0;

        /// <summary>Speed multipliers the button cycles through, starting at ×1.</summary>
        private static readonly float[] SpeedCycle = { 1f, 4f, 8f };

        private readonly AceOfShadowsConfig _config;

        private EcsWorld _world;
        private ILogService _log;
        private ViewRegistryService _views;
        private StackSlotLayoutService _layout;
        private AddressablesAssetService _assets;
        private CardMovePlayerService _cardMovePlayer;
        private ScreenRegistryService _screens;

        private int _screenWidth = -1;
        private int _screenHeight = -1;

        /// <summary>
        /// Log-once latch for the out-of-views warning. A remembered value and not an owned one:
        /// a fresh instance mid-frame would log the line once more, which is all that is lost.
        /// <c>UnityLogService</c> passes straight through to <c>Debug.Log*</c> and deduplicates
        /// nothing, so the latch is what keeps one bad bind from filling the console.
        /// </summary>
        private bool _warnedOutOfViews;

        private EcsTagPool<MoveCompletedCommand> _completedMoves;
        private EcsTagPool<TweenRunningTag> _runningTweens;
        private EcsPool<ResetDeckCommand> _resetCommands;
        private EcsPool<DealDeckCommand> _dealCommands;

        public AceOfShadowsInpSystem(AceOfShadowsConfig config)
        {
            _config = config;
        }

        public void Input()
        {
            _DrainCompletedTweens();

            var stage = _world.Where(out SingleAspect<AceOfShadowsStageComp> stageAspect);
            var stageEntity = stage.Count > 0 ? stage[0] : NoStage;

            ref readonly var nav = ref _world.Get<ScreenStateComp>();
            var unloading = nav.Current == ScreenId.Unloading;
            var hasScreen = _screens.TryGet(out AceOfShadowsScreen screen);

            // Not "the screen is gone": what has to come down is the stage — the requests, the ids,
            // the view pool — and the navigation state says whether this demo is still selected.
            // The screen itself is a Unity object the scene unload can destroy before this phase
            // runs again, and gating teardown on it alone leaked every request on those frames.
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
            var exiting = unloading || !screenPresent;

            // Closing is never seen at the end of a frame: the exit and the teardown are one step,
            // exactly as they were when the guard at the top of this method wrote them by hand.
            while (true)
            {
                var comp = pool.Get(stageEntity);
                var screenChanged = hasScreen && screen.GetInstanceID() != comp.ScreenInstanceId;

                // The exit wins, so nothing is loaded or spawned on the frame the stage comes down.
                var load = !exiting && comp.State == StageState.Loading
                    ? _AdvanceLoading(stageEntity, pool, screen)
                    : AsyncOpStatus.Pending;

                var next = StageTransitions.Next(comp.State, screenPresent, screenChanged,
                    unloading, load);

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

                if (next == StageState.Ready)
                {
                    _dealCommands.Add(_world.NewEntity());
                    return;
                }

                if (comp.State == StageState.Loading)
                    _log.Error("Ace of Shadows content load failed. Retrying while the scene is open.");

                _Teardown(stageEntity, pool);
                return;
            }
        }

        /// <summary>Turns the tween player's finished flights into <c>MoveCompletedCommand</c>s.</summary>
        /// <remarks>
        /// The completion happens on a DOTween callback, at whatever point in the frame the tween
        /// ends; the player queues the entity and touches nothing else. This is where that leaves
        /// the outside world and enters the simulation's, which is the definition of the Input
        /// phase — and the reason <see cref="TweenPlaybackPreSystem"/>, which draws, cannot do it: a
        /// one-frame component produced in Present is deleted unread, which <c>DEU0131</c> reports.
        /// <para>The cost is one frame of lag on a landing, and it is the last one in the project.
        /// It is a property of where the completion enters, not of where somebody put an
        /// <c>Add</c> call in the builder.</para>
        /// </remarks>
        private void _DrainCompletedTweens()
        {
            if (!_cardMovePlayer.HasCompletedTweens)
                return;

            foreach (var completion in _cardMovePlayer.Completions)
            {
                // The entity can die while its tween runs. entlong holds a generation, so a
                // recycled id reads as dead.
                if (!completion.TryGetID(out var entityId))
                {
                    _log.Warn("A tween completed for an entity that no longer exists. Ignoring.");
                    continue;
                }

                _runningTweens.TryDel(entityId);
                _completedMoves.TryAdd(entityId);
            }

            _cardMovePlayer.ClearCompletions();
        }

        /// <summary>The <c>Idle -&gt; Loading</c> edge: the stage is born holding its requests.</summary>
        private void _BeginLoading(AceOfShadowsScreen screen)
        {
            var entity = _world.NewEntity();
            ref var comp = ref _world.GetPool<AceOfShadowsStageComp>().Add(entity);

            comp.State = StageState.Loading;
            comp.ScreenInstanceId = screen.GetInstanceID();
            comp.AtlasRequestId = _assets.Request(new AssetLoadRequest(AtlasAddress));
            comp.BackgroundRequestId = _assets.Request(new AssetLoadRequest(BackgroundAddress));
        }

        /// <summary>
        /// Moves the load one frame on and reports where it stands: <c>Failed</c> when the content
        /// will not come, <c>Done</c> when the content resolved AND the whole view pool is spawned,
        /// <c>Pending</c> otherwise.
        /// </summary>
        /// <remarks>
        /// The demo becomes visible in the MIDDLE of this state, not at its end: the background,
        /// the skin and <c>DemoReadyTag</c> land as soon as the content resolves, and the cards
        /// arrive over the next few frames on top of them. What waits for the end is the deal.
        /// </remarks>
        private AsyncOpStatus _AdvanceLoading(int stageEntity,
            EcsPool<AceOfShadowsStageComp> pool, AceOfShadowsScreen screen)
        {
            // The content is resolved exactly when the background id is set: one home for one fact.
            if (pool.Get(stageEntity).BackgroundId == 0)
            {
                var comp = pool.Get(stageEntity);
                var atlasStatus = _assets.Poll(comp.AtlasRequestId);
                var backgroundStatus = _assets.Poll(comp.BackgroundRequestId);

                if (atlasStatus == AsyncOpStatus.Failed || backgroundStatus == AsyncOpStatus.Failed)
                    return AsyncOpStatus.Failed;

                if (atlasStatus != AsyncOpStatus.Done || backgroundStatus != AsyncOpStatus.Done)
                    return AsyncOpStatus.Pending;

                if (!_ResolveContent(stageEntity, pool))
                    return AsyncOpStatus.Failed;

                if (_assets.TryGetAsset(pool.Get(stageEntity).BackgroundId, out var background))
                    screen.Background.sprite = background as Sprite;

                // The screen is covered now, so the shell can hand over. The cards still arrive
                // over the next few frames, on top of the background.
                _world.GetPool<DemoReadyTag>().Add(_world.NewEntity());
                _SkinSpeedButton(screen);
                _RecalculateLayout(stageEntity, pool, screen);
            }

            var spawned = pool.Get(stageEntity).SpawnedCount;
            var spawnCount = Mathf.Min(SpawnPerFrame, _config.CardCount - spawned);

            for (var index = 0; index < spawnCount; index++)
            {
                var cardView = UnityEngine.Object.Instantiate(screen.CardPrefab, screen.CardRoot);
                cardView.name = $"Card {spawned + index:000}";
                var handle = _views.Register(cardView.transform, cardView);

                // The registry hands out handles in order and this system is its only caller, so
                // one open's views are one contiguous range. Only the first has to be recorded.
                ref var comp = ref pool.Get(stageEntity);

                if (comp.SpawnedCount == 0)
                    comp.FirstHandle = handle;

                comp.SpawnedCount++;
            }

            return pool.Get(stageEntity).SpawnedCount == _config.CardCount
                ? AsyncOpStatus.Done
                : AsyncOpStatus.Pending;
        }

        /// <summary>Skins the speed button with the shell's shared <c>ui-button</c> sprite.</summary>
        /// <remarks>
        /// The sprite is resolved through its owner, not loaded here. A second request on the shared
        /// atlas would make a second copy of the same pixels, and the shell already derived this one
        /// under the id <c>ShellSkinComp</c> carries. Does nothing if the shell has not loaded yet.
        /// </remarks>
        private void _SkinSpeedButton(AceOfShadowsScreen screen)
        {
            var image = screen.SpeedButtonImage;

            if (image == null)
                return;

            var skinId = _world.Get<ShellSkinComp>().Button;

            if (skinId == 0 || !_assets.TryGetAsset(skinId, out var asset) || asset is not Sprite button)
                return;

            image.sprite = button;
            image.type = Image.Type.Sliced;
        }

        private void _RunReady(int stageEntity, EcsPool<AceOfShadowsStageComp> pool,
            AceOfShadowsScreen screen)
        {
            if (screen == null)
                return;

            if (Screen.width != _screenWidth || Screen.height != _screenHeight)
                _RecalculateLayout(stageEntity, pool, screen);

            _BindUnboundCards(stageEntity, pool);

            if (!screen.SpeedRequested)
                return;

            screen.SpeedRequested = false;
            var speedIndex = (pool.Get(stageEntity).SpeedIndex + 1) % SpeedCycle.Length;
            var multiplier = SpeedCycle[speedIndex];
            var commandEntity = _world.NewEntity();
            _world.GetPool<SetDeckSpeedCommand>().Add(commandEntity).Multiplier = multiplier;
            pool.Get(stageEntity).SpeedIndex = speedIndex;
        }

        /// <summary>
        /// Gives every card the simulation dealt one of the pooled views, in spawn order.
        /// </summary>
        /// <remarks>
        /// The face is <c>BoundCount % FaceCount</c> — a property of the BIND order and never of the
        /// entity, which is why the cursor is world data and this is the half that writes it. Both
        /// sprites are resolved from the asset service on the call that configures the view, and
        /// neither is kept afterwards.
        /// </remarks>
        private void _BindUnboundCards(int stageEntity, EcsPool<AceOfShadowsStageComp> pool)
        {
            // The face table is written once when the content resolves; nothing binds before it.
            if (_world.Get<CardArtComp>().Faces == null)
                return;

            foreach (var entityId in _world.Where(out UnboundAspect aspect))
            {
                var comp = pool.Get(stageEntity);

                if (comp.BoundCount >= comp.SpawnedCount)
                {
                    if (!_warnedOutOfViews)
                    {
                        _warnedOutOfViews = true;
                        _log.Warn(
                            $"Ace of Shadows ran out of views after {comp.BoundCount} binding(s).");
                    }

                    return;
                }

                var handleId = comp.FirstHandle + comp.BoundCount;

                if (!_views.TryResolve(handleId, out var viewTransform, out var cardView) ||
                    cardView == null)
                {
                    _log.Warn($"Card view handle #{handleId} does not resolve. Skipping the bind.");
                    pool.Get(stageEntity).BoundCount++;
                    continue;
                }

                ref readonly var card = ref aspect.Cards.Read(entityId);

                if (_assets.TryGetAsset(_world.Get<CardArtComp>().Back, out var back) &&
                    _assets.TryGetAsset(
                        _world.Get<CardArtComp>().Faces[comp.BoundCount % FaceCount], out var face))
                    cardView.Configure(back as Sprite, face as Sprite);

                cardView.ResetToBack();
                viewTransform.position = _layout.SlotPosition(card.StackIndex, card.OrderInStack);
                cardView.SetSortingOrder(card.OrderInStack);
                aspect.Views.Add(entityId).Id = handleId;
                aspect.Seated.TryAdd(entityId);
                pool.Get(stageEntity).BoundCount++;
            }
        }

        /// <summary>
        /// Resolves the atlas, cuts the 14 card sprites out of it under ids of their own, and
        /// resolves the background to the id it is served under.
        /// </summary>
        /// <remarks>
        /// The atlas is read ONCE per open, for its NAMES — the asset service enumerates it and
        /// destroys the clones the engine minted to answer — and the copies that LIVE are cut one
        /// at a time through the same owner. It destroys them when the atlas request is released.
        /// Nothing engine-typed survives this method.
        /// </remarks>
        private bool _ResolveContent(int stageEntity, EcsPool<AceOfShadowsStageComp> pool)
        {
            var names = _assets.ReadAtlasNames(pool.Get(stageEntity).AtlasRequestId);

            if (names == null)
                return false;

            {
                ref var comp = ref pool.Get(stageEntity);
                comp.BackgroundId = _assets.ResolveSprite(comp.BackgroundRequestId);

                if (comp.BackgroundId == 0)
                    return false;
            }

            if (names.Length != FaceCount + 1)
            {
                _log.Error($"Ace of Shadows atlas has {names.Length} sprite(s); expected {FaceCount + 1}.");
                return false;
            }

            // Sorted by name, so the faces keep the order the demo has always dealt them in.
            Array.Sort(names, StringComparer.Ordinal);

            var atlasRequestId = pool.Get(stageEntity).AtlasRequestId;
            var faces = new int[FaceCount];
            var back = 0;
            var faceIndex = 0;

            foreach (var spriteName in names)
                if (spriteName == BackSpriteName)
                    back = _assets.DeriveSprite(atlasRequestId, spriteName);
                else if (faceIndex < faces.Length)
                    faces[faceIndex++] = _assets.DeriveSprite(atlasRequestId, spriteName);

            if (back == 0 || faceIndex != FaceCount || Array.IndexOf(faces, 0) >= 0)
            {
                _log.Error($"Ace of Shadows atlas is missing '{BackSpriteName}' or one of {FaceCount} faces.");
                return false;
            }

            ref var art = ref _world.Get<CardArtComp>();
            art.Back = back;
            art.Faces = faces;
            return true;
        }

        private void _RecalculateLayout(int stageEntity, EcsPool<AceOfShadowsStageComp> pool,
            AceOfShadowsScreen screen)
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;

            _assets.TryGetAsset(pool.Get(stageEntity).BackgroundId, out var background);

            var orthographicSize = BackgroundFitter.CoverFit(screen.Background.transform,
                background as Sprite, screen.StageCamera, _screenWidth, _screenHeight);

            _layout.Recalculate(_screenWidth, _screenHeight, orthographicSize);
            _world.GetPool<LayoutChangedEvent>().Add(_world.NewEntity());
        }

        /// <summary>
        /// The edge back to <c>Idle</c>: hand everything back to its owner, then delete the stage.
        /// </summary>
        /// <remarks>
        /// No guard on "is there anything to tear down": there is a stage entity or there is not,
        /// which is the fact the four zeroed fields used to spell out. The entity goes LAST, after
        /// the ids and the handles it carries have been released — reading them off a deleted
        /// entity is what rule 7 of adr-data-placement-is-decided-on-three-axes forbids.
        /// </remarks>
        private void _Teardown(int stageEntity, EcsPool<AceOfShadowsStageComp> pool)
        {
            foreach (var readyEntity in _world.Where(out SingleTagAspect<DemoReadyTag> _))
                _world.DelEntity(readyEntity);

            var spawn = pool.Get(stageEntity);
            _cardMovePlayer.KillTweensFor(spawn.FirstHandle, spawn.SpawnedCount);

            for (var handle = spawn.FirstHandle;
                 handle < spawn.FirstHandle + spawn.SpawnedCount;
                 handle++)
            {
                if (_views.TryResolve(handle, out _, out var cardView) && cardView != null)
                    UnityEngine.Object.Destroy(cardView.gameObject);

                _views.Unregister(handle);
            }

            if (_screens.TryGet(out AceOfShadowsScreen screen))
            {
                screen.SpeedRequested = false;
                screen.Background.sprite = null;
                var speedButtonImage = screen.SpeedButtonImage;

                if (speedButtonImage != null)
                    speedButtonImage.sprite = null;
            }

            _world.Get<CardArtComp>() = default;
            _warnedOutOfViews = false;
            _screenWidth = -1;
            _screenHeight = -1;

            // Releasing the atlas takes every sprite derived from it, so the ids are dropped rather
            // than freed one by one — the whole of what used to be _DestroySpriteCopies.
            ref var comp = ref pool.Get(stageEntity);
            _assets.Release(ref comp.AtlasRequestId);
            _assets.Release(ref comp.BackgroundRequestId);

            _world.DelEntity(stageEntity);
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _completedMoves = obj.GetPool<MoveCompletedCommand>();
            _runningTweens = obj.GetPool<TweenRunningTag>();
            _resetCommands = obj.GetPool<ResetDeckCommand>();
            _dealCommands = obj.GetPool<DealDeckCommand>();
        }

        public void Inject(ILogService obj) => _log = obj;
        public void Inject(ViewRegistryService obj) => _views = obj;
        public void Inject(StackSlotLayoutService obj) => _layout = obj;
        public void Inject(AddressablesAssetService obj) => _assets = obj;
        public void Inject(CardMovePlayerService obj) => _cardMovePlayer = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;

        private sealed class UnboundAspect : EcsAspect
        {
            public readonly EcsPool<CardComp> Cards = Inc;
            public readonly EcsPool<ViewHandleComp> Views = Exc;
            public readonly EcsTagPool<CardSeatedTag> Seated = Opt;
        }
    }
}
