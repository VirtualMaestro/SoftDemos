using DCFApixels.DragonECS;

namespace Client.Adapters.MagicWords.Components
{
    /// <summary>Marks a revealed line that already has a row in the log list, so it is not added twice.</summary>
    /// <remarks>
    /// Written only by <c>DialogueLogSystem</c>: added when the row is created, removed for every bound
    /// line when the list is cleared on teardown. This is a presentation fact, so it stays in the
    /// adapter. The simulation must not read it.
    /// </remarks>
    internal struct DialogueLineBoundTag : IEcsTagComponent
    {
    }
}
