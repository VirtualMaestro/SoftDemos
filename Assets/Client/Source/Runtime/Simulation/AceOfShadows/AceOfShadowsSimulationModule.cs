using Client.Simulation.AceOfShadows.Systems;
using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows
{
    public sealed class AceOfShadowsSimulationModule : IEcsModule
    {
        private readonly AceOfShadowsConfig _config;

        public AceOfShadowsSimulationModule(AceOfShadowsConfig config)
        {
            _config = config;
        }

        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new DeckSetupSimSystem(_config));
            builder.Add(new MoveCompletionSimSystem());
            builder.Add(new DeckSpeedSimSystem(_config));
            builder.Add(new CardCadenceSimSystem());
            builder.Add(new DeckClnSystem());
        }
    }
}
