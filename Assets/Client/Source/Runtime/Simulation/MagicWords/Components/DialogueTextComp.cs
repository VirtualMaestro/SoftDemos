using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords
{
    public struct DialogueTextComp : IEcsComponent
    {
        public DialogueSegment[] Segments;
    }
}
