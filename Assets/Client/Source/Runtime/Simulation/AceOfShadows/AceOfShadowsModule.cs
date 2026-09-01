using Client.Simulation.AceOfShadows.Systems;
using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows
{
    public sealed class AceOfShadowsModule : IEcsModule
    {
        private readonly AceOfShadowsConfig _config;

        public AceOfShadowsModule(AceOfShadowsConfig config)
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
