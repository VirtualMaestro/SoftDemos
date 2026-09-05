using Client.Simulation.MagicWords.Systems;
using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords
{
    public sealed class MagicWordsSimulationModule : IEcsModule
    {
        private readonly MagicWordsConfig _config;

        public MagicWordsSimulationModule(MagicWordsConfig config)
        {
            _config = config;
        }

        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new DialogueResetSimSystem());
            builder.Add(new DialogueFetchSimSystem());
            builder.Add(new DialogueIngestSimSystem(_config));
            builder.Add(new DialoguePlaybackSimSystem(_config));
            builder.Add(new AvatarLoadSimSystem());
            builder.Add(new DialogueClnSystem());
        }
    }
}
