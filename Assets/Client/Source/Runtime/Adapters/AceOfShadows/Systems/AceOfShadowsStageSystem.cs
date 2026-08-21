using System;
using Client.Adapters.AceOfShadows.Services;
using Client.Adapters.AceOfShadows.Views;
using Client.Adapters.Shared.Services;
using Client.Adapters.Shared.Stage;
using Client.Simulation.AceOfShadows;
using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.Shared.Navigation;
using Client.Simulation.Shared.Navigation.Components;
using Client.Simulation.Shared.Ports;
using DCFApixels.DragonECS;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace Client.Adapters.AceOfShadows.Systems
{
    /// <summary>
    /// Runs the card demo's screen lifecycle: loads atlas+background, spawns the card view pool,
    /// forwards the speed button, tears everything down on close.
    /// </summary>
    public sealed class AceOfShadowsStageSystem : IEcsLateRun, IEcsDestroy,
        IEcsInject<EcsWorld>, IEcsInject<ILogService>, IEcsInject<ViewRegistryService>,
        IEcsInject<StackSlotLayoutService>, IEcsInject<AddressablesAssetService>,
        IEcsInject<CardMovePlayerService>, IEcsInject<SharedUiSprites>, IEcsInject<CardViewChannel>,
        IEcsInject<StageReadyChannel>, IEcsInject<ScreenRegistryService>
    {
        private const string AtlasAddress = "art/ace-of-shadows/atlas";
        private const string BackgroundAddress = "art/ace-of-shadows/background";
        private const string BackSpriteName = "card-back";
        private const string DemoName = "Ace of Shadows";
        private const int DemoIndex = 0;
        private const int FaceCount = 13;
        private const int SpawnPerFrame = 24;

        /// <summary>Speed multipliers the button cycles through, starting at ×1.</summary>
        private static readonly float[] SpeedCycle = { 1f, 4f, 8f };

        private readonly AceOfShadowsConfig _config;
        private readonly Sprite[] _atlasSprites = new Sprite[FaceCount + 1];
        private readonly Sprite[] _faces = new Sprite[FaceCount];

        private EcsWorld _world;
        private ILogService _log;
        private ViewRegistryService _views;
        private StackSlotLayoutService _layout;
        private AddressablesAssetService _assets;
        private CardMovePlayerService _cardMovePlayer;
        private SharedUiSprites _uiSprites;
        private CardViewChannel _channel;
        private StageReadyChannel _stageReady;
        private ScreenRegistryService _screens;
        private StageState _state;
        private AceOfShadowsScreen _aosScreen;
        private Camera _camera;
        private Sprite _cardBack;
        private Sprite _backgroundSprite;
        private bool _ownsBackgroundSprite;
        private int _atlasRequestId;
        private int _backgroundRequestId;
        private int _screenWidth = -1;
        private int _screenHeight = -1;
        private int _speedIndex;
        private bool _contentReady;
        private bool _speedRequested;

        public AceOfShadowsStageSystem(AceOfShadowsConfig config)
        {
            _config = config;
        }

        public void LateRun()
        {
            if (_aosScreen != null && _state != StageState.Closing &&
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

        private void _BeginLoadingIfNeeded()
        {
            ref readonly var screen = ref _world.Get<ScreenStateComp>();

            if (!_screens.TryGet(out AceOfShadowsScreen current) || current == _aosScreen ||
                screen.Current != ScreenId.Demo || screen.ActiveDemoIndex != DemoIndex)
                return;

            _aosScreen = current;
            _aosScreen.OnSpeedButtonPressed += _OnRequestSpeedChange;

            _atlasRequestId = _assets.BeginLoad(AtlasAddress);
            _backgroundRequestId = _assets.BeginLoad(BackgroundAddress);

            _TransitionTo(StageState.Loading);
        }

        private void _ContinueLoading()
        {
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

                if (_ResolveContent() == false)
                {
                    _Teardown(false);
                    return;
                }

                _contentReady = true;
                _channel.SetSprites(_cardBack, _faces);
                _aosScreen.Background.sprite = _backgroundSprite;
                // The screen is covered now, so the shell can hand over. The cards still arrive
                // over the next few frames, on top of the background.
                _stageReady.MarkDemoReady();
                _SkinSpeedButton();
                _RecalculateLayout();
            }

            var remaining = _config.CardCount - _channel.Views.Count;
            var spawnCount = Mathf.Min(SpawnPerFrame, remaining);
            for (var index = 0; index < spawnCount; index++)
            {
                var poolIndex = _channel.Views.Count;
                var cardView = UnityEngine.Object.Instantiate(_aosScreen.CardPrefab, _aosScreen.CardRoot);
                cardView.name = $"Card {poolIndex:000}";
                _channel.Add(cardView, _views.Register(cardView.transform, cardView));
            }

            if (_channel.Views.Count != _config.CardCount)
                return;

            _channel.BumpBindingReset();
            _channel.BumpSeating();
            _world.GetPool<DealDeckCommand>().Add(_world.NewEntity());
            _TransitionTo(StageState.Ready);
        }

        /// <summary>Skins the speed button with the shell's shared <c>ui-button</c> sprite.</summary>
        /// <remarks>
        /// The sprite is borrowed, not loaded. A second request on the shared atlas would make a
        /// second copy of the same pixels. Does nothing if the shell has not loaded yet.
        /// </remarks>
        private void _SkinSpeedButton()
        {
            var image = _aosScreen.SpeedButtonImage;

            if (image == null || _uiSprites.Button == null)
                return;

            image.sprite = _uiSprites.Button;
            image.type = Image.Type.Sliced;
        }

        private void _RunReady()
        {
            if (Screen.width != _screenWidth || Screen.height != _screenHeight)
            {
                _RecalculateLayout();
                _channel.BumpSeating();
            }

            if (_speedRequested == false)
                return;

            _speedRequested = false;
            _speedIndex = (_speedIndex + 1) % SpeedCycle.Length;
            var multiplier = SpeedCycle[_speedIndex];
            var commandEntity = _world.NewEntity();
            _world.GetPool<SetDeckSpeedCommand>().Add(commandEntity).Multiplier = multiplier;
        }

        private bool _ResolveContent()
        {
            var atlas = StageContent.GetAsset<SpriteAtlas>(_assets, _atlasRequestId);

            if (atlas == null)
            {
                _log.Error("Ace of Shadows atlas address did not resolve to a SpriteAtlas.");
                return false;
            }

            _backgroundSprite = StageContent.ResolveBackground(
                _assets, _backgroundRequestId, DemoName, _log, out _ownsBackgroundSprite);

            if (_backgroundSprite == null)
                return false;

            if (atlas.spriteCount != _atlasSprites.Length)
            {
                _log.Error($"Ace of Shadows atlas contains {atlas.spriteCount} sprite(s); expected {_atlasSprites.Length}.");
                _DestroySpriteCopies();
                StageContent.DestroyOwnedSprite(ref _backgroundSprite, ref _ownsBackgroundSprite);
                return false;
            }

            var spriteCount = atlas.GetSprites(_atlasSprites);

            if (spriteCount != atlas.spriteCount)
            {
                _log.Error($"Ace of Shadows atlas returned {spriteCount} of {atlas.spriteCount} sprite(s).");
                _DestroySpriteCopies();
                StageContent.DestroyOwnedSprite(ref _backgroundSprite, ref _ownsBackgroundSprite);
                return false;
            }

            var faceIndex = 0;
            foreach (var sprite in _atlasSprites)
            {
                var spriteName = sprite.name.Replace("(Clone)", string.Empty).Trim();

                if (spriteName == BackSpriteName)
                    _cardBack = sprite;
                else if (faceIndex < _faces.Length)
                    _faces[faceIndex++] = sprite;
            }

            if (_cardBack == null || faceIndex != FaceCount)
            {
                _log.Error($"Ace of Shadows atlas is missing '{BackSpriteName}' or one of {FaceCount} faces.");
                _DestroySpriteCopies();
                StageContent.DestroyOwnedSprite(ref _backgroundSprite, ref _ownsBackgroundSprite);
                return false;
            }

            Array.Sort(_faces, (left, right) => string.CompareOrdinal(left.name, right.name));
            return true;
        }

        private void _RecalculateLayout()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            _camera = StageContent.FitBackground(_camera, _aosScreen.Background.transform,
                _backgroundSprite, DemoName, _log, out var orthographicSize);
            _layout.Recalculate(_screenWidth, _screenHeight, orthographicSize);
        }

        private void _Teardown(bool resetDeck)
        {
            if (_state == StageState.Idle && _aosScreen == null && _atlasRequestId == 0 &&
                _backgroundRequestId == 0 && _channel.Views.Count == 0)
                return;

            _stageReady.ClearDemo();
            _cardMovePlayer.KillTweensFor(_channel.Handles);

            if (resetDeck)
                _world.GetPool<ResetDeckCommand>().Add(_world.NewEntity());

            foreach (var handle in _channel.Handles)
                _views.Unregister(handle);

            foreach (var cardView in _channel.Views)
                if (cardView != null)
                    UnityEngine.Object.Destroy(cardView.gameObject);

            _channel.Clear();
            _DestroySpriteCopies();
            StageContent.DestroyOwnedSprite(ref _backgroundSprite, ref _ownsBackgroundSprite);

            if (_aosScreen != null)
            {
                _aosScreen.OnSpeedButtonPressed -= _OnRequestSpeedChange;
                _aosScreen.Background.sprite = null;
                var speedButtonImage = _aosScreen.SpeedButtonImage;

                if (speedButtonImage != null)
                    speedButtonImage.sprite = null;
            }

            _ReleaseRequests();

            _aosScreen = null;
            _camera = null;
            _contentReady = false;
            _speedRequested = false;
            _speedIndex = 0;
            _screenWidth = -1;
            _screenHeight = -1;
            _TransitionTo(StageState.Idle);
        }

        private void _DestroySpriteCopies()
        {
            foreach (var sprite in _atlasSprites)
                if (sprite != null)
                    UnityEngine.Object.Destroy(sprite);

            Array.Clear(_atlasSprites, 0, _atlasSprites.Length);
            Array.Clear(_faces, 0, _faces.Length);
            _cardBack = null;
        }

        private void _ReleaseRequests()
        {
            _atlasRequestId = StageContent.Release(_assets, _atlasRequestId);
            _backgroundRequestId = StageContent.Release(_assets, _backgroundRequestId);
        }

        private void _OnRequestSpeedChange()
        {
            _speedRequested = true;
        }

        private void _TransitionTo(StageState next) => _state = next;

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILogService obj) => _log = obj;
        public void Inject(ViewRegistryService obj) => _views = obj;
        public void Inject(StackSlotLayoutService obj) => _layout = obj;
        public void Inject(AddressablesAssetService obj) => _assets = obj;
        public void Inject(CardMovePlayerService obj) => _cardMovePlayer = obj;
        public void Inject(SharedUiSprites obj) => _uiSprites = obj;
        public void Inject(CardViewChannel obj) => _channel = obj;
        public void Inject(StageReadyChannel obj) => _stageReady = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;

    }
}
