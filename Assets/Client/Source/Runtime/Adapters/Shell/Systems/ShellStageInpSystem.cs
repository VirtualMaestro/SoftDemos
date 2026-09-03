using System.Runtime.CompilerServices;
using Client.Simulation.Core.Phases;
using Client.Adapters.Shared.Components;
using Client.Adapters.Shared.Services;
using Client.Adapters.Shared.Stage;
using Client.Adapters.Shell.Views;
using Client.Simulation.Core.Ports;
using Client.Simulation.Core.Ports.Requests;
using DCFApixels.DragonECS;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.Shell.Systems
{
    /// <summary>Paints the persistent shell with content loaded by address.</summary>
    /// <remarks>
    /// The lifecycle is <c>Idle -> Loading -> Ready</c>. There is no <c>Closing</c>, because
    /// <c>Boot</c> stays loaded. <c>Ready</c> is terminal: a failed load is reported once and
    /// not retried.
    /// <para>Every sprite it paints with is cut out of an atlas by
    /// <see cref="AddressablesAssetService"/> and owned by it, under an id of its own: releasing
    /// the atlas at <see cref="Destroy"/> destroys them all, which is what the hand-written list of
    /// owned copies used to do. The skin view comes from the screen registry per call, and the one
    /// id a demo may draw with crosses to it as <see cref="ShellSkinComp"/>
    /// (adr-an-engine-object-has-one-owner-per-kind, DEU0146).</para>
    /// </remarks>
    internal sealed class ShellStageInpSystem : IEcsInput, IEcsDestroy, IEcsInject<EcsWorld>,
        IEcsInject<ILogService>, IEcsInject<AddressablesAssetService>,
        IEcsInject<ScreenRegistryService>
    {
        /// <summary>How many Addressables requests the shell keeps open for the whole session.</summary>
        /// <remarks>
        /// The sprites cut from those atlases are backed by their textures. Releasing the handles
        /// would unload them. This count is the floor a leak check on OPEN REQUESTS returns to, not
        /// zero; the held-asset count sits above it by the number of cuts still live.
        /// </remarks>
        public const int AddressCount = 3;

        private const string BackgroundAddress = "art/menu/background";
        private const string MenuAtlasAddress = "art/menu/ui-atlas";
        private const string SharedAtlasAddress = "art/shared/ui-atlas";
        private const string PanelSpriteName = "ui-panel";
        private const string ButtonSpriteName = "ui-button";
        private const string BackIconSpriteName = "ui-icon-back";
        private const string SpinnerSpriteName = "ui-loading-spinner";

        private readonly DemoEntry[] _demos;

        private EcsWorld _world;
        private ILogService _log;
        private AddressablesAssetService _assets;
        private ScreenRegistryService _screens;
        private StageState _state;
        private int _backgroundRequestId;
        private int _menuAtlasRequestId;
        private int _sharedAtlasRequestId;

        public ShellStageInpSystem(DemoEntry[] demos)
        {
            _demos = demos;
        }

        public void Input()
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
            // Releasing the atlases destroys every sprite cut from them: the asset service owns
            // each cut and takes it with the parent.
            _ReleaseRequests();
            _TransitionTo(StageState.Idle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _BeginLoading()
        {
            _backgroundRequestId = _assets.Request(new AssetLoadRequest(BackgroundAddress));
            _menuAtlasRequestId = _assets.Request(new AssetLoadRequest(MenuAtlasAddress));
            _sharedAtlasRequestId = _assets.Request(new AssetLoadRequest(SharedAtlasAddress));
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
                _world.GetPool<ShellReadyTag>().Add(_world.NewEntity());
                _TransitionTo(StageState.Ready);
                return;
            }

            if (backgroundStatus != AsyncOpStatus.Done || menuStatus != AsyncOpStatus.Done ||
                sharedStatus != AsyncOpStatus.Done)
                return;

            _TryApplySkin();

            // The menu has its backdrop, panel, buttons and icons. Presentation can show it now.
            _world.GetPool<ShellReadyTag>().Add(_world.NewEntity());
            _TransitionTo(StageState.Ready);
        }

        private void _TryApplySkin()
        {
            if (!_screens.TryGet(out ShellSkinView skin))
            {
                _log.Error("The shell skin view is not in the Boot scene; the menu stays unskinned.");
                return;
            }

            var backgroundId = _assets.ResolveSprite(_backgroundRequestId);

            _ApplyHiddenUntilLoaded(skin.Background, _Sprite(backgroundId));
            skin.Panel.sprite = _Sprite(_TakeSpriteId(_sharedAtlasRequestId, PanelSpriteName));

            _ApplyHiddenUntilLoaded(
                skin.BackIcon, _Sprite(_TakeSpriteId(_sharedAtlasRequestId, BackIconSpriteName)));

            _ApplyHiddenUntilLoaded(
                skin.Spinner, _Sprite(_TakeSpriteId(_sharedAtlasRequestId, SpinnerSpriteName)));

            // One cut across all buttons, and across every demo that wants the same look: the id
            // goes into the world and a demo resolves it through the same owner.
            var buttonId = _TakeSpriteId(_sharedAtlasRequestId, ButtonSpriteName);
            var buttonSprite = _Sprite(buttonId);

            foreach (var button in skin.Buttons)
                button.sprite = buttonSprite;

            _world.Get<ShellSkinComp>().Button = buttonId;

            var iconCount = Mathf.Min(skin.DemoIconCount, _demos.Length);

            for (var i = 0; i < iconCount; i++)
                _ApplyHiddenUntilLoaded(
                    skin.DemoIcons[i], _Sprite(_TakeSpriteId(_menuAtlasRequestId, _demos[i].IconName)));
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

        /// <summary>
        /// Cuts one sprite out of an atlas under an id of its own, which the asset service owns.
        /// </summary>
        /// <remarks>
        /// There is no separate "is this address an atlas" check any more: a request that is not
        /// one hands back 0 here with the cause in the log, and this is where the shell says which
        /// sprite it could not get.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int _TakeSpriteId(int atlasRequestId, string spriteName)
        {
            var derivedId = _assets.DeriveSprite(atlasRequestId, spriteName);

            if (derivedId == 0)
                _log.Error($"The shell atlas is missing sprite '{spriteName}'.");

            return derivedId;
        }

        /// <summary>The sprite an id names, resolved through its owner and kept by nobody here.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Sprite _Sprite(int requestId) =>
            _assets.TryGetAsset(requestId, out var asset) ? asset as Sprite : null;

        private void _ClearSpriteTargets()
        {
            _world.Get<ShellSkinComp>().Button = 0;

            if (!_screens.TryGet(out ShellSkinView skin))
                return;

            _ClearSprite(skin.Background, true);
            _ClearSprite(skin.Panel, false);
            _ClearSprite(skin.BackIcon, true);
            _ClearSprite(skin.Spinner, true);

            if (skin.Buttons != null)
                foreach (var button in skin.Buttons)
                    _ClearSprite(button, false);

            if (skin.DemoIcons != null)
                foreach (var icon in skin.DemoIcons)
                    _ClearSprite(icon, true);
        }

        private void _ReleaseRequests()
        {
            _assets.Release(ref _backgroundRequestId);
            _assets.Release(ref _menuAtlasRequestId);
            _assets.Release(ref _sharedAtlasRequestId);
        }

        private void _TransitionTo(StageState next) => _state = next;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void _ClearSprite(Image image, bool disable)
        {
            if (image == null)
                return;

            image.sprite = null;

            if (disable)
                image.enabled = false;
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILogService obj) => _log = obj;
        public void Inject(AddressablesAssetService obj) => _assets = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;
    }
}
