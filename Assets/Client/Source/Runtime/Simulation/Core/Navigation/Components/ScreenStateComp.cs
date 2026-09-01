using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Simulation.Core.Navigation.Components
{
    /// <summary>
    /// Which screen the app is on, which demo it belongs to and the scene request still in flight. It is
    /// the one place that says a load is running, so a second open cannot start on top of it.
    /// </summary>
    /// <remarks>
    /// <c>NavigationSimSystem</c> is the only writer: it moves Menu to Loading to Demo to Unloading and
    /// back, and sets <c>LastOperationFailed</c> when a scene request fails. The shell's presentation
    /// systems read it to show the right screen. It lives as long as the world does, and starts at Menu
    /// with the indices at -1.
    /// </remarks>
    public struct ScreenStateComp : IEcsWorldComponent<ScreenStateComp>
    {
        public ScreenId Current;
        public int ActiveDemoIndex;
        public int PendingRequestId;
        public bool LastOperationFailed;

        void IEcsWorldComponent<ScreenStateComp>.Init(ref ScreenStateComp component, EcsWorld world)
        {
            component.Current = ScreenId.Menu;
            component.ActiveDemoIndex = -1;
            component.PendingRequestId = -1;
            component.LastOperationFailed = false;
        }

        void IEcsWorldComponent<ScreenStateComp>.OnDestroy(ref ScreenStateComp component, EcsWorld world)
        {
            component = default;
        }

        public override string ToString()
        {
            return $"{nameof(Current)}={Current}, {nameof(ActiveDemoIndex)}={ActiveDemoIndex}, " +
                $"{nameof(PendingRequestId)}={PendingRequestId}, " +
                $"{nameof(LastOperationFailed)}={LastOperationFailed}";
        }
    }
}
