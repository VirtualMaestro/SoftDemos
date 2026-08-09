using Game.Simulation.Ports;

namespace Game.Simulation.Tests.Fakes
{
    /// <summary>
    /// <see cref="ITimeService"/> a test drives by hand. Set <see cref="DeltaSeconds"/>, tick the
    /// pipeline, assert — no frames, no waiting, no flakiness.
    /// </summary>
    public sealed class FakeTime : ITimeService
    {
        public float DeltaSeconds { get; set; }

        public override string ToString() => $"FakeTime(DeltaSeconds={DeltaSeconds})";
    }
}
