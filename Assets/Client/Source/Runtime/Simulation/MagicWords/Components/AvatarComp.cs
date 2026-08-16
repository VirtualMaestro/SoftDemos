using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords
{
    public struct AvatarComp : IEcsComponent
    {
        public string Url;
        public AvatarSide Side;
    }
}
