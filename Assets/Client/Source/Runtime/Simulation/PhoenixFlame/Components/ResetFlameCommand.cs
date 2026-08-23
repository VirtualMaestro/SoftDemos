using DCFApixels.DragonECS;

namespace Client.Simulation.PhoenixFlame.Components
{
    /// <summary>Asks for the flame state to be wiped back to inactive.</summary>
    /// <remarks>
    /// Raised by <c>PhoenixFlameStageSystem</c> when the screen closes. <c>FlameSetupSystem</c> consumes
    /// it before the start command and deletes the command entity in the same tick; on an already
    /// inactive flame it is consumed silently, because closing a demo that never opened is a normal path.
    /// </remarks>
    public struct ResetFlameCommand : IEcsComponent
    {
    }
}
