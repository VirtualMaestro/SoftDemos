using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Components
{
    /// <summary>Where a card rests right now, so the next card to move can be chosen by position.</summary>
    /// <remarks>
    /// <c>DeckSetupSystem</c> writes it on deal, <c>MoveCompletionSystem</c> rewrites it when a move
    /// lands. <c>CardCadenceSystem</c> reads it to take the top card of the source stack and
    /// <c>CardBindingSystem</c> to place the view. Never removed on its own: reset deletes the entity.
    /// </remarks>
    public struct CardComp : IEcsComponent
    {
        public int StackIndex;
        public int OrderInStack;
    }
}
