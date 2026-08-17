using System;
using Client.Simulation.Ports;
using Client.Simulation.Tests.Fakes;
using DCFApixels.DragonECS;
using NUnit.Framework;

namespace Client.Simulation.Tests
{
    /// <summary>
    /// Pins the one mechanism every future system depends on: a port implementation reaching an
    /// <see cref="IEcsInject{T}"/> system through its *interface*.
    ///
    /// The trap this fixture exists for: <c>Injector.Inject&lt;T&gt;</c> creates an injection node
    /// for <c>typeof(T)</c> only (<c>Injector.cs:97-116</c>), and a branch is matched against
    /// existing nodes by <c>nodeType.IsAssignableFrom(objectRuntimeType)</c>
    /// (<c>Injector.cs:184</c>). Write <c>.Inject(new UnityTimeService())</c> and the only node
    /// created is <c>UnityTimeService</c> — no <c>ITimeService</c> node ever exists, and every
    /// <c>IEcsInject&lt;ITimeService&gt;</c> system matches nothing.
    ///
    /// In DEBUG that is loud: <c>InjectionList.InitInjectTo</c> collects every required injection
    /// type and calls <c>Throw.Injection_RequiredNodeNotFound</c> for each unsatisfied one
    /// (<c>Injector.cs:286-301</c>), from inside the <see cref="EcsPipeline"/> constructor
    /// (<c>EcsPipeline.cs:218</c>) — so <c>BuildAndInit()</c> throws. In a
    /// <c>DISABLE_DEBUG</c> player build the same mistake would run with a null port instead.
    /// Hence the rule these tests enforce: always name the interface explicitly.
    /// </summary>
    public sealed class PortInjectionTests
    {
        private EcsWorld _world;
        private EcsPipeline _pipeline;

        [SetUp]
        public void SetUp()
        {
            _world = new EcsWorld();
        }

        /// <summary>
        /// Pipeline first, then world. DragonECS registers worlds globally by id: a world left
        /// alive keeps its id and pools allocated and corrupts every later test (RESEARCH §3.4).
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _pipeline?.Destroy();
            _pipeline = null;

            _world?.Destroy();
            _world = null;
        }

        [Test]
        public void Inject_WithInterfaceGenericArgument_ReachesEveryPort()
        {
            var time = new FakeTime { DeltaSeconds = 0.25f };
            var log = new FakeLog();
            var scenes = new FakeSceneService();
            var assets = new FakeAssetService();
            var dialogue = new FakeDialogueService();
            var images = new FakeImageService();
            var probe = new AllPortsProbeSystem();

            _pipeline = EcsPipeline.New()
                .Inject(_world)
                .Inject<ITimeService>(time)
                .Inject<ILog>(log)
                .Inject<ISceneService>(scenes)
                .Inject<IAssetService>(assets)
                .Inject<IDialogueService>(dialogue)
                .Inject<IImageLoadService>(images)
                .Add(probe)
                .BuildAndInit();

            Assert.That(probe.Time, Is.SameAs(time), $"ITimeService did not arrive. {probe}");
            Assert.That(probe.Log, Is.SameAs(log), $"ILog did not arrive. {probe}");
            Assert.That(probe.Scenes, Is.SameAs(scenes), $"ISceneService did not arrive. {probe}");
            Assert.That(probe.Assets, Is.SameAs(assets), $"IAssetService did not arrive. {probe}");
            Assert.That(probe.Dialogue, Is.SameAs(dialogue), $"IDialogueService did not arrive. {probe}");
            Assert.That(probe.Images, Is.SameAs(images), $"IImageLoadService did not arrive. {probe}");
        }

        [Test]
        public void Inject_WithConcreteTypeOnly_ThrowsAtBuild()
        {
            var time = new FakeTime { DeltaSeconds = 0.25f };

            // No explicit generic argument: T is inferred as FakeTime, so only a FakeTime node
            // is created and the ITimeService requirement stays unsatisfied.
            // Catch, not Throws: the concrete type is DragonECS's InjectionException, and pinning
            // it here would couple the test to a framework internal. The message is the contract.
            var exception = Assert.Catch<Exception>(
                () =>
                {
                    _pipeline = EcsPipeline.New()
                        .Inject(_world)
                        .Inject(time)
                        .Add(new TimeProbeSystem())
                        .BuildAndInit();
                },
                "Injecting the concrete type must not satisfy IEcsInject<ITimeService>. " +
                "If this stops throwing, the guard that makes the mistake visible is gone.");

            Assert.That(exception.Message, Does.Contain(nameof(ITimeService)),
                $"The failure must name the missing port. Actual message: {exception.Message}");
        }

