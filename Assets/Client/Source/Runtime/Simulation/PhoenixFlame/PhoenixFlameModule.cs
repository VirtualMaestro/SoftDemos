using Client.Simulation.PhoenixFlame.Systems;
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

        /// <summary>Adds the flame systems.</summary>
        /// <remarks>
        /// The sequence of <c>Add</c> calls below is the schedule inside the Sim phase, and
        /// <c>DEU0136</c> is what checks it. This remark used to restate that sequence in prose the
        /// calls themselves contradicted, which is the whole argument for not writing it twice.
        /// What is worth recording is the cost of changing it: a start command and an advance
        /// command that arrive together stop both working.
        /// </remarks>
        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new FlameSetupSimSystem(_config));
            builder.Add(new FlamePhaseRequestSimSystem());
            builder.Add(new FlameTransitionSimSystem());
            builder.Add(new FlameClnSystem());
        }
    }
}
