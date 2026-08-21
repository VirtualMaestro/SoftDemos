using Client.Simulation.Shared.Ports;
using Client.Simulation.PhoenixFlame.Components;
using DCFApixels.DragonECS;

namespace Client.Simulation.PhoenixFlame.Systems
{
    /// <summary>
    /// Counts the running transition down each frame, updates its 0..1 progress, and swaps the
    /// current phase when time runs out.
    /// </summary>
    public sealed class FlameTransitionSystem : IEcsRun, IEcsInject<EcsWorld>,
        IEcsInject<ITimeService>
    {
        private EcsWorld _world;
        private ITimeService _time;

        public void Run()
        {
            ref var state = ref _world.Get<FlameStateComp>();

            if (state.IsActive == false)
                return;

            if (state.IsTransitioning == false)
                return;

            // The division below is safe without a guard: PhoenixFlameConfig rejects a non-positive
            // or non-finite duration, and FlameSetupSystem is the only writer — it copies the config
            // value on start and zeroes the whole component on reset, which also clears
            // IsTransitioning and returns above.
            state.SecondsRemaining -= _time.DeltaSeconds;

            if (state.SecondsRemaining <= 0f)
            {
                _Complete(ref state);
                return;
            }

            state.Progress = _Clamp01(1f - state.SecondsRemaining / state.TransitionDurationSeconds);
        }

        private void _Complete(ref FlameStateComp state)
        {
            // The overshoot is dropped on purpose. CardCadenceSystem carries its remainder because
            // it repeats on a fixed interval; this is a one-shot transition that ends here and only
            // a new press opens the next one, so there is no interval to carry anything into.
            state.CurrentPhase = state.NextPhase;
            state.IsTransitioning = false;
            state.SecondsRemaining = 0f;
            state.Progress = 1f;
            state.PhaseChangeCount++;
        }

        private static float _Clamp01(float value)
        {
            if (value < 0f)
                return 0f;

            return value > 1f ? 1f : value;
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ITimeService obj) => _time = obj;
    }
}
