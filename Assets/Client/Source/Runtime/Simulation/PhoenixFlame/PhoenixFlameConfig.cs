using System;

namespace Client.Simulation.PhoenixFlame
{
    /// <summary>
    /// Immutable setup values for the Phoenix Flame simulation. The transition duration is copied
    /// into <see cref="FlameStateComp.TransitionDurationSeconds"/> when the demo starts, so
    /// presentation may shorten a running transition without touching the config.
    /// </summary>
    public sealed class PhoenixFlameConfig
    {
        public PhoenixFlameConfig(
            FlamePhase startPhase = FlamePhase.Orange,
            float transitionDurationSeconds = 1f)
        {
            if (FlamePhaseCycle.IsDefined(startPhase) == false)
                throw new ArgumentOutOfRangeException(nameof(startPhase), startPhase, "Start phase is not a defined flame phase.");
            // NaN fails every comparison, infinity passes the positive test and then never counts
            // down, so both need naming here rather than a single `<= 0f`.
            if (float.IsNaN(transitionDurationSeconds) || float.IsInfinity(transitionDurationSeconds))
                throw new ArgumentOutOfRangeException(nameof(transitionDurationSeconds), transitionDurationSeconds, "Transition duration must be a finite number.");

            if (transitionDurationSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(transitionDurationSeconds), transitionDurationSeconds, "Transition duration must be positive.");

            StartPhase = startPhase;
            TransitionDurationSeconds = transitionDurationSeconds;
        }

        public FlamePhase StartPhase { get; }
        public float TransitionDurationSeconds { get; }
    }
}
