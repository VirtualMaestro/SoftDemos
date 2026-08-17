using Client.Simulation.Ports;

namespace Client.Adapters.Services
{
    /// <summary><see cref="IRandomService"/> on <see cref="UnityEngine.Random"/>.</summary>
    public sealed class UnityRandomService : IRandomService
    {
        public int Range(int minInclusive, int maxExclusive)
        {
            // Unity is not consistent on an inverted range. Some versions throw, others do not.
            // Apply the behaviour the port documents.
            if (maxExclusive <= minInclusive)
                return minInclusive;

            return UnityEngine.Random.Range(minInclusive, maxExclusive);
        }
    }
}
