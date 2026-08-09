using DCFApixels.DragonECS;
using Game.Adapters;
using Game.Adapters.Bindings;
using Game.Adapters.Services;
using Game.Adapters.Views;
using Game.Simulation.AceOfShadows;
using Game.Simulation.MagicWords;
using Game.Simulation.Menu;
using Game.Simulation.PhoenixFlame;
using Game.Simulation.Ports;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Bootstrap
{
    public class EntryPoint : MonoBehaviour
    {
        // Serialized fields use no leading underscore. The inspector shows the field name.
        [SerializeField] private MenuScreen menuScreen;
        [FormerlySerializedAs("demoHudScreen")]
        [SerializeField] private DemoHudView demoHud;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private ShellSkinView shellSkin;
        [SerializeField] private DemoEntry[] demos;

        private EcsWorld _world;
        private EcsPipeline _pipeline;
        private ILog _log;
        private SceneLoaderService _sceneService;
        private AddressablesAssetService _assetSource;
        private HttpDialogueService _dialogueSource;
        private WebImageLoaderService _webImages;
        private AtlasImageLoaderService _atlasImages;
        private AvatarImageRouterService _avatarImages;
        private ViewRegistry _viewRegistry;
        private TweenPlayer _tweenPlayer;
        private StageReadyChannel _stageReady;

        public EcsWorld World => _world;
        public ViewRegistry Views => _viewRegistry;
        public AddressablesAssetService Assets => _assetSource;
        public AvatarImageRouterService Avatars => _avatarImages;
        public TweenPlayer TweenPlayer => _tweenPlayer;
        public StageReadyChannel StageReady => _stageReady;

        private void Start()
        {
            _log = new UnityLogService("Bootstrap");

            if (_HasEveryInspectorReference() == false)
                return;

            var timeSource = new UnityTimeService();
            var randomSource = new UnityRandomService();
            _sceneService = new SceneLoaderService(new UnityLogService("Scenes"));
            _assetSource = new AddressablesAssetService(new UnityLogService("Assets"));
            _dialogueSource = new HttpDialogueService(new UnityLogService("Dialogue"));
            _webImages = new WebImageLoaderService(new UnityLogService("Avatars.Remote"));
            _atlasImages = new AtlasImageLoaderService(new UnityLogService("Avatars.Local"));
            _avatarImages = new AvatarImageRouterService(
                _atlasImages, _webImages, new UnityLogService("Avatars"));

            _viewRegistry = new ViewRegistry();
            var slotLayout = new StackSlotLayout();
            var aceConfig = new AceOfShadowsConfig();

            // Make the world before the collaborators. TweenPlayer clears move state from its
            // pools when it kills a tween.
            _world = new EcsWorld();

            // Shared state and behaviour. Systems reach through these instead of holding each other.
            _tweenPlayer = new TweenPlayer(_world, _viewRegistry, new UnityLogService("Tweens"));
            var uiSprites = new SharedUiSprites();
            var cardChannel = new CardViewChannel();
            var dialogueChannel = new DialogueLogChannel();
            _stageReady = new StageReadyChannel();

            var tweenPlayback = new TweenPlaybackSystem(slotLayout, _tweenPlayer);
            var shellStage = new ShellStageSystem(shellSkin, demos, _assetSource, uiSprites);
            var aceStage = new AceOfShadowsStageSystem(aceConfig, _viewRegistry, slotLayout,
                _assetSource, _tweenPlayer, uiSprites, cardChannel, _stageReady);
            var cardBinding = new CardBindingSystem(_viewRegistry, slotLayout, cardChannel);
            var deckHud = new DeckHudSystem(slotLayout);
            var dialogueLog = new DialogueLogSystem(_avatarImages, _tweenPlayer, dialogueChannel);
            var magicWordsStage = new MagicWordsStageSystem(_assetSource, _atlasImages,
                _avatarImages, dialogueChannel, _tweenPlayer, _stageReady);
            var phoenixFlameStage = new PhoenixFlameStageSystem(_assetSource, _stageReady);

            _pipeline = EcsPipeline.New()
                .Inject(_world)
                .Inject<ITimeService>(timeSource)
                .Inject<IRandomService>(randomSource)
                .Inject<ILog>(new UnityLogService("Simulation"))
                .Inject<ISceneService>(_sceneService)
                .Inject<IAssetService>(_assetSource)
                .Inject<IDialogueService>(_dialogueSource)
                .Inject<IImageLoadService>(_avatarImages)
                .Inject<ViewRegistry>(_viewRegistry)

                .AddModule(new MenuModule(new DemoCatalog(_GetDemoAddresses(demos))))
                .AddModule(new AceOfShadowsModule(aceConfig))
                .AddModule(new MagicWordsModule(new MagicWordsConfig()))
                .AddModule(new PhoenixFlameModule(new PhoenixFlameConfig()))

                .Add(aceStage)
                .Add(cardBinding)
                .Add(deckHud)
                .Add(magicWordsStage)
                .Add(dialogueLog)
                .Add(phoenixFlameStage)
                .Add(tweenPlayback)
                .Add(shellStage)
                .Add(new ScreenPresentationSystem(
                    menuScreen, demoHud, loadingIndicator, shellSkin, _tweenPlayer, _stageReady))
                .BuildAndInit();

            menuScreen.Bind(_world, demos);
            demoHud.Bind(_world, demos);

            _log.Info($"World and pipeline built. Live worlds: {EcsWorld.AllWorldsCount}.");
        }

        private bool _HasEveryInspectorReference()
        {
            var isComplete = true;

            if (menuScreen == null)
            {
                _log.Error($"{nameof(menuScreen)} is not assigned on EntryPoint.");
                isComplete = false;
            }

            if (demoHud == null)
            {
                _log.Error($"{nameof(demoHud)} is not assigned on EntryPoint.");
                isComplete = false;
            }

            if (loadingIndicator == null)
            {
                _log.Error($"{nameof(loadingIndicator)} is not assigned on EntryPoint.");
                isComplete = false;
            }

            if (shellSkin == null)
            {
                _log.Error($"{nameof(shellSkin)} is not assigned on EntryPoint.");
                isComplete = false;
            }
            else if (shellSkin.HasEveryReference(_log) == false)
                isComplete = false;

            if (demos == null || demos.Length == 0)
            {
                _log.Error($"{nameof(demos)} is empty on EntryPoint; the menu would open nothing.");
                return false;
            }

            for (var i = 0; i < demos.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(demos[i]?.Address))
                {
                    _log.Error($"{nameof(demos)}[{i}] has no addressable scene address.");
                    isComplete = false;
                }

                if (string.IsNullOrWhiteSpace(demos[i]?.IconName))
                {
                    _log.Error($"{nameof(demos)}[{i}] has no atlas icon name; its menu button would stay blank.");
                    isComplete = false;
                }
            }

            // The buttons and the catalog share one order. A mismatch labels the wrong button or
            // reads past the end of the list.
            if (isComplete && menuScreen.ButtonCount != demos.Length)
            {
                _log.Error($"MenuScreen has {menuScreen.ButtonCount} button(s) but {nameof(demos)} " +
                           $"holds {demos.Length} entries. They must match.");
                isComplete = false;
            }

            // demoIcons[i] comes from demos[i].IconName, so these two share one order too.
            // A different count leaves at least one button with a blank or old icon.
            if (isComplete && shellSkin.DemoIconCount != demos.Length)
            {
                _log.Error($"{nameof(shellSkin)} exposes {shellSkin.DemoIconCount} demo icon(s) but " +
                           $"{nameof(demos)} holds {demos.Length} entries. They must match.");
                isComplete = false;
            }

            return isComplete;
        }

        private static string[] _GetDemoAddresses(DemoEntry[] demoList)
        {
            var addresses = new string[demoList.Length];
            for (var i = 0; i < demoList.Length; i++)
                addresses[i] = demoList[i].Address;

            return addresses;
        }

        private void Update()
        {
            _pipeline?.Run();
        }

        private void FixedUpdate()
        {
            _pipeline?.FixedRun();
        }

        private void LateUpdate()
        {
            _pipeline?.LateRun();
        }

        /// <summary>Tears down in this order: pipeline, then ports, then world.</summary>
        /// <remarks>
        /// <c>IEcsDestroy</c> handlers run inside <see cref="EcsPipeline.Destroy"/> and one of
        /// them can still call a port, so the systems must stop before the ports do. The world
        /// goes last, because DragonECS registers worlds globally and a world that outlives its
        /// owner keeps its id and its pools.
        /// </remarks>
        private void OnDestroy()
        {
            _pipeline?.Destroy();
            _pipeline = null;

            _sceneService?.Dispose();
            _sceneService = null;

            _assetSource?.Dispose();
            _assetSource = null;

            _dialogueSource?.Dispose();
            _dialogueSource = null;

            _avatarImages?.Dispose();
            _avatarImages = null;
            _atlasImages = null;
            _webImages = null;

            _viewRegistry = null;
            _tweenPlayer = null;
            _stageReady = null;

            _world?.Destroy();
            _world = null;

            _log?.Info($"World, pipeline and ports destroyed. Live worlds: {EcsWorld.AllWorldsCount}.");
            _log = null;
        }
    }
}
