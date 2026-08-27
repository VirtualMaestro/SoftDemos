using DCFApixels.DragonECS;

namespace Client.Simulation.PhoenixFlame.Components
{
    /// <summary>Asks for the flame to be brought up from config. Until one arrives the flame is
    /// inactive and every press is ignored.</summary>
    /// <remarks>
    /// Raised by <c>PhoenixFlameInputSystem</c> when the screen opens. <c>FlameSetupSystem</c> consumes
    /// it and deletes the command entity in the same tick, after the reset command so a close-then-open
    /// within one tick still leaves the flame active.
    /// </remarks>
    public struct StartFlameCommand : IEcsComponent
    {
    }
}
