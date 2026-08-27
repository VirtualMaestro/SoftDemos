using DCFApixels.DragonECS;

namespace Client.Adapters.AceOfShadows.Components
{
    /// <summary>An opaque reference to whatever the adapter uses to show this entity.</summary>
    /// <remarks>
    /// Presentation owns this: the adapter is the only writer and <c>ViewRegistryService</c> is the
    /// only thing that can turn the number into a view. The simulation neither writes nor reads it.
    /// </remarks>
    public struct ViewHandleComp : IEcsComponent
    {
        public int Id;
    }
}
