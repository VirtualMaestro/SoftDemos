using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    public struct AvatarComp : IEcsComponent
    {
        public string Url;
        public AvatarSide Side;
    }
}
