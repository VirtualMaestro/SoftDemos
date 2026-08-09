using DCFApixels.DragonECS;

namespace Game.Simulation.AceOfShadows
{
    public struct MovingComp : IEcsComponent
    {
        public int TargetStack;
        public int TargetOrder;
    }
}
