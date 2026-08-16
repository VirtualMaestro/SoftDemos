using System.Collections.Generic;
using Client.Adapters.Services;
using Client.Adapters.Shared;
using Client.Adapters.Views;
using Client.Simulation.Ports;
using DCFApixels.DragonECS;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace Client.Adapters.Systems
{
    /// <summary>Paints the persistent shell with content loaded by address.</summary>
    /// <remarks>
    /// The lifecycle is <c>Idle -> Loading -> Ready</c>. There is no <c>Closing</c>, because
    /// <c>Boot</c> stays loaded. <c>Ready</c> is terminal: a failed load is reported once and
    /// not retried. Every sprite from <see cref="SpriteAtlas.GetSprite"/> is a copy this system
    /// owns and must destroy in <see cref="Destroy"/>.
    /// </remarks>
    public sealed class ShellStageSystem : IEcsLateRun, IEcsDestroy, IEcsInject<ILog>,
        IEcsInject<AddressablesAssetService>, IEcsInject<SharedUiSprites>,
        IEcsInject<StageReadyChannel>
    {
        /// <summary>How many Addressables requests the shell keeps open for the whole session.</summary>
        /// <remarks>
        /// The sprite copies are backed by the atlas texture. Releasing the handles would unload it.
        /// This count is the floor a leak check returns to, not zero.
        /// </remarks>
        public const int AddressCount = 3;

        private const string BackgroundAddress = "art/menu/background";
        private const string MenuAtlasAddress = "art/menu/ui-atlas";
        private const string SharedAtlasAddress = "art/shared/ui-atlas";
        private const string PanelSpriteName = "ui-panel";
        private const string ButtonSpriteName = "ui-button";
        private const string BackIconSpriteName = "ui-icon-back";
        private const string SpinnerSpriteName = "ui-loading-spinner";

        private readonly ShellSkinView _skin;
        private readonly DemoEntry[] _demos;
        private readonly List<Sprite> _ownedSprites = new();

        private ILog _log;
        private AddressablesAssetService _assets;
        private SharedUiSprites _uiSprites;
        private StageReadyChannel _stageReady;
        private StageState _state;
        private int _backgroundRequestId;
        private int _menuAtlasRequestId;
        private int _sharedAtlasRequestId;

        public ShellStageSystem(ShellSkinView skin, DemoEntry[] demos)
        {
            _skin = skin;
            _demos = demos;
        }

        /// <summary>Sprite copies this system is holding. Zero after teardown, or it leaked.</summary>
        public int HeldSpriteCount => _ownedSprites.Count;
        public bool IsReady => _state == StageState.Ready;

        public void LateRun()
        {
            switch (_state)
            {
                case StageState.Idle:
                    _BeginLoading();
                    break;
                case StageState.Loading:
                    _ContinueLoading();
                    break;
            }
        }

        public void Destroy()
        {
            _ClearSpriteTargets();
            _DestroySpriteCopies();
            _ReleaseRequests();
            _TransitionTo(StageState.Idle);
        }

        private void _BeginLoading()
        {
            _backgroundRequestId = _assets.BeginLoad(BackgroundAddress);
            _menuAtlasRequestId = _assets.BeginLoad(MenuAtlasAddress);
            _sharedAtlasRequestId = _assets.BeginLoad(SharedAtlasAddress);
            _TransitionTo(StageState.Loading);
        }

        private void _ContinueLoading()
        {
            var backgroundStatus = _assets.Poll(_backgroundRequestId);
            var menuStatus = _assets.Poll(_menuAtlasRequestId);
            var sharedStatus = _assets.Poll(_sharedAtlasRequestId);

            if (backgroundStatus == AsyncOpStatus.Failed || menuStatus == AsyncOpStatus.Failed ||
                sharedStatus == AsyncOpStatus.Failed)
            {
                _log.Error("Shell skin content failed to load; the menu stays unskinned.");
                _ReleaseRequests();
                // Release to presentation anyway. A plain menu is playable, a hidden one is not.
                // This is the only path that ends with white boxes on screen.
                _stageReady.MarkShellReady();
                _TransitionTo(StageState.Ready);
                return;
            }

            if (backgroundStatus != AsyncOpStatus.Done || menuStatus != AsyncOpStatus.Done ||
                sharedStatus != AsyncOpStatus.Done)
                return;

            if (_TryApplySkin())
                _log.Info($"Shell skin applied from {MenuAtlasAddress}, {SharedAtlasAddress} and {BackgroundAddress}.");

            // The menu has its backdrop, panel, buttons and icons. Presentation can show it now.
            _stageReady.MarkShellReady();
            _TransitionTo(StageState.Ready);
        }

        private bool _TryApplySkin()
        {
            if (_TryResolveAtlas(_menuAtlasRequestId, MenuAtlasAddress, out var menuAtlas) == false ||
                _TryResolveAtlas(_sharedAtlasRequestId, SharedAtlasAddress, out var sharedAtlas) == false)
                return false;

            _ApplyHiddenUntilLoaded(_skin.Background, _CreateBackgroundSprite());
            _skin.Panel.sprite = _TakeSprite(sharedAtlas, PanelSpriteName);
            _ApplyHiddenUntilLoaded(_skin.BackIcon, _TakeSprite(sharedAtlas, BackIconSpriteName));
            _ApplyHiddenUntilLoaded(_skin.Spinner, _TakeSprite(sharedAtlas, SpinnerSpriteName));

            // Share one copy across all buttons. GetSprite allocates a new Sprite on each call.
            // SharedUiSprites lends this copy out. This system stays the owner.
            var buttonSprite = _TakeSprite(sharedAtlas, ButtonSpriteName);
            foreach (var button in _skin.Buttons)
                button.sprite = buttonSprite;

            _uiSprites.Button = buttonSprite;

            var iconCount = Mathf.Min(_skin.DemoIconCount, _demos.Length);
            for (var i = 0; i < iconCount; i++)
                _ApplyHiddenUntilLoaded(_skin.DemoIcons[i], _TakeSprite(menuAtlas, _demos[i].IconName));

            return true;
        }

        /// <summary>Assigns a sprite to an <see cref="Image"/> that starts disabled.</summary>
        /// <remarks>
        /// An <see cref="Image"/> with no sprite draws a white quad. A failed load must leave the
        /// shell plain. Do not route buttons through here: a disabled <see cref="Image"/> loses its
        /// raycast target and stops responding to clicks.
        /// </remarks>
        private static void _ApplyHiddenUntilLoaded(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;

            var fitter = image.GetComponent<AspectRatioFitter>();

            if (fitter == null || sprite == null)
                return;

            // Take the ratio from the loaded image. A constant would crop after a re-export.
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        }

        private bool _TryResolveAtlas(int requestId, string address, out SpriteAtlas atlas)
        {
            atlas = null;
            var handleId = _assets.ResolveHandle(requestId);

            if (_assets.TryGetAsset(handleId, out var asset) == false || asset is not SpriteAtlas resolved)
            {
                _log.Error($"Address '{address}' did not resolve to a {nameof(SpriteAtlas)}.");
                return false;
            }

            atlas = resolved;
            return true;
        }

        private Sprite _TakeSprite(SpriteAtlas atlas, string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                _log.Error($"An empty sprite name was requested from atlas '{atlas.name}'.");
                return null;
            }

            var sprite = atlas.GetSprite(spriteName);

            if (sprite == null)
            {
                _log.Error($"Atlas '{atlas.name}' is missing sprite '{spriteName}'.");
                return null;
            }

            // GetSprite returns a copy named "<name>(Clone)". The atlas does not keep it.
            sprite.name = spriteName;
            _ownedSprites.Add(sprite);
            return sprite;
        }

        /// <summary>Resolves the menu backdrop, which can load as a <see cref="Sprite"/> or a <see cref="Texture2D"/>.</summary>
        /// <remarks>The backdrop is a standalone image, not an atlas entry. Its importer decides the type.</remarks>
        private Sprite _CreateBackgroundSprite()
        {
            var handleId = _assets.ResolveHandle(_backgroundRequestId);

            if (_assets.TryGetAsset(handleId, out var asset) == false)
            {
                _log.Error($"Address '{BackgroundAddress}' did not resolve.");
                return null;
            }

            if (asset is Sprite sprite)
                return sprite;

            if (asset is not Texture2D texture)
            {
                _log.Error($"Address '{BackgroundAddress}' resolved as {asset.GetType().Name}, " +
                    "expected Sprite or Texture2D.");
                return null;
            }

            var created = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            created.name = texture.name;
            _ownedSprites.Add(created);
            return created;
        }

        private void _ClearSpriteTargets()
        {
            _uiSprites.Button = null;

            if (_skin == null)
                return;

            _ClearSprite(_skin.Background, true);
            _ClearSprite(_skin.Panel, false);
            _ClearSprite(_skin.BackIcon, true);
            _ClearSprite(_skin.Spinner, true);

            if (_skin.Buttons != null)
                foreach (var button in _skin.Buttons)
                    _ClearSprite(button, false);

            if (_skin.DemoIcons != null)
                foreach (var icon in _skin.DemoIcons)
                    _ClearSprite(icon, true);
        }

        private void _DestroySpriteCopies()
        {
            foreach (var sprite in _ownedSprites)
                if (sprite != null)
                    Object.Destroy(sprite);

            _ownedSprites.Clear();
        }

        private void _ReleaseRequests()
        {
            if (_backgroundRequestId != 0)
                _assets.Release(_backgroundRequestId);

            if (_menuAtlasRequestId != 0)
                _assets.Release(_menuAtlasRequestId);

            if (_sharedAtlasRequestId != 0)
                _assets.Release(_sharedAtlasRequestId);

            _backgroundRequestId = 0;
            _menuAtlasRequestId = 0;
            _sharedAtlasRequestId = 0;
        }

        private void _TransitionTo(StageState next)
        {
            if (_state == next)
                return;

            _log.Info($"Shell stage: {_state} -> {next}.");
            _state = next;
        }

        private static void _ClearSprite(Image image, bool disable)
        {
            if (image == null)
                return;

            image.sprite = null;

            if (disable)
                image.enabled = false;
        }

        public void Inject(ILog obj) => _log = obj;
        public void Inject(AddressablesAssetService obj) => _assets = obj;
        public void Inject(SharedUiSprites obj) => _uiSprites = obj;
        public void Inject(StageReadyChannel obj) => _stageReady = obj;

        private enum StageState
        {
            Idle,
            Loading,
            Ready,
        }
    }
}
