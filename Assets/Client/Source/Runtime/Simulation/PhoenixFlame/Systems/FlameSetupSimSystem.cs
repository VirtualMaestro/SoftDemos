using System.Runtime.CompilerServices;
using Client.Simulation.Core.Phases;
using Client.Simulation.PhoenixFlame.Components;
using Client.Simulation.PhoenixFlame.Components.Commands;
using DCFApixels.DragonECS;

namespace Client.Simulation.PhoenixFlame.Systems
{
    /// <summary>Consumes Start/Reset commands: initializes the flame state from config or wipes it.</summary>
    internal sealed class FlameSetupSimSystem : IEcsSim, IEcsInject<EcsWorld>
    {
        private readonly PhoenixFlameConfig _config;

        private EcsWorld _world;

        public FlameSetupSimSystem(PhoenixFlameConfig config)
        {
            _config = config;
        }

        public void Sim()
        {
            ref var state = ref _world.Get<FlameStateComp>();

            // The reset loop is written above the start loop, and the cost of swapping them is
            // what is worth recording: closing and reopening a demo produces both commands at once,
            // and starting a flame that is then wiped leaves it dead to every press.
            foreach (var resetEntity in _world.Where(out ResetCommandAspect _))
            {
                // Resetting an inactive flame is a no-op, not a mistake: closing a demo that was
                // never opened is a normal path, so it is read silently.
                if (state.IsActive)
                    _Reset();
            }

            foreach (var startEntity in _world.Where(out StartCommandAspect _))
            {
                if (state.IsActive)
                    _Reset();

                _Start(ref state);
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

        /// <summary>Takes no state: the world component IS the state, and a parameter that only
        /// ever gets wiped carries nothing in.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _Reset() => _world.Get<FlameStateComp>() = default;

        public void Inject(EcsWorld obj) => _world = obj;

        private sealed class StartCommandAspect : EcsAspect
        {
            public readonly EcsPool<StartFlameCommand> Commands = Inc;
        }

        private sealed class ResetCommandAspect : EcsAspect
        {
            public readonly EcsPool<ResetFlameCommand> Commands = Inc;
        }
    }
}
