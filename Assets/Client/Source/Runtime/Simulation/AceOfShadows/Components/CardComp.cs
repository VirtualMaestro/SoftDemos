using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows
{
    public struct CardComp : IEcsComponent
    {
        public int StackIndex;
        public int OrderInStack;
    }
}
