using Client.Simulation.Core.Phases;
using DCFApixels.DragonECS;

namespace Client.Simulation.Tests
{
    /// <summary>The headless driver: one tick is the whole frame, in the contract's order.</summary>
    /// <remarks>
    /// The same order <c>Boot</c> runs — the systems under test cannot tell the two apart,
    /// which is the point of the driver being one file. It is a single call rather than the
    /// server-shaped <c>Input(); Sim(); Cleanup();</c> because one fixture's fake adapter half
    /// declares Present; where no system answers, the call costs nothing.
    /// <para>A test that wants to assert on a tick's own <c>*Command</c> or <c>*Event</c> calls the
    /// phases itself and asserts before <c>Cleanup()</c> — after it, the one-frame components of
    /// that tick are gone, which is exactly what they promise.</para>
    /// </remarks>
    internal static class PipelineTestDriver
    {
        public static void Tick(this EcsPipeline pipeline)
        {
            pipeline.Input();
            pipeline.Sim();
            pipeline.Present();
            pipeline.Cleanup();
        }
    }
}
