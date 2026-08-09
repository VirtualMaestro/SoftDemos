using DCFApixels.DragonECS;
using Game.Simulation.AceOfShadows;
using Game.Simulation.Ports;
using Game.Simulation.Tests.Fakes;
using NUnit.Framework;

namespace Game.Simulation.Tests.AceOfShadows
{
    public abstract class AceOfShadowsTestFixture
    {
        protected EcsWorld World { get; private set; }
        protected EcsPipeline Pipeline { get; private set; }
        protected FakeTime Time { get; private set; }
        protected FakeLog Log { get; private set; }
        protected FakeMovePlayback Playback { get; private set; }

        [SetUp]
        public void SetUp()
        {
            World = new EcsWorld();
            Time = new FakeTime();
            Log = new FakeLog();
            Playback = new FakeMovePlayback();
            Pipeline = EcsPipeline.New()
                .Inject(World)
                .Inject<ITimeService>(Time)
                .Inject<ILog>(Log)
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
