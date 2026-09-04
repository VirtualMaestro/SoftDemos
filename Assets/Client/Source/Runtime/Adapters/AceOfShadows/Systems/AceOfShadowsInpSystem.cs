using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
    /// <see cref="ScreenRegistryService"/> and is resolved per call. What stays here is
    /// <c>int</c>s and a cursor (adr-an-engine-object-has-one-owner-per-kind, DEU0146).</para>
    /// <para>Binding lives here rather than in <see cref="CardBindingPreSystem"/> because the face
    /// a card shows is chosen by the BIND ORDER — <c>cursor % faces</c> — and the cursor is this
    /// system's state. The Present half keeps what it can answer from the world alone: the seating
    /// and the sorting order.</para>
    /// </remarks>
    internal sealed class AceOfShadowsInpSystem : IEcsInput, IEcsDestroy,
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

        /// <summary>Speed multipliers the button cycles through, starting at ×1.</summary>
        private static readonly float[] SpeedCycle = { 1f, 4f, 8f };

        private readonly AceOfShadowsConfig _config;

        /// <summary>Face ids in atlas-name order, handed out by the asset service.</summary>
        private readonly int[] _faceIds = new int[FaceCount];

        /// <summary>Every card view this system spawned, by registry handle and in spawn order.</summary>
        private readonly List<int> _handles = new();

        private EcsWorld _world;
        private ILogService _log;
        private ViewRegistryService _views;
        private StackSlotLayoutService _layout;
        private AddressablesAssetService _assets;
        private CardMovePlayerService _cardMovePlayer;
        private ScreenRegistryService _screens;
        private StageState _state;

        /// <summary>
        /// The instance id of the screen this system opened on, so a reopened scene reads as a
        /// different screen without a reference to the old one being kept.
        /// </summary>
        private int _screenInstanceId;

        private int _backId;
        private int _backgroundId;
        private int _atlasRequestId;
        private int _backgroundRequestId;
        private int _bindCursor;
        private bool _warnedOutOfViews;
        private int _screenWidth = -1;
        private int _screenHeight = -1;
        private int _speedIndex;
        private bool _contentReady;
        private EcsTagPool<MoveCompletedCommand> _completedMoves;
        private EcsTagPool<TweenRunningTag> _runningTweens;

        public AceOfShadowsInpSystem(AceOfShadowsConfig config)
        {
            _config = config;
        }

        public void Input()
        {
            _DrainCompletedTweens();

            // Not "the screen is gone": what must be torn down is this system's own state — the
            // requests, the ids, the view pool — and that is what a non-Idle state says. The screen
            // is a Unity object the scene unload can destroy before this phase runs again, and
            // gating teardown on it leaked every request on the frames where it did.
            if (_state != StageState.Idle && _state != StageState.Closing &&
                (_world.Get<ScreenStateComp>().Current == ScreenId.Unloading ||
                 !_screens.TryGet<AceOfShadowsScreen>(out _)))
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

        public void Destroy()
        {
            _Teardown(false);
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

        private void _BeginLoadingIfNeeded()
        {
            ref readonly var screen = ref _world.Get<ScreenStateComp>();

            if (!_screens.TryGet(out AceOfShadowsScreen current) ||
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
            if (!_screens.TryGet(out AceOfShadowsScreen screen))
                return;

            if (!_contentReady)
            {
                var atlasStatus = _assets.Poll(_atlasRequestId);
                var backgroundStatus = _assets.Poll(_backgroundRequestId);

                if (atlasStatus == AsyncOpStatus.Failed || backgroundStatus == AsyncOpStatus.Failed)
                {
                    _log.Error("Ace of Shadows content load failed. Retrying while the scene is open.");
                    _Teardown(false);
                    return;
                }

                if (atlasStatus != AsyncOpStatus.Done || backgroundStatus != AsyncOpStatus.Done)
                    return;

                if (!_ResolveContent())
                {
                    _Teardown(false);
                    return;
                }

                _contentReady = true;

                if (_assets.TryGetAsset(_backgroundId, out var background))
                    screen.Background.sprite = background as Sprite;

                // The screen is covered now, so the shell can hand over. The cards still arrive
                // over the next few frames, on top of the background.
                _world.GetPool<DemoReadyTag>().Add(_world.NewEntity());
                _SkinSpeedButton(screen);
                _RecalculateLayout(screen);
            }

            var remaining = _config.CardCount - _handles.Count;
            var spawnCount = Mathf.Min(SpawnPerFrame, remaining);

            for (var index = 0; index < spawnCount; index++)
            {
                var poolIndex = _handles.Count;
                var cardView = UnityEngine.Object.Instantiate(screen.CardPrefab, screen.CardRoot);
                cardView.name = $"Card {poolIndex:000}";
                _handles.Add(_views.Register(cardView.transform, cardView));
            }

            if (_handles.Count != _config.CardCount)
                return;

            _world.GetPool<DealDeckCommand>().Add(_world.NewEntity());
            _TransitionTo(StageState.Ready);
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

        private void _RunReady()
        {
            if (!_screens.TryGet(out AceOfShadowsScreen screen))
                return;

            if (Screen.width != _screenWidth || Screen.height != _screenHeight)
                _RecalculateLayout(screen);

            _BindUnboundCards();

            if (!screen.SpeedRequested)
                return;

            screen.SpeedRequested = false;
            _speedIndex = (_speedIndex + 1) % SpeedCycle.Length;
            var multiplier = SpeedCycle[_speedIndex];
            var commandEntity = _world.NewEntity();
            _world.GetPool<SetDeckSpeedCommand>().Add(commandEntity).Multiplier = multiplier;
        }

        /// <summary>
        /// Gives every card the simulation dealt one of the pooled views, in spawn order.
        /// </summary>
        /// <remarks>
        /// The face is <c>cursor % FaceCount</c> — a property of the BIND order and never of the
        /// entity, which is why this belongs to the half that owns the cursor. Both sprites are
        /// resolved from the asset service on the call that configures the view, and neither is
        /// kept afterwards.
        /// </remarks>
        private void _BindUnboundCards()
        {
            foreach (var entityId in _world.Where(out UnboundAspect aspect))
            {
                if (_bindCursor >= _handles.Count)
                {
                    if (!_warnedOutOfViews)
                    {
                        _warnedOutOfViews = true;
                        _log.Warn($"Ace of Shadows ran out of views after {_bindCursor} binding(s).");
                    }

                    return;
                }

                var handleId = _handles[_bindCursor];

                if (!_views.TryResolve(handleId, out var viewTransform, out var cardView) ||
                    cardView == null)
                {
                    _log.Warn($"Card view handle #{handleId} does not resolve. Skipping the bind.");
                    _bindCursor++;
                    continue;
                }

                ref readonly var card = ref aspect.Cards.Read(entityId);

                if (_assets.TryGetAsset(_backId, out var back) &&
                    _assets.TryGetAsset(_faceIds[_bindCursor % FaceCount], out var face))
                    cardView.Configure(back as Sprite, face as Sprite);

                cardView.ResetToBack();
                viewTransform.position = _layout.SlotPosition(card.StackIndex, card.OrderInStack);
                cardView.SetSortingOrder(card.OrderInStack);
                aspect.Views.Add(entityId).Id = handleId;
                aspect.Seated.TryAdd(entityId);
                _bindCursor++;
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
        private bool _ResolveContent()
        {
            var names = _assets.ReadAtlasNames(_atlasRequestId);

            if (names == null)
                return false;

            _backgroundId = _assets.ResolveSprite(_backgroundRequestId);

            if (_backgroundId == 0)
                return false;

            if (names.Length != FaceCount + 1)
            {
                _log.Error($"Ace of Shadows atlas has {names.Length} sprite(s); expected {FaceCount + 1}.");
                return false;
            }

            // Sorted by name, so the faces keep the order the demo has always dealt them in.
            Array.Sort(names, StringComparer.Ordinal);

            var faceIndex = 0;

            foreach (var spriteName in names)
                if (spriteName == BackSpriteName)
                    _backId = _assets.DeriveSprite(_atlasRequestId, spriteName);
                else if (faceIndex < _faceIds.Length)
                    _faceIds[faceIndex++] = _assets.DeriveSprite(_atlasRequestId, spriteName);

            if (_backId == 0 || faceIndex != FaceCount || Array.IndexOf(_faceIds, 0) >= 0)
            {
                _log.Error($"Ace of Shadows atlas is missing '{BackSpriteName}' or one of {FaceCount} faces.");
                return false;
            }

            return true;
        }

        private void _RecalculateLayout(AceOfShadowsScreen screen)
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;

            _assets.TryGetAsset(_backgroundId, out var background);

            var orthographicSize = BackgroundFitter.CoverFit(screen.Background.transform,
                background as Sprite, screen.StageCamera, _screenWidth, _screenHeight);

            _layout.Recalculate(_screenWidth, _screenHeight, orthographicSize);
            _world.GetPool<LayoutChangedEvent>().Add(_world.NewEntity());
        }

        private void _Teardown(bool resetDeck)
        {
            if (_state == StageState.Idle && _screenInstanceId == 0 && _atlasRequestId == 0 &&
                _backgroundRequestId == 0 && _handles.Count == 0)
                return;

            foreach (var readyEntity in _world.Where(out SingleTagAspect<DemoReadyTag> _))
                _world.DelEntity(readyEntity);

            _cardMovePlayer.KillTweensFor(_handles);

            if (resetDeck)
                _world.GetPool<ResetDeckCommand>().Add(_world.NewEntity());

            foreach (var handle in _handles)
            {
                if (_views.TryResolve(handle, out _, out var cardView) && cardView != null)
                    UnityEngine.Object.Destroy(cardView.gameObject);

                _views.Unregister(handle);
            }

            _handles.Clear();

            if (_screens.TryGet(out AceOfShadowsScreen screen))
            {
                screen.SpeedRequested = false;
                screen.Background.sprite = null;
                var speedButtonImage = screen.SpeedButtonImage;

                if (speedButtonImage != null)
                    speedButtonImage.sprite = null;
            }

            // Releasing the atlas takes every sprite derived from it, so the ids are dropped rather
            // than freed one by one — the whole of what used to be _DestroySpriteCopies.
            _ReleaseRequests();

            _backId = 0;
            _backgroundId = 0;
            Array.Clear(_faceIds, 0, _faceIds.Length);
            _screenInstanceId = 0;
            _bindCursor = 0;
            _warnedOutOfViews = false;
            _contentReady = false;
            _speedIndex = 0;
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

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _completedMoves = obj.GetPool<MoveCompletedCommand>();
            _runningTweens = obj.GetPool<TweenRunningTag>();
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
