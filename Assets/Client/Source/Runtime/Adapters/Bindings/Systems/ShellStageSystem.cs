using System.Collections.Generic;
using DCFApixels.DragonECS;
using Game.Adapters.Services;
using Game.Adapters.Views;
using Game.Simulation.Ports;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace Game.Adapters.Bindings
{
    /// <summary>
    /// Paints the persistent shell with content loaded by address.
    ///
    /// Shaped on <see cref="AceOfShadowsStageSystem"/>, but with a shorter lifecycle:
    /// <c>Idle -> Loading -> Ready</c> and no <c>Closing</c>, because <c>Boot</c> is never
    /// unloaded while the game runs. <c>Ready</c> is terminal for both outcomes — a failed load is
    /// reported once and left alone rather than retried, since a shell retry would relog the same
    /// error every frame for the whole session.
    ///
    /// Every sprite handed out by <see cref="SpriteAtlas.GetSprite"/> is a fresh copy this system
    /// owns; each one is destroyed in <see cref="Destroy"/> or <c>BootSceneSmokeTests</c> sees the
    /// leak grow across load/unload passes.
    /// </summary>
    public sealed class ShellStageSystem : IEcsLateRun, IEcsDestroy, IEcsInject<ILog>
    {
        /// <summary>
        /// How many Addressables requests the shell keeps open for the whole session.
        ///
        /// The sprites are <see cref="SpriteAtlas.GetSprite"/> copies backed by the atlas texture,
        /// so releasing the handles once they are applied would unload the pages underneath them.
        /// The shell therefore holds its three requests until <see cref="Destroy"/> — which makes
        /// this the floor every demo's leak assertion has to return to, not zero.
        /// </summary>
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
        private readonly AddressablesAssetService _assets;
        private readonly SharedUiSprites _uiSprites;
        private readonly List<Sprite> _ownedSprites = new();

        private ILog _log;
        private StageState _state;
        private int _backgroundRequestId;
        private int _menuAtlasRequestId;
        private int _sharedAtlasRequestId;

        public ShellStageSystem(ShellSkinView skin, DemoEntry[] demos,
            AddressablesAssetService assets, SharedUiSprites uiSprites)
        {
            _skin = skin;
            _demos = demos;
            _assets = assets;
            _uiSprites = uiSprites;
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
                _TransitionTo(StageState.Ready);
                return;
            }

            if (backgroundStatus != AsyncOpStatus.Done || menuStatus != AsyncOpStatus.Done ||
                sharedStatus != AsyncOpStatus.Done)
                return;

            if (_TryApplySkin())
                _log.Info($"Shell skin applied from {MenuAtlasAddress}, {SharedAtlasAddress} and {BackgroundAddress}.");

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

            // One copy shared by every button: GetSprite allocates a new Sprite per call, and four
            // identical copies would be four objects to destroy for no visible difference. The
            // same copy is lent out through SharedUiSprites; this system stays its owner.
            var buttonSprite = _TakeSprite(sharedAtlas, ButtonSpriteName);
            foreach (var button in _skin.Buttons)
                button.sprite = buttonSprite;

            _uiSprites.Button = buttonSprite;

            var iconCount = Mathf.Min(_skin.DemoIconCount, _demos.Length);
            for (var i = 0; i < iconCount; i++)
                _ApplyHiddenUntilLoaded(_skin.DemoIcons[i], _TakeSprite(menuAtlas, _demos[i].IconName));

            return true;
        }

        /// <summary>
        /// Assigns a sprite to an <see cref="Image"/> that ships <c>enabled = false</c>.
        ///
        /// A uGUI <see cref="Image"/> with no sprite draws a white quad, so every target this
        /// system owns and nothing else draws — the buttons and the panel are not routed through
        /// here, because disabling a button's <see cref="Image"/> would also remove its raycast
        /// target and silently kill the click. A failed load therefore leaves the shell plain
        /// rather than covered in white boxes.
        /// </summary>
        private static void _ApplyHiddenUntilLoaded(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;

            var fitter = image.GetComponent<AspectRatioFitter>();

            if (fitter == null || sprite == null)
                return;

            // The fitter's ratio has to match whatever the address resolved to; an authored
            // constant would silently letterbox or crop the day the source image is re-exported.
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

            // GetSprite hands back a copy named "<name>(Clone)"; the atlas keeps nothing of it.
            sprite.name = spriteName;
            _ownedSprites.Add(sprite);
            return sprite;
        }

        /// <summary>
        /// The menu backdrop ships as a standalone image rather than an atlas entry, so it may
        /// resolve as either a <see cref="Sprite"/> or the raw <see cref="Texture2D"/> depending on
        /// its importer. The <see cref="Sprite.Create"/> branch is owned exactly like an atlas copy.
        /// </summary>
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

        private enum StageState
        {
            Idle,
            Loading,
            Ready,
        }
    }
}
