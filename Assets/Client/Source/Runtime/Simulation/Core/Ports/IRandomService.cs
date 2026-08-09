namespace Game.Simulation.Ports
{
    /// <summary>
    /// The simulation's only source of randomness. A fake returns a scripted sequence, which is
    /// what makes a shuffling or spawning system assertable.
    /// </summary>
    public interface IRandomService
    {
        /// <summary>
        /// Returns a value in <c>[minInclusive, maxExclusive)</c>. An empty range
        /// (<c>maxExclusive &lt;= minInclusive</c>) returns <paramref name="minInclusive"/>.
        /// </summary>
        int Range(int minInclusive, int maxExclusive);
    }
}
