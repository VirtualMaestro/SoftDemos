using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>Asks for the dialogue payload to be fetched. Nothing is downloaded until one arrives.</summary>
    /// <remarks>
    /// Raised by <c>MagicWordsInpSystem</c> when the screen opens. <c>DialogueFetchSimSystem</c> consumes
    /// it and deletes the command entity in the same tick, ignoring it while a fetch is already loading
    /// or ready.
    /// </remarks>
    public struct LoadDialogueCommand : IEcsComponent
    {
    }
}
