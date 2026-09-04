using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Components.Commands
{
    /// <summary>Asks for the card and stack entities to be created from config. Until one arrives the
    /// deck does not exist.</summary>
    /// <remarks>
    /// Raised by <c>AceOfShadowsInpSystem</c> when the screen opens. <c>DeckSetupSimSystem</c> consumes
    /// it and deletes the command entity in the same tick, so it never survives a frame.
    /// </remarks>
    public struct DealDeckCommand : IEcsComponent
    {
    }
}
