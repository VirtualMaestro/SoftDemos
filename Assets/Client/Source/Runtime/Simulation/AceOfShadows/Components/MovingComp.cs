using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Components
{
    public struct MovingComp : IEcsComponent
    {
        public int TargetStack;
        public int TargetOrder;
    }
}
