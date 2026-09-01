using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Simulation.PhoenixFlame.Components
{
    /// <summary>
    /// The flame as a whole: whether it burns, which colour it shows, and the transition in flight with
    /// its 0..1 progress. One flame per screen, so it is world data rather than an entity's.
    /// </summary>
    /// <remarks>
    /// <c>FlameSetupSimSystem</c> starts and wipes it, <c>FlamePhaseRequestSimSystem</c> opens a transition,
    /// <c>FlameTransitionSimSystem</c> counts it down and swaps the phase. The adapter's flame view reads
    /// <c>Progress</c> to blend the colours. It lives as long as the world does; reset returns it to
    /// default instead of removing it.
    /// </remarks>
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
