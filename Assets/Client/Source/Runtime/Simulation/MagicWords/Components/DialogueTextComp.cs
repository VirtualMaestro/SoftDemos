using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    public struct DialogueTextComp : IEcsComponent
    {
        public DialogueSegment[] Segments;
    }
}
