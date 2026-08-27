using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>Asks for the whole dialogue to be torn down: open requests released, speaker and line
    /// entities deleted, state zeroed.</summary>
    /// <remarks>
    /// Raised by <c>MagicWordsInputSystem</c> when the screen closes. <c>DialogueResetSystem</c> consumes
    /// it and deletes the command entity in the same tick.
    /// </remarks>
    public struct ResetDialogueCommand : IEcsComponent
    {
    }
}
