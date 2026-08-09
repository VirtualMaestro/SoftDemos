using DCFApixels.DragonECS;

namespace Game.Simulation.Messages
{
    /// <summary>
    /// "This entity should end up at slot <see cref="TargetSlot"/>, taking <see cref="Duration"/>
    /// seconds about it." The simulation's entire share of an animated move.
    ///
    /// No coordinates: the simulation knows stack *indices*, and the adapter's layout turns an
    /// index into a position. That is what makes landscape and portrait the same simulation.
    /// The adapter removes this component and adds <see cref="MoveCompletedTag"/> when the
    /// movement finishes.
    /// </summary>
    public struct MoveCommand : IEcsComponent
    {
        public int TargetSlot;

        /// <summary>
        /// How deep in the target slot the view lands. Features that do not stack use zero.
        /// </summary>
        public int TargetDepth;

        public float Duration;
    }
}
