using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Components
{
    /// <summary>Asks for every card and stack entity to be deleted and the deck state wiped.</summary>
    /// <remarks>
    /// Raised by <c>AceOfShadowsInputSystem</c> when the screen closes. <c>DeckSetupSystem</c> consumes
    /// it before the deal command and deletes the command entity in the same tick, so a close-then-open
    /// within one tick still leaves the deck dealt.
    /// </remarks>
    public struct ResetDeckCommand : IEcsComponent
    {
    }
}
