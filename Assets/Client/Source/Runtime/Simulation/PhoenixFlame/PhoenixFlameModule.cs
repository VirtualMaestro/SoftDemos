using DCFApixels.DragonECS;

namespace Client.Simulation.PhoenixFlame
{
    public sealed class PhoenixFlameModule : IEcsModule
    {
        private readonly PhoenixFlameConfig _config;

        public PhoenixFlameModule(PhoenixFlameConfig config)
        {
            _config = config;
        }

        /// <summary>Adds the three systems. Keep this order.</summary>
        /// <remarks>
        /// Setup runs first, so a start command and an advance command in the same tick both work.
        /// The transition runs last, so a press accepted this tick starts to count down on the
        /// next tick and not on this one.
        /// </remarks>
        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new FlameSetupSystem(_config));
            builder.Add(new FlamePhaseRequestSystem());
            builder.Add(new FlameTransitionSystem());
        }
    }
}
