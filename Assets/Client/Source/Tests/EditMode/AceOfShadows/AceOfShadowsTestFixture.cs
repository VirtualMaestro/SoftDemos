using Client.Simulation.AceOfShadows;
using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.Shared.Ports;
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
        protected FakeMovePlaybackSystem Playback { get; private set; }

        [SetUp]
        public void SetUp()
        {
            World = new EcsWorld();
            Time = new FakeTimeService();
            Log = new FakeLogService();
            Playback = new FakeMovePlaybackSystem();
            Pipeline = EcsPipeline.New()
                .Inject(World)
                .Inject<ITimeService>(Time)
                .Inject<ILogService>(Log)
                .AddModule(new AceOfShadowsModule(new AceOfShadowsConfig()))
                .Add(Playback)
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
            Pipeline.Run();
        }

        protected void _Reset()
        {
            Time.DeltaSeconds = 0f;
            var entityId = World.NewEntity();
            World.GetPool<ResetDeckCommand>().Add(entityId);
            Pipeline.Run();
        }

        protected void _Tick(float seconds)
        {
            Time.DeltaSeconds = seconds;
            Pipeline.Run();
        }
    }
}
