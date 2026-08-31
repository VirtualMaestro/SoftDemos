using Client.Adapters.AceOfShadows.Systems;
using Client.Simulation.AceOfShadows;
using DCFApixels.DragonECS;

namespace Client.Adapters.AceOfShadows
{
    public sealed class AceOfShadowsModule : IEcsModule
    {
        private readonly AceOfShadowsConfig _config;

        public AceOfShadowsModule(AceOfShadowsConfig config)
        {
            _config = config;
        }

        /// <summary>Adds the demo's presentation half.</summary>
        /// <remarks>
        /// The config is the same instance the simulation module takes: the input system reads the
        /// speed steps a player cycles through, and two copies of that would drift apart the first
        /// time one is edited.
        /// </remarks>
        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new AceOfShadowsInputSystem(_config));
            builder.Add(new CardBindingSystem());
            builder.Add(new DeckHudSystem());
            builder.Add(new TweenPlaybackSystem());
            builder.Add(new AceOfShadowsCleanupSystem());
        }
    }
}
