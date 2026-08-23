using DCFApixels.DragonECS;

namespace Client.Simulation.Shared.Navigation.Components
{
    /// <summary>Asks for the open demo to be unloaded and the menu brought back.</summary>
    /// <remarks>
    /// Raised by <c>ScreenPresentationSystem</c> from the back button. <c>NavigationSystem</c> consumes
    /// it and deletes the command entity in the same tick, warning instead of acting when no demo is
    /// open.
    /// </remarks>
    public struct CloseDemoCommand : IEcsComponent
    {
    }
}
