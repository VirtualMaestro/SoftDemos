using DCFApixels.DragonECS;

namespace Game.Simulation.MagicWords
{
    public struct AvatarComp : IEcsComponent
    {
        public string Url;
        public AvatarSide Side;
    }
}
