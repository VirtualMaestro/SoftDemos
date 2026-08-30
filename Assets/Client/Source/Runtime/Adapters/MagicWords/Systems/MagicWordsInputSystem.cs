using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Client.Simulation.Core.Phases;
using Client.Adapters.MagicWords.Components;
using Client.Adapters.MagicWords.Services;
using Client.Adapters.MagicWords.Views;
using Client.Adapters.Shared.Services;
using Client.Adapters.Shared.Stage;
using Client.Simulation.MagicWords.Components;
using Client.Simulation.Core.Navigation;
using Client.Simulation.Core.Navigation.Components;
using Client.Simulation.Core.Ports;
using DCFApixels.DragonECS;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;

namespace Client.Adapters.MagicWords.Systems
{
    /// <summary>
    /// Runs the dialogue demo's screen lifecycle: loads atlas/background/emoji, hands content to
    /// the dialogue log channel, drains the skip and avatar-mode buttons into commands.
    /// </summary>
    /// <remarks>
    /// The two labels this used to paint moved to <see cref="MagicWordsViewSystem"/>, which is the
    /// half that reads the world and draws. What is left is the port polling, the content this
    /// system owns and must destroy, and every world write — an Input phase in everything but the
    /// interface name, which arrives in the flip.
    /// </remarks>
    public sealed class MagicWordsInputSystem : IEcsInput, IEcsDestroy,
        IEcsInject<EcsWorld>, IEcsInject<ILogService>, IEcsInject<AddressablesAssetService>,
        IEcsInject<AvatarImageRouterService>,
        IEcsInject<DialogueLogChannel>, IEcsInject<FadePlayerService>,
        IEcsInject<ScreenRegistryService>
    {
        private const string AtlasAddress = "art/magic-words/atlas";
        private const string BackgroundAddress = "art/magic-words/background";
        private const string EmojiAddress = "art/magic-words/emoji";
        private const string BubbleSpriteName = "mw-bubble";
        private const string FrameSpriteName = "mw-avatar-frame";
        private const string PlaceholderSpriteName = "mw-avatar-placeholder";
        private const string DemoName = "Magic Words";
        private const int DemoIndex = 1;

        private readonly Dictionary<string, Sprite> _sprites = new(StringComparer.Ordinal);

        private EcsWorld _world;
        private ILogService _log;
        private AddressablesAssetService _assets;
        private AvatarImageRouterService _avatars;
        private DialogueLogChannel _dialogueChannel;
        private FadePlayerService _tweens;
        private ScreenRegistryService _screens;
        private StageState _state;
        private MagicWordsScreen _mwScreen;
        private Camera _camera;
        private Sprite[] _atlasSprites = Array.Empty<Sprite>();
        private Sprite _backgroundSprite;
        private bool _ownsBackgroundSprite;
        private int _atlasRequestId;
        private int _backgroundRequestId;
        private int _emojiRequestId;
        private int _screenWidth = -1;
        private int _screenHeight = -1;

        public void Input()
        {
            // Not "_mwScreen != null": what must be torn down is this system's own state, and
            // that is what a non-Idle state says. The screen is a Unity object the scene unload can
            // destroy before this phase runs again — see AceOfShadowsInputSystem for the leak that
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

            if (_screens.TryGet(out MagicWordsScreen current) == false || current == _mwScreen ||
                screen.Current != ScreenId.Demo || screen.ActiveDemoIndex != DemoIndex)
                return;

            _mwScreen = current;
            _atlasRequestId = _assets.Request(new AssetLoadRequest(AtlasAddress));
            _backgroundRequestId = _assets.Request(new AssetLoadRequest(BackgroundAddress));
            _emojiRequestId = _assets.Request(new AssetLoadRequest(EmojiAddress));
            _TransitionTo(StageState.Loading);
        }

        private void _ContinueLoading()
        {
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

            if (_ResolveContent() == false)
            {
                _Teardown(false);
                return;
            }

            _mwScreen.Background.sprite = _backgroundSprite;
            // The screen is covered now, so the shell can hand over.
            _world.GetPool<DemoReadyTag>().Add(_world.NewEntity());
            _avatars.SetLocalSprites(_sprites);
            _dialogueChannel.SetContent(
                StageContent.GetAsset<TMP_SpriteAsset>(_assets, _emojiRequestId),
                _sprites[BubbleSpriteName],
                _sprites[FrameSpriteName],
                _sprites[PlaceholderSpriteName],
                _mwScreen);
            _RecalculateLayout();
            _world.GetPool<LoadDialogueCommand>().Add(_world.NewEntity());
            _TransitionTo(StageState.Ready);
        }

        private void _RunReady()
        {
            if (Screen.width != _screenWidth || Screen.height != _screenHeight)
                _RecalculateLayout();

            if (_mwScreen.SkipRequested)
            {
                _mwScreen.SkipRequested = false;
                _world.GetPool<SkipDialogueCommand>().Add(_world.NewEntity());
            }

            if (_mwScreen.ModeRequested == false)
                return;

            _mwScreen.ModeRequested = false;
            var next = _avatars.Mode == AvatarMode.Local ? AvatarMode.Remote : AvatarMode.Local;
            _avatars.SetMode(next);
            _world.GetPool<ReloadAvatarsCommand>().Add(_world.NewEntity());
        }

        private bool _ResolveContent()
        {
            var atlasAsset = StageContent.GetAsset<SpriteAtlas>(_assets, _atlasRequestId);
            var emojiAsset = StageContent.GetAsset<TMP_SpriteAsset>(_assets, _emojiRequestId);

            if (atlasAsset == null || emojiAsset == null)
            {
                _log.Error("Magic Words addresses did not resolve to a SpriteAtlas and TMP sprite asset.");
                return false;
            }

            _backgroundSprite = StageContent.ResolveBackground(
                _assets, _backgroundRequestId, DemoName, _log, out _ownsBackgroundSprite);

            if (_backgroundSprite == null)
                return false;

            _atlasSprites = new Sprite[atlasAsset.spriteCount];
            var count = atlasAsset.GetSprites(_atlasSprites);

            if (count != atlasAsset.spriteCount)
            {
                _log.Error($"Magic Words atlas returned {count} of {atlasAsset.spriteCount} sprite(s).");
                return false;
            }

            _sprites.Clear();

            foreach (var sprite in _atlasSprites)
            {
                var spriteName = sprite.name.Replace("(Clone)", string.Empty).Trim();
                _sprites[spriteName] = sprite;
            }

            if (_sprites.ContainsKey(BubbleSpriteName) && _sprites.ContainsKey(FrameSpriteName) &&
                _sprites.ContainsKey(PlaceholderSpriteName))
                return true;

            _log.Error("Magic Words atlas is missing a required dialogue UI sprite.");
            return false;
        }

        private void _RecalculateLayout()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            _camera = StageContent.FitBackground(_camera, _mwScreen.Background.transform,
                _backgroundSprite, DemoName, _log, out _);
        }

