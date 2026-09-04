using DCFApixels.DragonECS;

namespace Client.Simulation.Core.Components.Commands
{
    /// <summary>Tells the simulation that the flight it started has ended.</summary>
    /// <remarks>
    /// A command rather than a tag because it crosses the boundary the other way: the adapter is
    /// its only writer, and adapter-to-simulation is the one direction the two halves sanction.
    /// The adapter adds it on a tick, draining its completion queue, never inside a tween callback.
    /// The simulation reads it beside the component that describes the flight (<c>MovingComp</c> in
    /// Ace of Shadows) and drops both.
    /// </remarks>
    public struct MoveCompletedCommand : IEcsTagComponent { }
}
