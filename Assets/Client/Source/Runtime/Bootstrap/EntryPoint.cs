using Client.Adapters;
using Client.Adapters.Services;
using Client.Adapters.Shared;
using Client.Adapters.Systems;
using Client.Adapters.Views;
using Client.Simulation.AceOfShadows;
using Client.Simulation.MagicWords;
using Client.Simulation.Menu;
using Client.Simulation.PhoenixFlame;
using Client.Simulation.Ports;
using DCFApixels.DragonECS;
using UnityEngine;
using UnityEngine.Serialization;

namespace Client.Bootstrap
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
        private AddressablesAssetService _assetSourceService;
        private HttpDialogueService _dialogueSourceService;
        private WebImageLoaderService _webImagesService;
        private AtlasImageLoaderService _atlasImagesService;
        private AvatarImageRouterService _avatarImagesService;
        private ViewRegistryService _viewRegistryService;
        private TweenPlayerService _tweenPlayerService;
        private StageReadyChannel _stageReady;
        private ScreenRegistryService _screens;

        public EcsWorld World => _world;
        public ViewRegistryService Views => _viewRegistryService;
        public AddressablesAssetService Assets => _assetSourceService;
        public AvatarImageRouterService Avatars => _avatarImagesService;
        public TweenPlayerService Tweens => _tweenPlayerService;
        public StageReadyChannel StageReady => _stageReady;

        private void Start()
        {
            _log = new UnityLogService("Bootstrap");

            if (_HasEveryInspectorReference() == false)
                return;

            _sceneService = new SceneLoaderService(new UnityLogService("Scenes"));
            _assetSourceService = new AddressablesAssetService(new UnityLogService("Assets"));
            _dialogueSourceService = new HttpDialogueService(new UnityLogService("Dialogue"));
            _webImagesService = new WebImageLoaderService(new UnityLogService("Avatars.Remote"));
            _atlasImagesService = new AtlasImageLoaderService(new UnityLogService("Avatars.Local"));
            _avatarImagesService = new AvatarImageRouterService(
                _atlasImagesService, _webImagesService, new UnityLogService("Avatars"));
            _viewRegistryService = new ViewRegistryService();

            // Make the world before the collaborators. TweenPlayerService clears move state from its
            // pools when it kills tween.
            _world = new EcsWorld();

            // Shared state and behaviour. Systems reach through these instead of holding each other.
            _tweenPlayerService = new TweenPlayerService(_world, _viewRegistryService, new UnityLogService("Tweens"));
            _stageReady = new StageReadyChannel();
            _screens = new ScreenRegistryService(
                typeof(AceOfShadowsScreen), typeof(MagicWordsScreen), typeof(PhoenixFlameScreen));

            var aceConfig = new AceOfShadowsConfig();

            // Every shared collaborator is injected; a system's constructor carries only what is
            // unique to that instance. See CLAUDE.md, "Composition root".
            _pipeline = EcsPipeline.New()
                .Inject(_world)
                .Inject<ITimeService>(new UnityTimeService())
                .Inject<IRandomService>(new UnityRandomService())
                .Inject<ILog>(new UnityLogService("Simulation"))
                .Inject<ISceneService>(_sceneService)
                .Inject<IDialogueService>(_dialogueSourceService)

                // One instance behind two types: the port for the simulation, the adapter for the
                // stage systems that need engine objects. AddNode declares the second node; a
                // second Inject call cannot, because it finds the branch already registered under
                // the runtime type and skips node creation.
                //
                // Only one implementation of each port may be injected. A branch attaches every
                // node its runtime type is assignable to, so injecting _atlasImages here would
                // also land on the IImageLoadService node and displace the router. The atlas is
                // reached through AvatarImageRouterService instead.
                .Injections.AddNode<IAssetService>().Inject(_assetSourceService)
                .Injections.AddNode<IImageLoadService>().Inject(_avatarImagesService)
                .Inject(_viewRegistryService)
                .Inject(_tweenPlayerService)
                .Inject(new StackSlotLayoutService())
                .Inject(new SharedUiSprites())
                .Inject(_stageReady)
                .Inject(_screens)
                .Inject(new CardViewChannel())
                .Inject(new DialogueLogChannel())

                // The simulation halves are modules because the test fixtures build a headless
                // pipeline from the same ones. Presentation has no such reuse, so it is a plain
                // list; a module around Add(new X()) would only hide the order.
                .AddModule(new MenuModule(new DemoCatalog(_GetDemoAddresses(demos))))
                .AddModule(new AceOfShadowsModule(aceConfig))
                .AddModule(new MagicWordsModule(new MagicWordsConfig()))
                .AddModule(new PhoenixFlameModule(new PhoenixFlameConfig()))

                // Presentation, in run order. These are IEcsLateRun, the simulation is IEcsRun,
                // and a runner only ever collects its own interface — so the two lists above and
                // below never interleave at run time.
                .Add(new AceOfShadowsStageSystem(aceConfig))
                .Add(new CardBindingSystem())
                .Add(new DeckHudSystem())
                .Add(new MagicWordsStageSystem())
                .Add(new DialogueLogSystem())
                .Add(new PhoenixFlameStageSystem())
                .Add(new TweenPlaybackSystem())
                .Add(new ShellStageSystem(shellSkin, demos))
                .Add(new ScreenPresentationSystem(menuScreen, demoHud, loadingIndicator, shellSkin))
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

            _assetSourceService?.Dispose();
            _assetSourceService = null;

            _dialogueSourceService?.Dispose();
            _dialogueSourceService = null;

            _avatarImagesService?.Dispose();
            _avatarImagesService = null;
            _atlasImagesService = null;
            _webImagesService = null;

            _viewRegistryService = null;
            _tweenPlayerService = null;
            _stageReady = null;

            _screens?.Dispose();
            _screens = null;

            _world?.Destroy();
            _world = null;

            _log?.Info($"World, pipeline and ports destroyed. Live worlds: {EcsWorld.AllWorldsCount}.");
            _log = null;
        }
    }
}
