using DCFApixels.DragonECS;

namespace Client.Simulation.PhoenixFlame.Components
{
    /// <summary>Asks for a transition to the next colour in the cycle.</summary>
    /// <remarks>
    /// Raised by <c>PhoenixFlameInpSystem</c> on a button press. <c>FlamePhaseRequestSimSystem</c> deletes
    /// the command entity in the same tick whether or not the press is honoured — a held button emits one
    /// per tick, and a backlog would fire the moment the running transition ends.
    /// </remarks>
    public struct AdvanceFlamePhaseCommand : IEcsComponent
    {
    }
}
