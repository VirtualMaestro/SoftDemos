using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Components
{
    /// <summary>Asks for the card and stack entities to be created from config. Until one arrives the
    /// deck does not exist.</summary>
    /// <remarks>
    /// Raised by <c>AceOfShadowsStageSystem</c> when the screen opens. <c>DeckSetupSystem</c> consumes
    /// it and deletes the command entity in the same tick, so it never survives a frame.
    /// </remarks>
    public struct DealDeckCommand : IEcsComponent
    {
    }
}
