namespace Client.Simulation.PhoenixFlame
{
    /// <summary>The three colours of the flame. The cycle follows this order.</summary>
    /// <remarks>Keep the values contiguous from zero. A new value in the middle changes the cycle.</remarks>
    public enum FlamePhase
    {
        Orange = 0,
        Green = 1,
        Blue = 2
    }

    /// <summary>The only place that knows the length of the cycle.</summary>
    /// <remarks>Call <see cref="Next"/> instead of <c>% 3</c>, so a fourth phase is one change here.</remarks>
    public static class FlamePhaseCycle
    {
        public const int Count = 3;

        public static FlamePhase Next(FlamePhase phase) => (FlamePhase)(((int)phase + 1) % Count);

        // Do not use Enum.IsDefined. It boxes the value and uses reflection, which allocates
        // on every call and does not survive IL2CPP stripping well.
        public static bool IsDefined(FlamePhase phase) =>
            phase == FlamePhase.Orange || phase == FlamePhase.Green || phase == FlamePhase.Blue;
    }
}
