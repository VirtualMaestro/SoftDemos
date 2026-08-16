using System.Collections.Generic;
using Client.Simulation.Ports;
using DCFApixels.DragonECS;

namespace Client.Simulation.PhoenixFlame
{
    public sealed class FlamePhaseRequestSystem : IEcsRun, IEcsInject<EcsWorld>, IEcsInject<ILog>
    {
        private readonly List<int> _entitiesToDelete = new();

        private EcsWorld _world;
        private ILog _log;

        public void Run()
        {
            ref var state = ref _world.Get<FlameStateComp>();

            // Every command entity is consumed even when the press is ignored: a held button emits
            // one per tick, and leaving them alive would build a backlog that fires the moment the
            // in-flight transition ends.
            foreach (var entityId in _world.Where(out CommandAspect _))
            {
                _entitiesToDelete.Add(entityId);

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

            foreach (var entityId in _entitiesToDelete)
                _world.DelEntity(entityId);

            _entitiesToDelete.Clear();
        }

        private void _StartTransition(ref FlameStateComp state)
        {
            state.NextPhase = FlamePhaseCycle.Next(state.CurrentPhase);
            state.IsTransitioning = true;
            state.SecondsRemaining = state.TransitionDurationSeconds;
            state.Progress = 0f;

            _log.Info($"Flame transition {state.CurrentPhase} -> {state.NextPhase} over " +
                $"{state.TransitionDurationSeconds:0.###}s.");
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILog obj) => _log = obj;

        private sealed class CommandAspect : EcsAspect
        {
            public EcsPool<AdvanceFlamePhaseCommand> Commands = Inc;
        }
    }
}
