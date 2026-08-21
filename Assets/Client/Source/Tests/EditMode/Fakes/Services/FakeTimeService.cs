using Client.Simulation.Shared.Ports;

namespace Client.Simulation.Tests.Fakes.Services
{
    /// <summary>
    /// <see cref="ITimeService"/> a test drives by hand. Set <see cref="DeltaSeconds"/>, tick the
    /// pipeline, assert — no frames, no waiting, no flakiness.
    /// </summary>
    public sealed class FakeTimeService : ITimeService
    {
        public float DeltaSeconds { get; set; }

        public override string ToString() => $"FakeTimeService(DeltaSeconds={DeltaSeconds})";
    }
}
