using DCFApixels.DragonECS;

namespace Client.Simulation.Core.Navigation.Components
{
    /// <summary>Asks for the demo at this catalog index to be loaded. The index, not a scene name, is
    /// what crosses the boundary: the simulation does not know addresses.</summary>
    /// <remarks>
    /// Raised by <c>ScreenPreSystem</c> from a menu button. <c>NavigationSimSystem</c> consumes it
    /// and deletes the command entity in the same tick, warning instead of acting when the screen is not
    /// the menu or the index is outside the catalog.
    /// </remarks>
    public struct OpenDemoCommand : IEcsComponent
    {
        public int DemoIndex;
    }
}
