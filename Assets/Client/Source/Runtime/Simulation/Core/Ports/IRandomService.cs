namespace Client.Simulation.Ports
{
    /// <summary>The only source of randomness in the simulation.</summary>
    /// <remarks>A fake returns a fixed sequence, so a shuffle or a spawn becomes testable.</remarks>
    public interface IRandomService
    {
        /// <summary>
        /// Returns a value in <c>[minInclusive, maxExclusive)</c>. An empty range returns
        /// <paramref name="minInclusive"/>.
        /// </summary>
        int Range(int minInclusive, int maxExclusive);
    }
}
