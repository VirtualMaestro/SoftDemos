using Client.Simulation.Ports;
using DCFApixels.DragonECS;

namespace Client.Simulation.PhoenixFlame
{
    /// <summary>Consumes Start/Reset commands: initializes the flame state from config or wipes it.</summary>
    public sealed class FlameSetupSystem : IEcsRun, IEcsInject<EcsWorld>, IEcsInject<ILog>
    {
        private readonly PhoenixFlameConfig _config;

        private EcsWorld _world;
        private ILog _log;

        public FlameSetupSystem(PhoenixFlameConfig config)
        {
            _config = config;
        }

        public void Run()
        {
            ref var state = ref _world.Get<FlameStateComp>();

            // Reset is consumed before Start so that a close-then-open in the same tick leaves the
            // flame active. The reverse order would start it and then wipe it, and the demo would
            // open with a dead flame that no press can revive.
            foreach (var entityId in _world.Where(out ResetCommandAspect _))
            {
                // Resetting an inactive flame is a no-op, not a mistake: closing a demo that was
                // never opened is a normal path, so it is consumed silently.
                if (state.IsActive)
                    _Reset(ref state);

                _world.DelEntity(entityId);
            }

            foreach (var entityId in _world.Where(out StartCommandAspect _))
            {
                if (state.IsActive)
                {
                    _log.Info("Start requested while the flame is already active; restarting.");
                    _Reset(ref state);
                }

                _Start(ref state);
                _world.DelEntity(entityId);
            }
        }

        private void _Start(ref FlameStateComp state)
        {
            state = default;
            state.IsActive = true;
            state.CurrentPhase = _config.StartPhase;
            state.NextPhase = _config.StartPhase;
            state.TransitionDurationSeconds = _config.TransitionDurationSeconds;

            _log.Info($"Flame started at {_config.StartPhase} with a " +
                $"{_config.TransitionDurationSeconds:0.###}s transition.");
        }

        private void _Reset(ref FlameStateComp state)
        {
            var previousPhase = state.CurrentPhase;
            var phaseChangeCount = state.PhaseChangeCount;
            state = default;

            _log.Info($"Flame reset from {previousPhase} after {phaseChangeCount} phase change(s).");
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILog obj) => _log = obj;

        private sealed class StartCommandAspect : EcsAspect
        {
            public EcsPool<StartFlameCommand> Commands = Inc;
        }

        private sealed class ResetCommandAspect : EcsAspect
        {
            public EcsPool<ResetFlameCommand> Commands = Inc;
        }
    }
}
