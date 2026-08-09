using Game.Simulation.Ports;
using UnityEngine;

namespace Game.Adapters.Services
{
    /// <summary>
    /// <see cref="ITimeService"/> backed by the Unity player loop.
    /// </summary>
    public sealed class UnityTimeService : ITimeService
    {
        /// <summary>
        /// Deliberately <c>Time.deltaTime</c> and not <c>unscaledDeltaTime</c>: pausing a demo is
        /// a gameplay decision, and routing it through <c>Time.timeScale</c> keeps every system
        /// paused without a single system knowing that "paused" exists.
        /// </summary>
        public float DeltaSeconds => Time.deltaTime;
    }
}
