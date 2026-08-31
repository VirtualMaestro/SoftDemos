using DCFApixels.DragonECS;

namespace Client.Adapters.Shared.Components
{
    /// <summary>Says the shell has finished with its three addresses and the menu may be shown.</summary>
    /// <remarks>
    /// Added by <c>ShellStageSystem</c>, including on the failure path, which is terminal and
    /// leaves the shell unskinned: a plain menu is still playable, a menu that never appears is
    /// not. Never removed — the shell skin outlives every demo — so this is a latch that is set
    /// once. Presentation asks whether the tag exists; no entity id is stored.
    /// </remarks>
    public struct ShellReadyTag : IEcsTagComponent { }
}
