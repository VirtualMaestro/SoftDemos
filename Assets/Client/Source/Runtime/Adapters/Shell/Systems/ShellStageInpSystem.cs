using System.Runtime.CompilerServices;
using Client.Simulation.Core.Phases;
using Client.Adapters.Shared.Components;
using Client.Adapters.Shell.Components;
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
    /// not retried - which is why a SETTLED load, successful or not, is handed to
    /// <see cref="StageTransitions.Next"/> as <see cref="AsyncOpStatus.Done"/>. A plain menu is
    /// playable and a hidden one is not, so for this stage a failure is not a reason to go back.
    /// <para>Every sprite it paints with is cut out of an atlas by
    /// <see cref="AddressablesAssetService"/> and owned by it, under an id of its own: releasing
    /// the atlas at <see cref="Destroy"/> destroys them all, which is what the hand-written list of
    /// owned copies used to do. The skin view comes from the screen registry per call, and the one
    /// id a demo may draw with crosses to it as <see cref="ShellSkinComp"/>
    /// (adr-an-engine-object-has-one-owner-per-kind, DEU0146).</para>
    /// <para>It owns nothing. The state and the three request ids live in
    /// <see cref="ShellStageComp"/> - a world singleton, because this stage lives as long as the
    /// world does and has no birth or death for an entity to model. There is no
    /// <c>IEcsDestroy</c>: the asset service releases the session's requests when the composition
    /// root disposes it (adr-data-placement-is-decided-on-three-axes rules 6 and 8).</para>
    /// </remarks>
    internal sealed class ShellStageInpSystem : IEcsInput, IEcsInject<EcsWorld>,
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
        private EcsTagPool<ShellReadyTag> _shellReady;

        public ShellStageInpSystem(DemoEntry[] demos)
        {
            _demos = demos;
        }

        public void Input()
        {
            var state = _world.Get<ShellStageComp>().State;

            if (state == StageState.Ready)
                return;

            var status = state == StageState.Loading ? _Poll() : AsyncOpStatus.Pending;

            // A settled load makes the menu presentable either way, so the machine is told Done and
            // the failure is kept for the edge below. The shell is always "present" and never
            // unloading, which is what leaves it with three of the four states.
            var settled = status == AsyncOpStatus.Failed ? AsyncOpStatus.Done : status;
            var next = StageTransitions.Next(state, true, true, false, settled);

            if (next == state)
                return;

            if (next == StageState.Loading)
            {
                _BeginLoading();
                return;
            }

            if (status == AsyncOpStatus.Failed)
            {
                _log.Error("Shell skin content failed to load; the menu stays unskinned.");
                _ReleaseRequests();
                // Release to presentation anyway. A plain menu is playable, a hidden one is not.
                // This is the only path that ends with white boxes on screen.
            }
            else
            {
                _TryApplySkin();
            }

            // The menu has its backdrop, panel, buttons and icons. Presentation can show it now.
            _shellReady.Add(_world.NewEntity());
            _world.Get<ShellStageComp>().State = StageState.Ready;
        }

        /// <summary>The worst of the three content requests: a failure first, then a wait.</summary>
        private AsyncOpStatus _Poll()
        {
            ref readonly var comp = ref _world.Get<ShellStageComp>();
            var backgroundStatus = _assets.Poll(comp.BackgroundRequestId);
            var menuStatus = _assets.Poll(comp.MenuAtlasRequestId);
            var sharedStatus = _assets.Poll(comp.SharedAtlasRequestId);

            if (backgroundStatus == AsyncOpStatus.Failed || menuStatus == AsyncOpStatus.Failed ||
                sharedStatus == AsyncOpStatus.Failed)
                return AsyncOpStatus.Failed;

            return backgroundStatus == AsyncOpStatus.Done && menuStatus == AsyncOpStatus.Done &&
                   sharedStatus == AsyncOpStatus.Done
                ? AsyncOpStatus.Done
                : AsyncOpStatus.Pending;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _BeginLoading()
        {
            ref var comp = ref _world.Get<ShellStageComp>();
            comp.State = StageState.Loading;
            comp.BackgroundRequestId = _assets.Request(new AssetLoadRequest(BackgroundAddress));
            comp.MenuAtlasRequestId = _assets.Request(new AssetLoadRequest(MenuAtlasAddress));
            comp.SharedAtlasRequestId = _assets.Request(new AssetLoadRequest(SharedAtlasAddress));
        }

        private void _TryApplySkin()
        {
            if (!_screens.TryGet(out ShellSkinView skin))
            {
                _log.Error("The shell skin view is not in the Boot scene; the menu stays unskinned.");
                return;
            }

            ref readonly var comp = ref _world.Get<ShellStageComp>();
            var backgroundId = _assets.ResolveSprite(comp.BackgroundRequestId);

            _ApplyHiddenUntilLoaded(skin.Background, _Sprite(backgroundId));
            skin.Panel.sprite = _Sprite(_TakeSpriteId(comp.SharedAtlasRequestId, PanelSpriteName));

            _ApplyHiddenUntilLoaded(
                skin.BackIcon, _Sprite(_TakeSpriteId(comp.SharedAtlasRequestId, BackIconSpriteName)));

            _ApplyHiddenUntilLoaded(
                skin.Spinner, _Sprite(_TakeSpriteId(comp.SharedAtlasRequestId, SpinnerSpriteName)));

            // One cut across all buttons, and across every demo that wants the same look: the id
            // goes into the world and a demo resolves it through the same owner.
            var buttonId = _TakeSpriteId(comp.SharedAtlasRequestId, ButtonSpriteName);
            var buttonSprite = _Sprite(buttonId);

            foreach (var button in skin.Buttons)
                button.sprite = buttonSprite;

            _world.Get<ShellSkinComp>().Button = buttonId;

            var iconCount = Mathf.Min(skin.DemoIconCount, _demos.Length);

            for (var i = 0; i < iconCount; i++)
                _ApplyHiddenUntilLoaded(
                    skin.DemoIcons[i], _Sprite(_TakeSpriteId(comp.MenuAtlasRequestId, _demos[i].IconName)));
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _ReleaseRequests()
        {
            ref var comp = ref _world.Get<ShellStageComp>();
            _assets.Release(ref comp.BackgroundRequestId);
            _assets.Release(ref comp.MenuAtlasRequestId);
            _assets.Release(ref comp.SharedAtlasRequestId);
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _shellReady = obj.GetPool<ShellReadyTag>();
        }

        public void Inject(ILogService obj) => _log = obj;
        public void Inject(AddressablesAssetService obj) => _assets = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;
    }
}
