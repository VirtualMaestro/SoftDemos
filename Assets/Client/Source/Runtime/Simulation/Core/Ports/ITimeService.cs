namespace Game.Simulation.Ports
{
    /// <summary>The only source of elapsed time in the simulation.</summary>
    /// <remarks>No system reads a clock, so a test can move time forward without a frame.</remarks>
    public interface ITimeService
    {
        /// <summary>Seconds since the previous tick.</summary>
        float DeltaSeconds { get; }
    }
}
