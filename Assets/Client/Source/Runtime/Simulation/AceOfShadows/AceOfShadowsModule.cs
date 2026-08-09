using DCFApixels.DragonECS;

namespace Game.Simulation.AceOfShadows
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
            builder.Add(new DeckSetupSystem(_config));
            builder.Add(new MoveCompletionSystem());
            builder.Add(new DeckSpeedSystem(_config));
            builder.Add(new CardCadenceSystem());
        }
    }
}
