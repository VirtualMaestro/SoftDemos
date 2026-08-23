using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>
    /// One line of the dialogue: its place in the payload order and who says it. The order is kept here
    /// because entity iteration order is not the payload order.
    /// </summary>
    /// <remarks>
    /// Written by <c>DialogueIngestSystem</c> once per payload entry. <c>DialoguePlaybackSystem</c>
    /// reads it to reveal the lowest index still hidden, <c>DialogueLogSystem</c> to sort the log.
    /// Removed only when <c>DialogueResetSystem</c> deletes the line entity.
    /// </remarks>
    public struct DialogueLineComp : IEcsComponent
    {
        public int Index;
        public entlong Speaker;
    }
}
