using DCFApixels.DragonECS;

namespace Client.Simulation.Core.Messages
{
    /// <summary>Reports that a <see cref="MoveCommand"/> finished.</summary>
    /// <remarks>The adapter adds this on a tick, never inside a tween callback.</remarks>
    public struct MoveCompletedTag : IEcsTagComponent { }
}
