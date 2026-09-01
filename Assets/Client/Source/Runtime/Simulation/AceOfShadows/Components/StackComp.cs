using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Components
{
    /// <summary>How many cards a stack holds, which is what gives a landing card its depth.</summary>
    /// <remarks>
    /// <c>DeckSetupSimSystem</c> creates one per stack on deal. <c>CardCadenceSimSystem</c> decrements the
    /// source stack when a move is issued, <c>MoveCompletionSimSystem</c> increments the target when it
    /// lands, <c>DeckHudPreSystem</c> shows the numbers. Reset deletes the entity.
    /// </remarks>
    public struct StackComp : IEcsComponent
    {
        public int Index;
        public int Count;
    }
}
