using DCFApixels.DragonECS;

namespace Client.Adapters.AceOfShadows.Components
{
    /// <summary>Says the slot positions were recalculated, so anything placed by them is stale.</summary>
    /// <remarks>
    /// Added by the stage system wherever it calls <c>StackSlotLayoutService.Recalculate</c> — on
    /// load and on a screen resize. Read by <c>CardBindingSystem</c>, which reseats the resting
    /// cards, and by <c>DeckHudSystem</c>, which moves the stack counters. One frame: the Ace of
    /// Shadows cleanup system deletes it and nothing else may.
    /// </remarks>
    internal struct LayoutChangedEvent : IEcsTagComponent { }
}
