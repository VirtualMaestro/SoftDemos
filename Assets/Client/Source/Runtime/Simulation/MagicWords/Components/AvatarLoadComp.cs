using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    public struct AvatarLoadComp : IEcsComponent
    {
        public AvatarLoadState State;
        public int RequestId;
        public int HandleId;
    }
}
