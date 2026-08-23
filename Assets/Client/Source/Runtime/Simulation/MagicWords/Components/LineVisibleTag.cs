using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>
    /// Says this line has already been revealed. It is what separates the lines that are on screen from
    /// the ones still waiting, without a second list to keep in sync.
    /// </summary>
    /// <remarks>
    /// Added by <c>DialoguePlaybackSystem</c> when a line's turn comes (or for all of them on skip) and
    /// read by <c>DialogueLogSystem</c> to spawn the bubble. Never taken off: reset deletes the line
    /// entity instead.
    /// </remarks>
    public struct LineVisibleTag : IEcsTagComponent
    {
    }
}
