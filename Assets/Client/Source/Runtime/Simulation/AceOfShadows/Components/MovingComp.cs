using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows
{
    public struct MovingComp : IEcsComponent
    {
        public int TargetStack;
        public int TargetOrder;
    }
}
