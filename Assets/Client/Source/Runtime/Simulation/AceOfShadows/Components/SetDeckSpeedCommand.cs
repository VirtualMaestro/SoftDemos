using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Components
{
    /// <summary>Carries a requested speed multiplier from the stage slider into the simulation.</summary>
    /// <remarks>
    /// Raised by <c>AceOfShadowsInpSystem</c> on every slider change. <c>DeckSpeedSimSystem</c> clamps
    /// the value into the move interval and duration, then deletes the command entity in the same tick.
    /// </remarks>
    public struct SetDeckSpeedCommand : IEcsComponent
    {
        public float Multiplier;
    }
}
