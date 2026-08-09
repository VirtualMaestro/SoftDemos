using DCFApixels.DragonECS;

namespace Game.Simulation.MagicWords
{
    public struct DialogueTextComp : IEcsComponent
    {
        public DialogueSegment[] Segments;
    }
}