        private void _Teardown(bool resetDialogue)
        {
            if (_state == StageState.Idle && _mwScreen == null && _atlasRequestId == 0 &&
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
            _dialogueChannel.Reset();
            _world.GetPool<DialogueLogResetEvent>().Add(_world.NewEntity());
            _avatars.ClearLocalSprites();
            _DestroySpriteCopies();
            StageContent.DestroyOwnedSprite(ref _backgroundSprite, ref _ownsBackgroundSprite);

            if (_mwScreen != null)
            {
                _mwScreen.SkipRequested = false;
                _mwScreen.ModeRequested = false;
                _mwScreen.Background.sprite = null;
            }

            _ReleaseRequests();

            _mwScreen = null;
            _camera = null;
            _screenWidth = -1;
            _screenHeight = -1;
            _TransitionTo(StageState.Idle);
        }

        private void _DestroySpriteCopies()
        {
            foreach (var sprite in _atlasSprites)
                if (sprite != null)
                    UnityEngine.Object.Destroy(sprite);

            _atlasSprites = Array.Empty<Sprite>();
            _sprites.Clear();
        }

        private void _ReleaseRequests()
        {
            _atlasRequestId = StageContent.Release(_assets, _atlasRequestId);
            _backgroundRequestId = StageContent.Release(_assets, _backgroundRequestId);
            _emojiRequestId = StageContent.Release(_assets, _emojiRequestId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _TransitionTo(StageState next) => _state = next;

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILogService obj) => _log = obj;
        public void Inject(AddressablesAssetService obj) => _assets = obj;
        public void Inject(AvatarImageRouterService obj) => _avatars = obj;
        public void Inject(DialogueLogChannel obj) => _dialogueChannel = obj;
        public void Inject(FadePlayerService obj) => _tweens = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;

    }
}
