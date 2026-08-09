namespace Game.Simulation.PhoenixFlame
{
    /// <summary>
    /// The three colours the flame cycles through. <b>The cycle order is the declaration order</b>:
    /// <see cref="FlamePhaseCycle.Next"/> walks the underlying numbers and wraps at the end, so the
    /// values must stay contiguous from zero and inserting one in the middle changes the cycle.
    /// </summary>
    public enum FlamePhase
    {
        Orange = 0,
        Green = 1,
        Blue = 2
    }

    /// <summary>
    /// The single place that knows how long the cycle is. Systems and tests call <see cref="Next"/>
    /// instead of inlining <c>% 3</c>, so adding a fourth phase stays a one-file change.
    /// </summary>
    public static class FlamePhaseCycle
    {
        public const int Count = 3;

        public static FlamePhase Next(FlamePhase phase) => (FlamePhase)(((int)phase + 1) % Count);

        // Not Enum.IsDefined: it boxes the value and reflects over the enum metadata, which costs
        // an allocation per call and is exactly the shape IL2CPP strips badly. Three comparisons
        // are the entire check.
        public static bool IsDefined(FlamePhase phase) =>
            phase == FlamePhase.Orange || phase == FlamePhase.Green || phase == FlamePhase.Blue;
    }
}
