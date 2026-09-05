using Client.Adapters.PhoenixFlame.Systems;
using DCFApixels.DragonECS;

namespace Client.Adapters.PhoenixFlame
{
    public sealed class PhoenixFlameAdapterModule : IEcsModule
    {
        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new PhoenixFlameInpSystem());
            builder.Add(new PhoenixFlamePreSystem());
        }
    }
}
