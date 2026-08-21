using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Simulation.PhoenixFlame.Components
{
    public struct FlameStateComp : IEcsWorldComponent<FlameStateComp>
    {
        public bool IsActive;
        public FlamePhase CurrentPhase;
        public FlamePhase NextPhase;
        public bool IsTransitioning;
        public float TransitionDurationSeconds;
        public float SecondsRemaining;
        public float Progress;
        public int PhaseChangeCount;

        void IEcsWorldComponent<FlameStateComp>.Init(ref FlameStateComp component, EcsWorld world)
        {
            component = default;
        }

        void IEcsWorldComponent<FlameStateComp>.OnDestroy(ref FlameStateComp component, EcsWorld world)
        {
            component = default;
        }

        public override string ToString()
        {
            return $"{nameof(IsActive)}={IsActive}, {nameof(CurrentPhase)}={CurrentPhase}, " +
                $"{nameof(NextPhase)}={NextPhase}, {nameof(IsTransitioning)}={IsTransitioning}, " +
                $"{nameof(TransitionDurationSeconds)}={TransitionDurationSeconds}, " +
                $"{nameof(SecondsRemaining)}={SecondsRemaining}, {nameof(Progress)}={Progress}, " +
                $"{nameof(PhaseChangeCount)}={PhaseChangeCount}";
        }
    }
}
