using Game.Simulation.Ports;
using UnityEngine;

namespace Game.Adapters.Services
{
    /// <summary><see cref="ITimeService"/> on the Unity player loop.</summary>
    public sealed class UnityTimeService : ITimeService
    {
        /// <summary>Uses <c>Time.deltaTime</c>, not <c>unscaledDeltaTime</c>.</summary>
        /// <remarks><c>Time.timeScale</c> then pauses every system, and no system needs to know about a pause.</remarks>
        public float DeltaSeconds => Time.deltaTime;
    }
}
