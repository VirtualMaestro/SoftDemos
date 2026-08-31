using Client.Adapters.MagicWords.Systems;
using DCFApixels.DragonECS;

namespace Client.Adapters.MagicWords
{
    public sealed class MagicWordsModule : IEcsModule
    {
        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new MagicWordsInputSystem());
            builder.Add(new MagicWordsViewSystem());
            builder.Add(new DialogueLogSystem());
            builder.Add(new MagicWordsCleanupSystem());
        }
    }
}
