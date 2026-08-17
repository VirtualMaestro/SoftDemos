using DCFApixels.DragonECS;

namespace Client.Simulation.PhoenixFlame
{
    /// <summary>Consumes Start/Reset commands: initializes the flame state from config or wipes it.</summary>
    public sealed class FlameSetupSystem : IEcsRun, IEcsInject<EcsWorld>
    {
        private readonly PhoenixFlameConfig _config;

        private EcsWorld _world;

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
                    _Reset(ref state);

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
        }

        private static void _Reset(ref FlameStateComp state) => state = default;

        public void Inject(EcsWorld obj) => _world = obj;

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
