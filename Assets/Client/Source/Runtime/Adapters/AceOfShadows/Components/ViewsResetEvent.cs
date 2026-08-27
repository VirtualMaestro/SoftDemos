using DCFApixels.DragonECS;

namespace Client.Adapters.AceOfShadows.Components
{
    /// <summary>Says the card view pool was rebuilt, so every binding is stale.</summary>
    /// <remarks>
    /// Added by the stage system once the pool is full, read by <c>CardBindingSystem</c>, which
    /// rewinds its bind cursor and unseats every card. One frame: the Ace of Shadows cleanup
    /// system deletes it and nothing else may.
    /// </remarks>
    internal struct ViewsResetEvent : IEcsTagComponent { }
}
