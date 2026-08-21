using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Simulation.Shared.Navigation.Components
{
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
