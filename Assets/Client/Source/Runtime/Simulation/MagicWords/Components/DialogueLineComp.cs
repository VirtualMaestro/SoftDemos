using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>
    /// One line of the dialogue: its place in the payload order and who says it. The order is kept here
    /// because entity iteration order is not the payload order.
    /// </summary>
    /// <remarks>
    /// Written by <c>DialogueIngestSimSystem</c> once per payload entry. <c>DialoguePlaybackSimSystem</c>
    /// reads it to reveal the lowest index still hidden, <c>DialogueLogPreSystem</c> to sort the log.
    /// Removed only when <c>DialogueResetSimSystem</c> deletes the line entity.
    /// </remarks>
    public struct DialogueLineComp : IEcsComponent
    {
        public int Index;
        public entlong Speaker;
    }
}
