using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Components
{
    public struct CardComp : IEcsComponent
    {
        public int StackIndex;
        public int OrderInStack;
    }
}
