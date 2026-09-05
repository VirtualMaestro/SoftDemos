using Client.Simulation.Core.Phases;
using Client.Adapters.MagicWords.Components;
using Client.Adapters.MagicWords.Components.Events;
using Client.Adapters.MagicWords.Services;
using Client.Adapters.MagicWords.Views;
using Client.Adapters.Shared.Components;
using Client.Adapters.Shared.Services;
using Client.Adapters.Shared.Stage;
using Client.Simulation.MagicWords.Components.Commands;
using Client.Simulation.Core.Navigation;
using Client.Simulation.Core.Navigation.Components;
using Client.Simulation.Core.Ports;
using Client.Simulation.Core.Ports.Requests;
using DCFApixels.DragonECS;
using TMPro;
using UnityEngine;

namespace Client.Adapters.MagicWords.Systems
{
    /// <summary>
    /// Runs the dialogue demo's screen lifecycle: loads atlas/background/emoji, writes the art ids
    /// into the world, drains the skip and avatar-mode buttons into commands.
    /// </summary>
    /// <remarks>
    /// The two labels this used to paint moved to <see cref="MagicWordsPreSystem"/>, which is the
    /// half that reads the world and draws. What is left is the port polling and every world write.
    /// <para>It holds no engine object. The atlas, the background and the emoji asset belong to
    /// <see cref="AddressablesAssetService"/>, the three UI sprites are cut out of the atlas under
    /// ids of their own, and those four ids cross to the Present half as
    /// <see cref="DialogueLogArtComp"/> — which is what <c>DialogueLogChannel</c> used to carry as
    /// resolved objects (adr-an-engine-object-has-one-owner-per-kind, DEU0146).</para>
    /// <para>It owns nothing either. The stage's state and the ids it holds open live in
    /// <see cref="MagicWordsStageComp"/>; the two remembered fields are last-drawn screen
    /// dimensions, which the next call derives again from <see cref="Screen"/>
    /// (adr-data-placement-is-decided-on-three-axes rules 4 and 5). There is no
    /// <c>IEcsDestroy</c>: the asset service releases the session's requests when the composition
    /// root disposes it.</para>
    /// </remarks>
    internal sealed class MagicWordsInpSystem : IEcsInput,
        IEcsInject<EcsWorld>, IEcsInject<ILogService>, IEcsInject<AddressablesAssetService>,
        IEcsInject<AvatarImageRouterService>, IEcsInject<FadePlayerService>,
        IEcsInject<ScreenRegistryService>
    {
        private const string AtlasAddress = "art/magic-words/atlas";
        private const string BackgroundAddress = "art/magic-words/background";
        private const string EmojiAddress = "art/magic-words/emoji";
        private const string BubbleSpriteName = "mw-bubble";
        private const string FrameSpriteName = "mw-avatar-frame";
        private const string PlaceholderSpriteName = "mw-avatar-placeholder";
        private const int DemoIndex = 1;

        /// <summary>No stage entity. <c>Idle</c> is recorded as the absence of one.</summary>
        private const int NoStage = 0;

        private EcsWorld _world;
        private ILogService _log;
        private AddressablesAssetService _assets;
        private AvatarImageRouterService _avatars;
        private FadePlayerService _tweens;
        private ScreenRegistryService _screens;

        private int _screenWidth = -1;
        private int _screenHeight = -1;

        private EcsPool<ResetDialogueCommand> _resetCommands;

        public void Input()
        {
            var stage = _world.Where(out SingleAspect<MagicWordsStageComp> stageAspect);
            var stageEntity = stage.Count > 0 ? stage[0] : NoStage;

            ref readonly var nav = ref _world.Get<ScreenStateComp>();
            var unloading = nav.Current == ScreenId.Unloading;
            var hasScreen = _screens.TryGet(out MagicWordsScreen screen);

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
                        _RunReady(screen, comp.BackgroundId);

                    return;
                }

                pool.Get(stageEntity).State = next;

                // Which edge was taken is the pair, which is why the machine returns a state and
                // the side effects live here.
                if (next == StageState.Closing)
                {
                    _resetCommands.Add(_world.NewEntity());
                    continue;
                }

                if (next == StageState.Ready && _HandOver(stageEntity, pool, screen))
                    return;

                if (comp.State == StageState.Loading && next == StageState.Idle)
                    _log.Error(
                        "Magic Words content load failed; retrying while the scene remains active.");

                _Teardown(stageEntity, pool);
                return;
            }
        }

        /// <summary>The worst of the three content requests: a failure first, then a wait.</summary>
        private AsyncOpStatus _Poll(MagicWordsStageComp comp)
        {
            var atlas = _assets.Poll(comp.AtlasRequestId);
            var background = _assets.Poll(comp.BackgroundRequestId);
            var emoji = _assets.Poll(comp.EmojiRequestId);

            if (atlas == AsyncOpStatus.Failed || background == AsyncOpStatus.Failed ||
                emoji == AsyncOpStatus.Failed)
                return AsyncOpStatus.Failed;

            return atlas == AsyncOpStatus.Done && background == AsyncOpStatus.Done &&
                   emoji == AsyncOpStatus.Done
                ? AsyncOpStatus.Done
                : AsyncOpStatus.Pending;
        }

        /// <summary>The <c>Idle -&gt; Loading</c> edge: the stage is born holding its requests.</summary>
        private void _BeginLoading(MagicWordsScreen screen)
        {
            var entity = _world.NewEntity();
            ref var comp = ref _world.GetPool<MagicWordsStageComp>().Add(entity);

            comp.State = StageState.Loading;
            comp.ScreenInstanceId = screen.GetInstanceID();
            comp.AtlasRequestId = _assets.Request(new AssetLoadRequest(AtlasAddress));
            comp.BackgroundRequestId = _assets.Request(new AssetLoadRequest(BackgroundAddress));
            comp.EmojiRequestId = _assets.Request(new AssetLoadRequest(EmojiAddress));
        }

        /// <summary>
        /// The <c>Loading -&gt; Ready</c> edge: resolve the content, dress the screen and let the
        /// shell hand over. Reports whether the content resolved.
        /// </summary>
        private bool _HandOver(int stageEntity, EcsPool<MagicWordsStageComp> pool,
            MagicWordsScreen screen)
        {
            if (screen == null || !_ResolveContent(stageEntity, pool))
                return false;

            var backgroundId = pool.Get(stageEntity).BackgroundId;

            if (_assets.TryGetAsset(backgroundId, out var background))
                screen.Background.sprite = background as Sprite;

            // The screen is covered now, so the shell can hand over.
            _world.GetPool<DemoReadyTag>().Add(_world.NewEntity());
            _avatars.SetLocalAtlas(pool.Get(stageEntity).AtlasRequestId);
            _RecalculateLayout(screen, backgroundId);
            _world.GetPool<LoadDialogueCommand>().Add(_world.NewEntity());
            return true;
        }

        private void _RunReady(MagicWordsScreen screen, int backgroundId)
        {
            if (screen == null)
                return;

            if (Screen.width != _screenWidth || Screen.height != _screenHeight)
                _RecalculateLayout(screen, backgroundId);

            if (screen.SkipRequested)
            {
                screen.SkipRequested = false;
                _world.GetPool<SkipDialogueCommand>().Add(_world.NewEntity());
            }

            if (!screen.ModeRequested)
                return;

            screen.ModeRequested = false;
            var next = _avatars.Mode == AvatarMode.Local ? AvatarMode.Remote : AvatarMode.Local;
            _avatars.SetMode(next);
            _world.GetPool<ReloadAvatarsCommand>().Add(_world.NewEntity());
        }

        /// <summary>
        /// Resolves the three loaded assets and cuts the demo's UI sprites out of the atlas, then
        /// writes the four ids the Present half draws with into the world.
        /// </summary>
        /// <remarks>
        /// The cut runs ONCE per open and the asset service owns every copy: releasing the atlas
        /// takes all three with it, which is what <c>_DestroySpriteCopies</c> used to do by hand.
        /// </remarks>
        private bool _ResolveContent(int stageEntity, EcsPool<MagicWordsStageComp> pool)
        {
            if (!_assets.TryGetAsset(pool.Get(stageEntity).EmojiRequestId, out var emoji) ||
                emoji is not TMP_SpriteAsset)
            {
                _log.Error("Magic Words emoji address did not resolve to a TMP sprite asset.");
                return false;
            }

            ref var comp = ref pool.Get(stageEntity);
            comp.BackgroundId = _assets.ResolveSprite(comp.BackgroundRequestId);

            if (comp.BackgroundId == 0)
                return false;

            ref var art = ref _world.Get<DialogueLogArtComp>();
            art.Emoji = comp.EmojiRequestId;
            art.Bubble = _assets.DeriveSprite(comp.AtlasRequestId, BubbleSpriteName);
            art.Frame = _assets.DeriveSprite(comp.AtlasRequestId, FrameSpriteName);
            art.Placeholder = _assets.DeriveSprite(comp.AtlasRequestId, PlaceholderSpriteName);

            if (art.Bubble != 0 && art.Frame != 0 && art.Placeholder != 0)
                return true;

            _log.Error("Magic Words atlas would not hand over a required dialogue UI sprite.");
            return false;
        }

        private void _RecalculateLayout(MagicWordsScreen screen, int backgroundId)
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;

            _assets.TryGetAsset(backgroundId, out var background);

            BackgroundFitter.CoverFit(screen.Background.transform, background as Sprite,
                screen.StageCamera, _screenWidth, _screenHeight);
        }

        /// <summary>
        /// The edge back to <c>Idle</c>: hand everything back to its owner, then delete the stage.
        /// </summary>
        /// <remarks>
        /// No guard on "is there anything to tear down": there is a stage entity or there is not,
        /// which is the fact the five zeroed fields used to spell out. The entity goes LAST, after
        /// the ids it carries have been released — reading them off a deleted entity is what rule 7
        /// of adr-data-placement-is-decided-on-three-axes forbids.
        /// </remarks>
        private void _Teardown(int stageEntity, EcsPool<MagicWordsStageComp> pool)
        {
            foreach (var readyEntity in _world.Where(out SingleTagAspect<DemoReadyTag> _))
                _world.DelEntity(readyEntity);

            _tweens.KillFades();
            // The log owns its views and destroys them itself; this only says they are stale.
            // A direct call would be one system holding another, which SystemIsolationTests
            // forbids and the event exists to replace.
            _world.GetPool<DialogueLogResetEvent>().Add(_world.NewEntity());
            _avatars.ClearLocalAtlas();
            _world.Get<DialogueLogArtComp>() = default;

            if (_screens.TryGet(out MagicWordsScreen screen))
            {
                screen.SkipRequested = false;
                screen.ModeRequested = false;
                screen.Background.sprite = null;
            }

            _screenWidth = -1;
            _screenHeight = -1;

            // Releasing the atlas takes the three sprites cut from it, and releasing the background
            // takes the sprite derived from its texture — the whole of _DestroySpriteCopies.
            ref var comp = ref pool.Get(stageEntity);
            _assets.Release(ref comp.AtlasRequestId);
            _assets.Release(ref comp.BackgroundRequestId);
            _assets.Release(ref comp.EmojiRequestId);

            _world.DelEntity(stageEntity);
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _resetCommands = obj.GetPool<ResetDialogueCommand>();
        }

        public void Inject(ILogService obj) => _log = obj;
        public void Inject(AddressablesAssetService obj) => _assets = obj;
        public void Inject(AvatarImageRouterService obj) => _avatars = obj;
        public void Inject(FadePlayerService obj) => _tweens = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;
    }
}
