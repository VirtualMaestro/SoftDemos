using Client.Simulation.Core.Ports;
using Client.Simulation.Menu;
using Client.Simulation.Menu.Components;
using Client.Simulation.Tests.Fakes;
using DCFApixels.DragonECS;
using NUnit.Framework;

namespace Client.Simulation.Tests.Menu
{
    public sealed class NavigationSystemTests
    {
        private static readonly string[] Addresses =
        {
            "scenes/ace-of-shadows",
            "scenes/magic-words",
            "scenes/phoenix-flame",
        };

        private EcsWorld _world;
        private EcsPipeline _pipeline;
        private FakeSceneService _scenes;
        private FakeLog _log;

        [SetUp]
        public void SetUp()
        {
            _world = new EcsWorld();
            _scenes = new FakeSceneService { CompleteAfterPolls = 2 };
            _log = new FakeLog();
            _pipeline = EcsPipeline.New()
                .Inject(_world)
                .Inject<ISceneService>(_scenes)
                .Inject<ILog>(_log)
                .AddModule(new MenuModule(new DemoCatalog(Addresses)))
                .BuildAndInit();
        }

        [TearDown]
        public void TearDown()
        {
            _pipeline?.Destroy();
            _pipeline = null;

            _world?.Destroy();
            _world = null;
        }

        [Test]
        public void OpenThenClose_CompletesBothRequestsAndReturnsToMenu()
        {
            _scenes.TerminalStatus = AsyncOpStatus.Done;

            _Open(1);
            _pipeline.Run();
            _pipeline.Run();

            ref var opened = ref _world.Get<ScreenStateComp>();
            Assert.That(opened.Current, Is.EqualTo(ScreenId.Demo), $"{opened}");
            Assert.That(opened.ActiveDemoIndex, Is.EqualTo(1), $"{opened}");

            _Close();
            _pipeline.Run();
            _pipeline.Run();

            ref var closed = ref _world.Get<ScreenStateComp>();
            Assert.That(closed.Current, Is.EqualTo(ScreenId.Menu), $"{closed}");
            Assert.That(closed.ActiveDemoIndex, Is.EqualTo(-1), $"{closed}");
            Assert.That(_scenes.OpenRequestCount, Is.Zero, $"{_scenes}");
            Assert.That(_scenes.LoadCalls, Is.EqualTo(new[] { Addresses[1] }), $"{_scenes}");
            Assert.That(_scenes.UnloadCalls, Is.EqualTo(new[] { Addresses[1] }), $"{_scenes}");
        }

        [Test]
        public void FailedLoad_ReturnsToMenuAndTheNextValidOpenClearsTheFailure()
        {
            _scenes.CompleteAfterPolls = 1;
            _scenes.TerminalStatus = AsyncOpStatus.Failed;

            _Open(0);
            _pipeline.Run();

            ref var failed = ref _world.Get<ScreenStateComp>();
            Assert.That(failed.Current, Is.EqualTo(ScreenId.Menu), $"{failed}");
            Assert.That(failed.LastOperationFailed, Is.True, $"{failed}");
            Assert.That(_log.CountOf(FakeLog.Level.Error), Is.EqualTo(1), $"{_log}");
            Assert.That(_scenes.OpenRequestCount, Is.Zero, $"{_scenes}");

            _scenes.CompleteAfterPolls = 2;
            _scenes.TerminalStatus = AsyncOpStatus.Done;
            _Open(2);
            _pipeline.Run();

            ref var retrying = ref _world.Get<ScreenStateComp>();
            Assert.That(retrying.Current, Is.EqualTo(ScreenId.Loading), $"{retrying}");
            Assert.That(retrying.LastOperationFailed, Is.False, $"{retrying}");

            _pipeline.Run();
            Assert.That(_scenes.OpenRequestCount, Is.Zero, $"{_scenes}");
        }

        [Test]
        public void FailedUnload_StillReturnsToMenuAndReleasesTheRequest()
        {
            _scenes.CompleteAfterPolls = 1;
            _scenes.TerminalStatus = AsyncOpStatus.Done;
            _Open(0);
            _pipeline.Run();

            _scenes.TerminalStatus = AsyncOpStatus.Failed;
            _Close();
            _pipeline.Run();

            ref var state = ref _world.Get<ScreenStateComp>();
            Assert.That(state.Current, Is.EqualTo(ScreenId.Menu), $"{state}");
            Assert.That(state.ActiveDemoIndex, Is.EqualTo(-1), $"{state}");
            Assert.That(state.LastOperationFailed, Is.True, $"{state}");
            Assert.That(_log.CountOf(FakeLog.Level.Error), Is.EqualTo(1), $"{_log}");
            Assert.That(_scenes.OpenRequestCount, Is.Zero, $"{_scenes}");
        }

        [Test]
        public void SecondOpenDuringLoading_IsConsumedWithoutStartingAnotherRequest()
        {
            _scenes.CompleteAfterPolls = 3;
            _scenes.TerminalStatus = AsyncOpStatus.Done;
            _Open(0);
            _pipeline.Run();

            var secondCommand = _Open(1);
            _pipeline.Run();

            ref var state = ref _world.Get<ScreenStateComp>();
            Assert.That(state.Current, Is.EqualTo(ScreenId.Loading), $"{state}");
            Assert.That(_world.GetPool<OpenDemoCommand>().Has(secondCommand), Is.False, $"{state}");
            Assert.That(_scenes.LoadCalls, Has.Count.EqualTo(1), $"{_scenes}");

            _pipeline.Run();
            Assert.That(_scenes.OpenRequestCount, Is.Zero, $"{_scenes}");
        }

        [Test]
        public void OutOfRangeIndex_IsConsumedWithoutChangingState()
        {
            var command = _Open(Addresses.Length);

            _pipeline.Run();

            ref var state = ref _world.Get<ScreenStateComp>();
            Assert.That(state.Current, Is.EqualTo(ScreenId.Menu), $"{state}");
            Assert.That(_world.GetPool<OpenDemoCommand>().Has(command), Is.False, $"{state}");
            Assert.That(_scenes.LoadCalls, Is.Empty, $"{_scenes}");
            Assert.That(_log.CountOf(FakeLog.Level.Error), Is.EqualTo(1), $"{_log}");
            Assert.That(_scenes.OpenRequestCount, Is.Zero, $"{_scenes}");
        }

        private int _Open(int demoIndex)
        {
            var entityId = _world.NewEntity();
            _world.GetPool<OpenDemoCommand>().Add(entityId).DemoIndex = demoIndex;
            return entityId;
        }

        private void _Close()
        {
            var entityId = _world.NewEntity();
            _world.GetPool<CloseDemoCommand>().Add(entityId);
        }
    }
}
