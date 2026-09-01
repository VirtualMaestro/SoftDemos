using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>
    /// The line's text already split into plain-text and emoji pieces, so the adapter never parses
    /// markup and the tokenizer runs once per line instead of once per redraw.
    /// </summary>
    /// <remarks>
    /// Written by <c>DialogueIngestSimSystem</c> beside <see cref="DialogueLineComp"/> and read by
    /// <c>DialogueLogPreSystem</c> when it builds the bubble. Removed with the line entity on reset.
    /// </remarks>
    public struct DialogueTextComp : IEcsComponent
    {
        public DialogueSegment[] Segments;
    }
}
