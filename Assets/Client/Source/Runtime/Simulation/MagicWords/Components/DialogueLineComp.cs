using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords
{
    public struct DialogueLineComp : IEcsComponent
    {
        public int Index;
        public entlong Speaker;
    }
}
