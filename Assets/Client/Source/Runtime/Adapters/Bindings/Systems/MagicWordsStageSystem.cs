using System;
using System.Collections.Generic;
using DCFApixels.DragonECS;
using Game.Adapters.Services;
using Game.Adapters.Views;
using Game.Simulation.MagicWords;
using Game.Simulation.Menu;
using Game.Simulation.Ports;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;

namespace Game.Adapters.Bindings
{
    public sealed class MagicWordsStageSystem : IEcsLateRun, IEcsDestroy,
        IEcsInject<EcsWorld>, IEcsInject<ILog>
    {
        private const string AtlasAddress = "art/magic-words/atlas";
        private const string BackgroundAddress = "art/magic-words/background";
        private const string EmojiAddress = "art/magic-words/emoji";
        private const string BubbleSpriteName = "mw-bubble";
        private const string FrameSpriteName = "mw-avatar-frame";
        private const string PlaceholderSpriteName = "mw-avatar-placeholder";
        private const string LocalModeLabel = "Avatars: Local";
        private const string RemoteModeLabel = "Avatars: Remote";
        private const string LoadingStatus = "Loading dialogue…";
        private const string FailedStatus = "Dialogue failed to load. Go back and try again.";

        private readonly AddressablesAssetService _assets;
        private readonly AtlasImageLoaderService _atlas;
        private readonly AvatarImageRouterService _avatars;
        private readonly DialogueLogChannel _dialogueChannel;
        private readonly TweenPlayer _tweens;
        private readonly StageReadyChannel _stageReady;
        private readonly Dictionary<string, Sprite> _sprites = new(StringComparer.Ordinal);

        private EcsWorld _world;
        private ILog _log;
        private StageState _state;
        private MagicWordsScreen _scene;
        private Camera _camera;
        private Sprite[] _atlasSprites = Array.Empty<Sprite>();
        private Sprite _backgroundSprite;
        private bool _ownsBackgroundSprite;
        private int _atlasRequestId;
        private int _backgroundRequestId;
        private int _emojiRequestId;
        private int _screenWidth = -1;
        private int _screenHeight = -1;
        private DialogueLoadState _shownDialogueState = (DialogueLoadState)(-1);
        private bool _skipRequested;
        private bool _modeRequested;
        private bool _loggedMissingCamera;

        public MagicWordsStageSystem(AddressablesAssetService assets, AtlasImageLoaderService atlas,
            AvatarImageRouterService avatars, DialogueLogChannel dialogueChannel, TweenPlayer tweens,
            StageReadyChannel stageReady)
        {
            _assets = assets;
            _atlas = atlas;
            _avatars = avatars;
            _dialogueChannel = dialogueChannel;
            _tweens = tweens;
            _stageReady = stageReady;
        }

        public void LateRun()
        {
            if (_scene != null && _state != StageState.Closing &&
                (_world.Get<ScreenStateComp>().Current == ScreenId.Unloading ||
                 MagicWordsScreen.Current == null))
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
            var current = MagicWordsScreen.Current;
            ref readonly var screen = ref _world.Get<ScreenStateComp>();

            if (current == null || current == _scene || screen.Current != ScreenId.Demo ||
                screen.ActiveDemoIndex != 1)
                return;

            _scene = current;
            _scene.OnSkipPressed += _OnRequestSkip;
            _scene.OnAvatarModePressed += _OnRequestModeChange;
            _atlasRequestId = _assets.BeginLoad(AtlasAddress);
            _backgroundRequestId = _assets.BeginLoad(BackgroundAddress);
            _emojiRequestId = _assets.BeginLoad(EmojiAddress);
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

            _scene.Background.sprite = _backgroundSprite;
            // The screen is covered now, so the shell can hand over.
            _stageReady.MarkDemoReady();
            _atlas.SetSprites(_sprites);
            _dialogueChannel.SetContent(
                _GetAsset<TMP_SpriteAsset>(_emojiRequestId),
                _sprites[BubbleSpriteName],
                _sprites[FrameSpriteName],
                _sprites[PlaceholderSpriteName],
                _scene);
            _UpdateAvatarModeLabel();
            _RecalculateLayout();
            _WriteCommand<LoadDialogueCommand>();
            _log.Info("Magic Words content loaded.");
            _TransitionTo(StageState.Ready);
        }

        private void _RunReady()
        {
            if (Screen.width != _screenWidth || Screen.height != _screenHeight)
                _RecalculateLayout();

            _UpdateStatusLabel();

            if (_skipRequested)
            {
                _skipRequested = false;
                _WriteCommand<SkipDialogueCommand>();
            }

            if (_modeRequested == false)
                return;

            _modeRequested = false;
            var next = _avatars.Mode == AvatarMode.Local ? AvatarMode.Remote : AvatarMode.Local;
            _avatars.SetMode(next);
            _UpdateAvatarModeLabel();
            _WriteCommand<ReloadAvatarsCommand>();
            _log.Info($"Magic Words avatar mode changed to {next}.");
        }

        private bool _ResolveContent()
        {
            var atlasAsset = _GetAsset<SpriteAtlas>(_atlasRequestId);
            var emojiAsset = _GetAsset<TMP_SpriteAsset>(_emojiRequestId);
            var backgroundHandle = _assets.ResolveHandle(_backgroundRequestId);

            if (atlasAsset == null || emojiAsset == null ||
                _assets.TryGetAsset(backgroundHandle, out var backgroundAsset) == false)
            {
                _log.Error("Magic Words addresses did not resolve to a SpriteAtlas, background and TMP sprite asset.");
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
                _log.Error($"Magic Words background resolved as {backgroundAsset.GetType().Name}, " +
                    "expected Sprite or Texture2D.");
                return false;
            }

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

        private T _GetAsset<T>(int requestId) where T : UnityEngine.Object
        {
            var handleId = _assets.ResolveHandle(requestId);
            return _assets.TryGetAsset(handleId, out var asset) ? asset as T : null;
        }

        private void _UpdateStatusLabel()
        {
            ref readonly var dialogue = ref _world.Get<DialogueStateComp>();

            if (_shownDialogueState == dialogue.State)
                return;

            _shownDialogueState = dialogue.State;
            var failed = dialogue.State == DialogueLoadState.Failed;
            _scene.StatusLabel.gameObject.SetActive(dialogue.State != DialogueLoadState.Ready);
            _scene.StatusLabel.text = failed ? FailedStatus : LoadingStatus;
        }

        private void _UpdateAvatarModeLabel()
        {
            _scene.AvatarModeLabel.text = _avatars.Mode == AvatarMode.Local
                ? LocalModeLabel
                : RemoteModeLabel;
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
                _log.Error("No MainCamera found for Magic Words layout; using orthographic size 5.");
            }

            if (_scene != null)
                BackgroundFitter.CoverFit(_scene.Background.transform, _backgroundSprite, _camera,
                    orthographicSize, _screenWidth, _screenHeight);
            var mode = _screenWidth < _screenHeight ? "portrait" : "landscape";
            _log.Info($"Magic Words layout recalculated for {_screenWidth}×{_screenHeight} ({mode}).");
        }

        private void _Teardown(bool resetDialogue)
        {
            if (_state == StageState.Idle && _scene == null && _atlasRequestId == 0 &&
                _backgroundRequestId == 0 && _emojiRequestId == 0)
                return;

            if (resetDialogue)
                _WriteCommand<ResetDialogueCommand>();
            _stageReady.ClearDemo();
            _tweens.KillFades();
            // The dialogue log destroys its own views when it sees the change. That happens later
            // in this same LateRun pass, or in its IEcsDestroy on teardown.
            _dialogueChannel.Reset();
            _atlas.ClearSprites();
            _DestroySpriteCopies();
            _DestroyBackgroundCopy();

            if (_scene != null)
            {
                _scene.OnSkipPressed -= _OnRequestSkip;
                _scene.OnAvatarModePressed -= _OnRequestModeChange;
                _scene.Background.sprite = null;
            }

            _ReleaseRequests();

            _scene = null;
            _camera = null;
            _shownDialogueState = (DialogueLoadState)(-1);
            _screenWidth = -1;
            _screenHeight = -1;
            _skipRequested = false;
            _modeRequested = false;
            _loggedMissingCamera = false;
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

            if (_emojiRequestId != 0)
                _assets.Release(_emojiRequestId);
            _atlasRequestId = 0;
            _backgroundRequestId = 0;
            _emojiRequestId = 0;
        }

        private void _OnRequestSkip() => _skipRequested = true;
        private void _OnRequestModeChange() => _modeRequested = true;

        private void _WriteCommand<T>() where T : struct, IEcsComponent
        {
            var entityId = _world.NewEntity();
            _world.GetPool<T>().Add(entityId);
        }

        private void _TransitionTo(StageState next)
        {
            if (_state == next)
                return;

            _log.Info($"Magic Words stage: {_state} -> {next}.");
            _state = next;
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILog obj) => _log = obj;

        private enum StageState
        {
            Idle,
            Loading,
            Ready,
            Closing,
        }
    }
}
