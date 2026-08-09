using DCFApixels.DragonECS;

namespace Game.Simulation.AceOfShadows
{
    public struct CardComp : IEcsComponent
    {
        public int StackIndex;
        public int OrderInStack;
    }
}
