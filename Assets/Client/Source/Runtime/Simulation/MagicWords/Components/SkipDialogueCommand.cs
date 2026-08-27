using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>Asks for the rest of the lines to be revealed at once instead of on the timer.</summary>
    /// <remarks>
    /// Raised by <c>MagicWordsInputSystem</c> on a tap. <c>DialoguePlaybackSystem</c> drains it at the
    /// top of its tick, before the readiness gate, so a tap made while the payload is still in flight is
    /// discarded rather than queued.
    /// </remarks>
    public struct SkipDialogueCommand : IEcsComponent
    {
    }
}
