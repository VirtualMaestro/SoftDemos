using Client.Simulation.AceOfShadows;
using Client.Simulation.AceOfShadows.Components.Commands;
using Client.Simulation.Core.Ports;
using Client.Simulation.Tests.Fakes.Services;
using Client.Simulation.Tests.Fakes.Systems;
using DCFApixels.DragonECS;
using NUnit.Framework;

namespace Client.Simulation.Tests.AceOfShadows
{
    public abstract class AceOfShadowsTestFixture
    {
        protected EcsWorld World { get; private set; }
        protected EcsPipeline Pipeline { get; private set; }
        protected FakeTimeService Time { get; private set; }
        protected FakeLogService Log { get; private set; }
        protected FakeMovePlayerService Playback { get; private set; }

        [SetUp]
        public void SetUp()
        {
            World = new EcsWorld();
            Time = new FakeTimeService();
            Log = new FakeLogService();
            Playback = new FakeMovePlayerService();
            Pipeline = EcsPipeline.New()
                .Inject(World)
                .Inject<ITimeService>(Time)
                .Inject<ILogService>(Log)
                .Inject(Playback)
                .AddModule(new AceOfShadowsModule(new AceOfShadowsConfig()))
                // The two halves the real adapter has: one starts flights in Present, one drains
                // the finished ones in Input. Both are needed for a tick to look like a frame.
                .Add(new FakeMoveCompletionInpSystem())
                .Add(new FakeMovePlaybackPreSystem())
                .BuildAndInit();
        }

        [TearDown]
        public void TearDown()
        {
            Pipeline?.Destroy();
            Pipeline = null;

            World?.Destroy();
            World = null;
        }

        protected void _Deal()
        {
            Time.DeltaSeconds = 0f;
            var entityId = World.NewEntity();
            World.GetPool<DealDeckCommand>().Add(entityId);
            Pipeline.Tick();
        }

        protected void _Reset()
        {
            Time.DeltaSeconds = 0f;
            var entityId = World.NewEntity();
            World.GetPool<ResetDeckCommand>().Add(entityId);
            Pipeline.Tick();
        }

        protected void _Tick(float seconds)
        {
            Time.DeltaSeconds = seconds;
            Pipeline.Tick();
        }
    }
}
