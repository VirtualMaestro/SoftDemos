using DCFApixels.DragonECS;

namespace Client.Adapters.AceOfShadows.Components
{
    /// <summary>Marks a card view that already stands at its slot, so the seating pass skips it.</summary>
    /// <remarks>
    /// Written only by <c>CardBindingSystem</c>: added on bind and after a move lands, removed while the
    /// card is in flight and for every card when the layout changes. This is a presentation fact, so it
    /// stays in the adapter. The simulation must not read it.
    /// </remarks>
    public struct CardSeatedTag : IEcsTagComponent
    {
    }
}
