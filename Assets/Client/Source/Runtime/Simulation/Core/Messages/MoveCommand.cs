using DCFApixels.DragonECS;

namespace Client.Simulation.Messages
{
    /// <summary>Asks the adapter to move this entity to a slot in a given time.</summary>
    /// <remarks>
    /// There are no coordinates here. The simulation knows slot indices and the adapter layout
    /// turns an index into a position, so portrait and landscape share one simulation. The adapter
    /// removes this component and adds <see cref="MoveCompletedTag"/> when the move ends.
    /// </remarks>
    public struct MoveCommand : IEcsComponent
    {
        public int TargetSlot;

        /// <summary>How deep in the slot the view lands. Features that do not stack use zero.</summary>
        public int TargetDepth;

        public float Duration;
    }
}