        [Test]
        public void AddNode_ThenInjectConcrete_IsEquivalentToExplicitGeneric()
        {
            var time = new FakeTime { DeltaSeconds = 0.5f };
            var probe = new TimeProbeSystem();

            // The node exists before the injection, so FakeTime's branch picks it up by
            // ITimeService.IsAssignableFrom(FakeTime). Same result, more moving parts —
            // production wiring uses the explicit generic form instead.
            _pipeline = EcsPipeline.New()
                .Inject(_world)
                .Injections.AddNode<ITimeService>()
                .Inject(time)
                .Add(probe)
                .BuildAndInit();

            Assert.That(probe.Time, Is.SameAs(time),
                $"AddNode<ITimeService>() + Inject(impl) must deliver the instance. {probe}");
        }

        [Test]
        public void FakeSceneService_StaysPendingUntilTheConfiguredPollCount()
        {
            var scenes = new FakeSceneService { CompleteAfterPolls = 3, TerminalStatus = AsyncOpStatus.Done };

            var requestId = scenes.BeginLoad("SomeScene");

            for (var poll = 1; poll < 3; poll++)
                Assert.That(scenes.Poll(requestId), Is.EqualTo(AsyncOpStatus.Pending),
                    $"Poll #{poll} of 3 must still be Pending. {scenes}");

            Assert.That(scenes.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done),
                $"Poll #3 must report the terminal status. {scenes}");
            Assert.That(scenes.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done),
                $"A settled request must keep reporting its terminal status. {scenes}");

            Assert.That(scenes.LoadCalls, Is.EqualTo(new[] { "SomeScene" }), $"{scenes}");

            scenes.Release(requestId);
            Assert.That(scenes.OpenRequestCount, Is.Zero, $"Release must drop the request. {scenes}");
            Assert.That(scenes.Poll(requestId), Is.EqualTo(AsyncOpStatus.Pending),
                $"A released id must read as Pending, never throw. {scenes}");
        }

        [Test]
        public void FakeAssetService_ResolvesAHandleOnlyWhenDone()
        {
            var assets = new FakeAssetService { CompleteAfterPolls = 2, TerminalStatus = AsyncOpStatus.Done };

            var requestId = assets.BeginLoad("some/address");

            Assert.That(assets.Poll(requestId), Is.EqualTo(AsyncOpStatus.Pending), $"{assets}");
            Assert.That(assets.ResolveHandle(requestId), Is.Zero,
                $"A pending request must not resolve a handle. {assets}");

            Assert.That(assets.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done), $"{assets}");
            Assert.That(assets.ResolveHandle(requestId), Is.Not.Zero,
                $"A completed request must resolve a non-zero handle. {assets}");

            assets.Release(requestId);
            Assert.That(assets.OpenRequestCount, Is.Zero, $"Release must drop the request. {assets}");
            Assert.That(assets.ResolveHandle(requestId), Is.Zero,
                $"A released request must not resolve a handle. {assets}");
        }

        [Test]
        public void FakeAssetService_FailedRequestNeverResolvesAHandle()
        {
            var assets = new FakeAssetService { CompleteAfterPolls = 1, TerminalStatus = AsyncOpStatus.Failed };

            var requestId = assets.BeginLoad("missing/address");

            Assert.That(assets.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed), $"{assets}");
            Assert.That(assets.ResolveHandle(requestId), Is.Zero,
                $"A failed request must resolve to 0, not to a stale handle. {assets}");
        }

        /// <summary>Throwaway probe: exists only to prove the injection arrived.</summary>
        private sealed class AllPortsProbeSystem :
            IEcsRun,
            IEcsInject<ITimeService>,
            IEcsInject<ILog>,
            IEcsInject<ISceneService>,
            IEcsInject<IAssetService>,
            IEcsInject<IDialogueService>,
            IEcsInject<IImageLoadService>
        {
            public ITimeService Time { get; private set; }
            public ILog Log { get; private set; }
            public ISceneService Scenes { get; private set; }
            public IAssetService Assets { get; private set; }
            public IDialogueService Dialogue { get; private set; }
            public IImageLoadService Images { get; private set; }

            public void Inject(ITimeService obj) => Time = obj;
            public void Inject(ILog obj) => Log = obj;
            public void Inject(ISceneService obj) => Scenes = obj;
            public void Inject(IAssetService obj) => Assets = obj;
            public void Inject(IDialogueService obj) => Dialogue = obj;
            public void Inject(IImageLoadService obj) => Images = obj;

            public void Run() { }

            public override string ToString() =>
                $"AllPortsProbeSystem(time={_Describe(Time)}, " +
                $"log={_Describe(Log)}, scenes={_Describe(Scenes)}, assets={_Describe(Assets)}, " +
                $"dialogue={_Describe(Dialogue)}, images={_Describe(Images)})";

            private static string _Describe(object value) => value?.ToString() ?? "<null>";
        }

        /// <summary>Single-port probe for the negative and AddNode cases.</summary>
        private sealed class TimeProbeSystem : IEcsRun, IEcsInject<ITimeService>
        {
            public ITimeService Time { get; private set; }

            public void Inject(ITimeService obj) => Time = obj;

            public void Run() { }

            public override string ToString() =>
                $"TimeProbeSystem(time={(Time == null ? "<null>" : Time.ToString())})";
        }
    }
}
