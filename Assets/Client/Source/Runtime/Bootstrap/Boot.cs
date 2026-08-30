using Client.Adapters.AceOfShadows;
using Client.Adapters.AceOfShadows.Services;
using Client.Adapters.AceOfShadows.Systems;
using Client.Adapters.AceOfShadows.Views;
using Client.Adapters.MagicWords;
using Client.Adapters.MagicWords.Services;
using Client.Adapters.MagicWords.Systems;
using Client.Adapters.MagicWords.Views;
using Client.Adapters.PhoenixFlame.Systems;
using Client.Adapters.PhoenixFlame.Views;
using Client.Adapters.Shared.Services;
using Client.Adapters.Shared.Stage;
using Client.Adapters.Shell;
using Client.Adapters.Shell.Systems;
using Client.Adapters.Shell.Views;
using Client.Simulation.AceOfShadows;
using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords.Ports;
using Client.Simulation.MagicWords;
using Client.Simulation.Core.Navigation;
using Client.Simulation.Core.Phases;
using Client.Simulation.PhoenixFlame;
using DCFApixels.DragonECS;
using UnityEngine;

namespace Client.Bootstrap
{
    public class Boot : MonoBehaviour
    {
        // Serialized fields use no leading underscore. The inspector shows the field name.
        [SerializeField] private MenuScreen menuScreen;
        [SerializeField] private DemoHudView demoHud;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private ShellSkinView shellSkin;
        [SerializeField] private DemoEntry[] demos;

        private EcsWorld _world;
        private EcsPipeline _pipeline;
        private SceneLoaderService _sceneService;
        private AddressablesAssetService _assetSourceService;
        private HttpDialogueService _dialogueSourceService;
        private WebImageLoaderService _webImagesService;
        private AtlasImageLoaderService _atlasImagesService;
        private AvatarImageRouterService _avatarImagesService;
        private ViewRegistryService _viewRegistryService;
        private FadePlayerService _fadePlayerService;
        private CardMovePlayerService _cardMovePlayerService;
        private ScreenRegistryService _screens;

        public EcsWorld World => _world;
        public ViewRegistryService Views => _viewRegistryService;
        public AddressablesAssetService Assets => _assetSourceService;
        public AvatarImageRouterService Avatars => _avatarImagesService;
        public FadePlayerService Fades => _fadePlayerService;

        private void Start()
        {
            menuScreen.SetDemos(demos);
            demoHud.SetDemos(demos);

            _sceneService = new SceneLoaderService(new UnityLogService("Scenes"));
            _assetSourceService = new AddressablesAssetService(new UnityLogService("Assets"));
            _dialogueSourceService = new HttpDialogueService(new UnityLogService("Dialogue"));
            _webImagesService = new WebImageLoaderService(new UnityLogService("Avatars.Remote"));
            _atlasImagesService = new AtlasImageLoaderService(new UnityLogService("Avatars.Local"));
            _avatarImagesService = new AvatarImageRouterService(_atlasImagesService, _webImagesService);
            _viewRegistryService = new ViewRegistryService();

            _world = new EcsWorld();

            // Shared state and behaviour. Systems reach through these instead of holding each other.
            _fadePlayerService = new FadePlayerService();
            _cardMovePlayerService = new CardMovePlayerService(_viewRegistryService);
            _screens = new ScreenRegistryService(
                typeof(AceOfShadowsScreen), typeof(MagicWordsScreen), typeof(PhoenixFlameScreen));

            var aceConfig = new AceOfShadowsConfig();

            // Every shared collaborator is injected; a system's constructor carries only what is
            // unique to that instance. See CLAUDE.md, "Composition root".
            _pipeline = EcsPipeline.New()
                .Inject(_world)
                .Inject<ITimeService>(new UnityTimeService())
                .Inject<ILogService>(new UnityLogService("Simulation"))
                .Inject<ISceneService>(_sceneService)
                .Inject<IDialogueService>(_dialogueSourceService)

                .Injections.AddNode<IAssetService>().Inject(_assetSourceService)
                .Injections.AddNode<IImageLoadService>().Inject(_avatarImagesService)
                .Inject(_viewRegistryService)
                .Inject(_fadePlayerService)
                .Inject(_cardMovePlayerService)
                .Inject(new StackSlotLayoutService())
                .Inject(new SharedUiSprites())
                .Inject(_screens)
                .Inject(new CardViewChannel())
                .Inject(new DialogueLogChannel())

                // The simulation halves are modules because the test fixtures build a headless
                // pipeline from the same ones. Presentation has no such reuse, so it is a plain
                // list; a module around Add(new X()) would only hide the order.
                .AddModule(new NavigationModule(new DemoCatalog(_GetDemoAddresses(demos))))
                .AddModule(new AceOfShadowsModule(aceConfig))
                .AddModule(new MagicWordsModule(new MagicWordsConfig()))
                .AddModule(new PhoenixFlameModule(new PhoenixFlameConfig()))

                // The adapter half. Each system says which phase it runs in on its own class line,
                // so this list decides only the order WITHIN a phase — a runner collects one
                // interface and never sees the others, and the phase order is Update/LateUpdate
                // below. Grouped by feature because that is what a reader looks for here.
                .Add(new AceOfShadowsInputSystem(aceConfig))
                .Add(new CardBindingSystem())
                .Add(new DeckHudSystem())
                .Add(new TweenPlaybackSystem())
                .Add(new AceOfShadowsCleanupSystem())

                .Add(new MagicWordsInputSystem())
                .Add(new MagicWordsViewSystem())
                .Add(new DialogueLogSystem())
                .Add(new MagicWordsCleanupSystem())

                .Add(new PhoenixFlameInputSystem())
                .Add(new PhoenixFlameViewSystem())

                .Add(new ShellStageSystem(shellSkin, demos))
                .Add(new ShellInputSystem(menuScreen, demoHud))
                .Add(new ScreenPresentationSystem(menuScreen, demoHud, loadingIndicator, shellSkin))
                .BuildAndInit();
        }

