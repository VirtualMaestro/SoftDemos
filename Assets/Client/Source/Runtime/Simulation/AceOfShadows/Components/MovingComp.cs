using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Components
{
    /// <summary>
    /// Says a card is in flight and carries the whole move: the destination chosen when the move was
    /// issued, so the landing does not depend on how the stacks look by the time the tween ends, and
    /// how long the flight lasts.
    /// </summary>
    /// <remarks>
    /// Added by <c>CardCadenceSystem</c> and removed by <c>MoveCompletionSystem</c> once
    /// <c>MoveCompletedCommand</c> arrives, so it lives exactly as long as the flight does — which is
    /// what makes it a component and not a one-frame command. The adapter reads it to start the
    /// tween and never writes it. Its presence also keeps the card out of the cadence pick and
    /// raises the view's sorting order in <c>CardBindingSystem</c>.
    /// There are no coordinates here: the simulation knows slot indices and the adapter's layout
    /// turns an index into a position, so portrait and landscape share one simulation.
    /// </remarks>
    public struct MovingComp : IEcsComponent
    {
        public int TargetStack;

        /// <summary>How deep in the target stack the card lands.</summary>
        public int TargetOrder;

        /// <summary>How long the flight lasts. What the motion looks like is the adapter's.</summary>
        public float DurationSeconds;
    }
}
