using DCFApixels.DragonECS;

namespace Game.Simulation.PhoenixFlame
{
    public sealed class PhoenixFlameModule : IEcsModule
    {
        private readonly PhoenixFlameConfig _config;

        public PhoenixFlameModule(PhoenixFlameConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// The order is load-bearing, not alphabetical.
        ///
        /// Setup runs first so that a <c>StartFlameCommand</c> and an
        /// <c>AdvanceFlamePhaseCommand</c> arriving in the same tick are both honoured — the stage
        /// system emits the start on scene open, and a press on that very frame must not be
        /// discarded as "the flame is not active".
        ///
        /// The transition runs last so a press consumed this tick opens a transition that only
        /// starts counting down on the next one. Ticking before the press would spend the first
        /// frame's delta on a transition that did not exist yet.
        /// </summary>
        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new FlameSetupSystem(_config));
            builder.Add(new FlamePhaseRequestSystem());
            builder.Add(new FlameTransitionSystem());
        }
    }
}