        private static string[] _GetDemoAddresses(DemoEntry[] demoList)
        {
            var addresses = new string[demoList.Length];

            for (var i = 0; i < demoList.Length; i++)
                addresses[i] = demoList[i].Address;

            return addresses;
        }

        /// <summary>The client driver: the frame's phase order, and the only place it is written.</summary>
        /// <remarks>
        /// Input turns the outside world into commands, Sim advances the game, Present draws, and
        /// Cleanup deletes every one-frame component. Cleanup closes the frame rather than opening
        /// it, so a component added in Input or Sim is visible to Present in the same frame and
        /// nothing one frame long ever crosses the frame boundary.
        /// <para>The split across <c>Update</c> and <c>LateUpdate</c> is the engine's business, not
        /// the contract's: drawing after the engine's own animation and physics callbacks is what
        /// <c>LateUpdate</c> is for. A server or a test driver runs <c>Input(); Sim(); Cleanup();</c>
        /// in a loop with no Present, and a realtime one calls <c>Sim()</c> k times against a fixed
        /// timestep. No system can tell which of them is running it.</para>
        /// </remarks>
        private void Update()
        {
            if (_pipeline == null)
                return;

            _pipeline.Input();
            _pipeline.Sim();
        }

        private void LateUpdate()
        {
            if (_pipeline == null)
                return;

            _pipeline.Present();
            _pipeline.Cleanup();
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
            _fadePlayerService = null;
            _cardMovePlayerService = null;

            _screens?.Dispose();
            _screens = null;

            _world?.Destroy();
            _world = null;
        }

        private void OnValidate()
        {
            Debug.Assert(menuScreen != null, $"'{nameof(menuScreen)}' is not assigned on {nameof(Boot)}.", this);
            Debug.Assert(demoHud != null, $"'{nameof(demoHud)}' is not assigned on {nameof(Boot)}.", this);
            Debug.Assert(loadingIndicator != null, $"'{nameof(loadingIndicator)}' is not assigned on {nameof(Boot)}.", this);
            Debug.Assert(shellSkin != null, $"'{nameof(shellSkin)}' is not assigned on {nameof(Boot)}.", this);
            Debug.Assert(demos != null && demos.Length > 0,
                $"'{nameof(demos)}' is empty on {nameof(Boot)}; the menu would open nothing.", this);

            for (var i = 0; demos != null && i < demos.Length; i++)
            {
                Debug.Assert(!string.IsNullOrWhiteSpace(demos[i]?.Address),
                    $"'{nameof(demos)}[{i}]' has no addressable scene address.", this);
                Debug.Assert(!string.IsNullOrWhiteSpace(demos[i]?.IconName),
                    $"'{nameof(demos)}[{i}]' has no atlas icon name; its menu button would stay blank.", this);
            }

            // The buttons and the catalog share one order. A mismatch labels the wrong button or
            // reads past the end of the list.
            if (menuScreen != null && demos != null)
                Debug.Assert(menuScreen.ButtonCount == demos.Length,
                    $"{nameof(MenuScreen)} has {menuScreen.ButtonCount} button(s) but '{nameof(demos)}' holds " +
                    $"{demos.Length} entries. They must match.", this);

            // demoIcons[i] comes from demos[i].IconName, so these two share one order too.
            // A different count leaves at least one button with a blank or old icon.
            if (shellSkin != null && demos != null)
                Debug.Assert(shellSkin.DemoIconCount == demos.Length,
                    $"'{nameof(shellSkin)}' exposes {shellSkin.DemoIconCount} demo icon(s) but '{nameof(demos)}' " +
                    $"holds {demos.Length} entries. They must match.", this);
        }
    }
}
