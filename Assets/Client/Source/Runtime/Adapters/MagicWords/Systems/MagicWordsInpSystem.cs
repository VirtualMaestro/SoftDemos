using System.Runtime.CompilerServices;
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
    /// </remarks>
    internal sealed class MagicWordsInpSystem : IEcsInput, IEcsDestroy,
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

        private EcsWorld _world;
        private ILogService _log;
        private AddressablesAssetService _assets;
        private AvatarImageRouterService _avatars;
        private FadePlayerService _tweens;
        private ScreenRegistryService _screens;
        private StageState _state;

        /// <summary>
        /// The instance id of the screen this system opened on, so a reopened scene reads as a
        /// different screen without a reference to the old one being kept.
        /// </summary>
        private int _screenInstanceId;

        private int _backgroundId;
        private int _atlasRequestId;
        private int _backgroundRequestId;
        private int _emojiRequestId;
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
                 !_screens.TryGet<MagicWordsScreen>(out _)))
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

            if (!_screens.TryGet(out MagicWordsScreen current) ||
                current.GetInstanceID() == _screenInstanceId ||
                screen.Current != ScreenId.Demo || screen.ActiveDemoIndex != DemoIndex)
                return;

            _screenInstanceId = current.GetInstanceID();
            _atlasRequestId = _assets.Request(new AssetLoadRequest(AtlasAddress));
            _backgroundRequestId = _assets.Request(new AssetLoadRequest(BackgroundAddress));
            _emojiRequestId = _assets.Request(new AssetLoadRequest(EmojiAddress));
            _TransitionTo(StageState.Loading);
        }

        private void _ContinueLoading()
        {
            if (!_screens.TryGet(out MagicWordsScreen screen))
                return;

            var atlasStatus = _assets.Poll(_atlasRequestId);
            var backgroundStatus = _assets.Poll(_backgroundRequestId);
            var emojiStatus = _assets.Poll(_emojiRequestId);

            if (atlasStatus == AsyncOpStatus.Failed || backgroundStatus == AsyncOpStatus.Failed ||
                emojiStatus == AsyncOpStatus.Failed)
            {
                _log.Error("Magic Words content load failed; retrying while the scene remains active.");
                _Teardown(false);
                return;
            }

            if (atlasStatus != AsyncOpStatus.Done || backgroundStatus != AsyncOpStatus.Done ||
                emojiStatus != AsyncOpStatus.Done)
                return;

            if (!_ResolveContent())
            {
                _Teardown(false);
                return;
            }

            if (_assets.TryGetAsset(_backgroundId, out var background))
                screen.Background.sprite = background as Sprite;

            // The screen is covered now, so the shell can hand over.
            _world.GetPool<DemoReadyTag>().Add(_world.NewEntity());
            _avatars.SetLocalAtlas(_atlasRequestId);
            _RecalculateLayout(screen);
            _world.GetPool<LoadDialogueCommand>().Add(_world.NewEntity());
            _TransitionTo(StageState.Ready);
        }

        private void _RunReady()
        {
            if (!_screens.TryGet(out MagicWordsScreen screen))
                return;

            if (Screen.width != _screenWidth || Screen.height != _screenHeight)
                _RecalculateLayout(screen);

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
        private bool _ResolveContent()
        {
            if (!_assets.TryGetAsset(_emojiRequestId, out var emoji) || emoji is not TMP_SpriteAsset)
            {
                _log.Error("Magic Words emoji address did not resolve to a TMP sprite asset.");
                return false;
            }

            _backgroundId = _assets.ResolveSprite(_backgroundRequestId);

            if (_backgroundId == 0)
                return false;

            ref var art = ref _world.Get<DialogueLogArtComp>();
            art.Emoji = _emojiRequestId;
            art.Bubble = _assets.DeriveSprite(_atlasRequestId, BubbleSpriteName);
            art.Frame = _assets.DeriveSprite(_atlasRequestId, FrameSpriteName);
            art.Placeholder = _assets.DeriveSprite(_atlasRequestId, PlaceholderSpriteName);

            if (art.Bubble != 0 && art.Frame != 0 && art.Placeholder != 0)
                return true;

            _log.Error("Magic Words atlas would not hand over a required dialogue UI sprite.");
            return false;
        }

        private void _RecalculateLayout(MagicWordsScreen screen)
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;

            _assets.TryGetAsset(_backgroundId, out var background);

            BackgroundFitter.CoverFit(screen.Background.transform, background as Sprite,
                screen.StageCamera, _screenWidth, _screenHeight);
        }

        private void _Teardown(bool resetDialogue)
        {
            if (_state == StageState.Idle && _screenInstanceId == 0 && _atlasRequestId == 0 &&
                _backgroundRequestId == 0 && _emojiRequestId == 0)
                return;

            if (resetDialogue)
                _world.GetPool<ResetDialogueCommand>().Add(_world.NewEntity());

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

            // Releasing the atlas takes the three sprites cut from it, and releasing the background
            // takes the sprite derived from its texture — the whole of _DestroySpriteCopies.
            _ReleaseRequests();

            _backgroundId = 0;
            _screenInstanceId = 0;
            _screenWidth = -1;
            _screenHeight = -1;
            _TransitionTo(StageState.Idle);
        }

        private void _ReleaseRequests()
        {
            _assets.Release(ref _atlasRequestId);
            _assets.Release(ref _backgroundRequestId);
            _assets.Release(ref _emojiRequestId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _TransitionTo(StageState next) => _state = next;

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILogService obj) => _log = obj;
        public void Inject(AddressablesAssetService obj) => _assets = obj;
        public void Inject(AvatarImageRouterService obj) => _avatars = obj;
        public void Inject(FadePlayerService obj) => _tweens = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;
    }
}
