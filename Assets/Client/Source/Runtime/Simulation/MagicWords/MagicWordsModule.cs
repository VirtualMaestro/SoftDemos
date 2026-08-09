using DCFApixels.DragonECS;

namespace Game.Simulation.MagicWords
{
    public sealed class MagicWordsModule : IEcsModule
    {
        private readonly MagicWordsConfig _config;

        public MagicWordsModule(MagicWordsConfig config)
        {
            _config = config;
        }

        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new DialogueResetSystem());
            builder.Add(new DialogueFetchSystem());
            builder.Add(new DialogueIngestSystem(_config));
            builder.Add(new DialoguePlaybackSystem(_config));
            builder.Add(new AvatarLoadSystem());
        }
    }
}
