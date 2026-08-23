using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Components
{
    /// <summary>
    /// Says a card is in flight and remembers the destination chosen when the move was issued, so the
    /// landing does not depend on how the stacks look by the time the tween ends.
    /// </summary>
    /// <remarks>
    /// Added by <c>CardCadenceSystem</c> together with the <c>MoveCommand</c> and removed by
    /// <c>MoveCompletionSystem</c> on <c>MoveCompletedTag</c>. Its presence also keeps the card out of
    /// the cadence pick and raises the view's sorting order in <c>CardBindingSystem</c>.
    /// </remarks>
    public struct MovingComp : IEcsComponent
    {
        public int TargetStack;
        public int TargetOrder;
    }
}
