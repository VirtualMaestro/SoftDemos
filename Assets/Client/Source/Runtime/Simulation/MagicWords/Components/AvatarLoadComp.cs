using DCFApixels.DragonECS;

namespace Game.Simulation.MagicWords
{
    public struct AvatarLoadComp : IEcsComponent
    {
        public AvatarLoadState State;
        public int RequestId;
        public int HandleId;
    }
}
