using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Components
{
    /// <summary>How many cards a stack holds, which is what gives a landing card its depth.</summary>
    /// <remarks>
    /// <c>DeckSetupSystem</c> creates one per stack on deal. <c>CardCadenceSystem</c> decrements the
    /// source stack when a move is issued, <c>MoveCompletionSystem</c> increments the target when it
    /// lands, <c>DeckHudSystem</c> shows the numbers. Reset deletes the entity.
    /// </remarks>
    public struct StackComp : IEcsComponent
    {
        public int Index;
        public int Count;
    }
}
