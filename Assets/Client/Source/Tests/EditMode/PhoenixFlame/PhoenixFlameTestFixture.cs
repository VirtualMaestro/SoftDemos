using Client.Simulation.Core.Ports;
using Client.Simulation.PhoenixFlame;
using Client.Simulation.PhoenixFlame.Components;
using Client.Simulation.Tests.Fakes;
using DCFApixels.DragonECS;
using NUnit.Framework;

namespace Client.Simulation.Tests.PhoenixFlame
{
    public abstract class PhoenixFlameTestFixture
    {
        protected EcsWorld World { get; private set; }
        protected EcsPipeline Pipeline { get; private set; }
        protected FakeTime Time { get; private set; }
        protected FakeLog Log { get; private set; }

        [SetUp]
        public void SetUp()
        {
            _Build(new PhoenixFlameConfig());
        }

        [TearDown]
        public void TearDown()
        {
            _Destroy();
        }

        /// <summary>
        /// Replaces the default world and pipeline with ones built around <paramref name="config"/>.
        /// <c>SetUp</c> has already built the default pair, so the old one is destroyed first —
        /// DragonECS registers worlds globally and a leaked one keeps its id and pools alive.
        /// </summary>
        protected void _Rebuild(PhoenixFlameConfig config)
        {
            _Destroy();
            _Build(config);
        }

        protected void _Start()
        {
            Time.DeltaSeconds = 0f;
            World.GetPool<StartFlameCommand>().Add(World.NewEntity());
            Pipeline.Run();
        }

        protected void _Advance()
        {
            Time.DeltaSeconds = 0f;
            World.GetPool<AdvanceFlamePhaseCommand>().Add(World.NewEntity());
            Pipeline.Run();
        }

        protected void _Reset()
        {
            Time.DeltaSeconds = 0f;
            World.GetPool<ResetFlameCommand>().Add(World.NewEntity());
            Pipeline.Run();
        }

        protected void _Tick(float seconds)
        {
            Time.DeltaSeconds = seconds;
            Pipeline.Run();
        }

        private void _Build(PhoenixFlameConfig config)
        {
            World = new EcsWorld();
            Time = new FakeTime();
            Log = new FakeLog();
            Pipeline = EcsPipeline.New()
                .Inject(World)
                .Inject<ITimeService>(Time)
                .Inject<ILog>(Log)
                .AddModule(new PhoenixFlameModule(config))
                .BuildAndInit();
        }

        private void _Destroy()
        {
            Pipeline?.Destroy();
            Pipeline = null;

            World?.Destroy();
            World = null;
        }
    }
}
