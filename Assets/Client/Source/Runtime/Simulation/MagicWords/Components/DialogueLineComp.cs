using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    public struct DialogueLineComp : IEcsComponent
    {
        public int Index;
        public entlong Speaker;
    }
}
