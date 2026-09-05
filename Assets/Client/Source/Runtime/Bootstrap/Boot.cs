using Client.Adapters.AceOfShadows.Services;
using Client.Adapters.AceOfShadows.Views;
using Client.Adapters.MagicWords.Services;
using Client.Adapters.MagicWords.Views;
using Client.Adapters.PhoenixFlame.Views;
using Client.Adapters.Shared.Services;
using Client.Adapters.Shell;
using Client.Adapters.Shell.Views;
using Client.Simulation.AceOfShadows;
using Client.Simulation.Core.Navigation;
using Client.Simulation.Core.Phases;
using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords;
using Client.Simulation.MagicWords.Ports;
using Client.Simulation.PhoenixFlame;
using DCFApixels.DragonECS;
using UnityEngine;
// Both halves of a feature declare a module and the two share the feature's name, so every one of
// them is aliased by half. Aliasing rather than qualifying at the call keeps the import list below
// one line per feature-half, and the composition root is the one place that knows every feature by
// name — the ambiguity belongs here and nowhere else.
using AceOfShadowsSimulationModule = Client.Simulation.AceOfShadows.AceOfShadowsModule;
using MagicWordsSimulationModule = Client.Simulation.MagicWords.MagicWordsModule;
using PhoenixFlameSimulationModule = Client.Simulation.PhoenixFlame.PhoenixFlameModule;
using AceOfShadowsAdapterModule = Client.Adapters.AceOfShadows.AceOfShadowsModule;
using MagicWordsAdapterModule = Client.Adapters.MagicWords.MagicWordsModule;
using PhoenixFlameAdapterModule = Client.Adapters.PhoenixFlame.PhoenixFlameModule;

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

        private void Start()
        {
            menuScreen.SetDemos(demos);
            demoHud.SetDemos(demos);

            _sceneService = new SceneLoaderService(new UnityLogService("Scenes"));
            _assetSourceService = new AddressablesAssetService(new UnityLogService("Assets"));
            _dialogueSourceService = new HttpDialogueService(new UnityLogService("Dialogue"));
            _webImagesService = new WebImageLoaderService(new UnityLogService("Avatars.Remote"));
            _atlasImagesService = new AtlasImageLoaderService(
                new UnityLogService("Avatars.Local"), _assetSourceService);
            _avatarImagesService = new AvatarImageRouterService(_atlasImagesService, _webImagesService);
            _viewRegistryService = new ViewRegistryService();

            _world = new EcsWorld();

            // Shared state and behaviour. Systems reach through these instead of holding each other.
            _fadePlayerService = new FadePlayerService();
            _cardMovePlayerService = new CardMovePlayerService(_viewRegistryService);
            // The shell's own views are tracked types too, so its systems resolve them per call
            // instead of taking them through a constructor (DEU0146). The registry scans every
            // scene already open when it is built, and Boot is one of them.
            _screens = new ScreenRegistryService(
                typeof(AceOfShadowsScreen), typeof(MagicWordsScreen), typeof(PhoenixFlameScreen),
                typeof(MenuScreen), typeof(DemoHudView), typeof(ShellSkinView));

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
                .Inject(_screens)

                // A feature ships as a module on both halves, and one line imports each. The order
                // of the Add calls inside an Import is that feature's own decision and lives there,
                // where DEU0136 can read it; the order BETWEEN modules is free, because each system
                // names its phase on its own class line and a runner collects one interface.
                .AddModule(new NavigationModule(new DemoCatalog(_GetDemoAddresses(demos))))
                .AddModule(new AceOfShadowsSimulationModule(aceConfig))
                .AddModule(new MagicWordsSimulationModule(new MagicWordsConfig()))
                .AddModule(new PhoenixFlameSimulationModule(new PhoenixFlameConfig()))

                .AddModule(new AceOfShadowsAdapterModule(aceConfig))
                .AddModule(new MagicWordsAdapterModule())
                .AddModule(new PhoenixFlameAdapterModule())

                // Shell has no simulation half - its counterpart is NavigationModule, put in the
                // shared kernel on purpose. Its systems used to be the one place that held scene
                // references; they resolve them through the screen registry now, so the module
                // carries the demo catalog and nothing else. The serialized fields stay here for
                // SetDemos and OnValidate.
                .AddModule(new ShellModule(demos))
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
        /// No system implements <c>IEcsDestroy</c> any more, so the pipeline goes first only to
        /// stop the phases running; every release right sits with an owner disposed here. The
        /// world goes last, because DragonECS registers worlds globally and a world that outlives
        /// its owner keeps its id and its pools. An owner disposes what it hands out; a system
        /// releases nothing on destroy (adr-data-placement-is-decided-on-three-axes rule 8).
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

            // Reverse construction order: the card mover reads the registry's views to kill their
            // tweens, so it has to go before the registry destroys them.
            _cardMovePlayerService?.Dispose();
            _cardMovePlayerService = null;

            _fadePlayerService?.Dispose();
            _fadePlayerService = null;

            _viewRegistryService?.Dispose();
            _viewRegistryService = null;

            _screens?.Dispose();
            _screens = null;

            _world?.Destroy();
            _world = null;
        }

        // Properties are used only for tests
        internal EcsWorld World => _world;
        internal ViewRegistryService Views => _viewRegistryService;
        internal AddressablesAssetService Assets => _assetSourceService;
        internal AvatarImageRouterService Avatars => _avatarImagesService;
        internal FadePlayerService Fades => _fadePlayerService;

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
