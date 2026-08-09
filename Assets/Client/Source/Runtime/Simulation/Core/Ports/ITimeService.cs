namespace Game.Simulation.Ports
{
    /// <summary>
    /// The simulation's only source of elapsed time. Systems never read a clock directly, so a
    /// test can advance time by whatever amount it needs without waiting for a frame.
    /// </summary>
    public interface ITimeService
    {
        /// <summary>Seconds elapsed since the previous tick.</summary>
        float DeltaSeconds { get; }
    }
}
