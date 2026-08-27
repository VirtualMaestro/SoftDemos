using Client.Simulation.Core.Phases;
using Client.Simulation.Core.Ports;
using Client.Simulation.PhoenixFlame.Components;
using DCFApixels.DragonECS;

namespace Client.Simulation.PhoenixFlame.Systems
{
    /// <summary>
    /// Consumes advance-phase button commands: starts a transition to the next color unless one
    /// is already running.
    /// </summary>
    internal sealed class FlamePhaseRequestSystem : IEcsSim, IEcsInject<EcsWorld>, IEcsInject<ILogService>
    {
        private EcsWorld _world;
        private ILogService _log;

        public void Sim()
        {
            ref var state = ref _world.Get<FlameStateComp>();

            // A press ignored here is dropped, not queued: the command lives one frame and
            // FlameCleanupSystem ends it, so a held button cannot build a backlog that fires the
            // moment the in-flight transition ends.
            foreach (var commandEntity in _world.Where(out CommandAspect _))
            {
                if (state.IsActive == false)
                {
                    _log.Warn("AdvanceFlamePhaseCommand ignored because the flame is not active.");
                    continue;
                }

                if (state.IsTransitioning)
                {
                    _log.Warn($"AdvanceFlamePhaseCommand ignored because {state.CurrentPhase} -> " +
                        $"{state.NextPhase} is still running with {state.SecondsRemaining:0.###}s left.");
                    continue;
                }

                _StartTransition(ref state);
            }
        }

        private void _StartTransition(ref FlameStateComp state)
        {
            state.NextPhase = FlamePhaseCycle.Next(state.CurrentPhase);
            state.IsTransitioning = true;
            state.SecondsRemaining = state.TransitionDurationSeconds;
            state.Progress = 0f;
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILogService obj) => _log = obj;

        private sealed class CommandAspect : EcsAspect
        {
            public readonly EcsPool<AdvanceFlamePhaseCommand> Commands = Inc;
        }
    }
}
