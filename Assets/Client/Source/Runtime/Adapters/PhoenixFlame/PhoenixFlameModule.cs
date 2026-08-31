using Client.Adapters.PhoenixFlame.Systems;
using DCFApixels.DragonECS;

namespace Client.Adapters.PhoenixFlame
{
    public sealed class PhoenixFlameModule : IEcsModule
    {
        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new PhoenixFlameInputSystem());
            builder.Add(new PhoenixFlameViewSystem());
        }
    }
}
