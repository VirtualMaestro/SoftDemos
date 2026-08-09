using DCFApixels.DragonECS;

namespace Game.Simulation.MagicWords
{
    public struct DialogueLineComp : IEcsComponent
    {
        public int Index;
        public entlong Speaker;
    }
}
