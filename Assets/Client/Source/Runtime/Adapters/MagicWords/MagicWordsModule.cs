using Client.Adapters.MagicWords.Systems;
using DCFApixels.DragonECS;

namespace Client.Adapters.MagicWords
{
    public sealed class MagicWordsModule : IEcsModule
    {
        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new MagicWordsInpSystem());
            builder.Add(new MagicWordsPreSystem());
            builder.Add(new DialogueLogPreSystem());
            builder.Add(new MagicWordsClnSystem());
        }
    }
}
