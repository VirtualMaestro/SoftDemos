using Game.Simulation.Ports;

namespace Game.Adapters.Services
{
    /// <summary>
    /// <see cref="IRandomService"/> backed by <see cref="UnityEngine.Random"/>.
    /// </summary>
    public sealed class UnityRandomService : IRandomService
    {
        public int Range(int minInclusive, int maxExclusive)
        {
            // UnityEngine.Random.Range throws on an inverted range in some versions and silently
            // returns minInclusive in others. Pin the port's documented behaviour here so no
            // system has to care which Unity version it is running on.
            if (maxExclusive <= minInclusive)
                return minInclusive;

            return UnityEngine.Random.Range(minInclusive, maxExclusive);
        }
    }
}
