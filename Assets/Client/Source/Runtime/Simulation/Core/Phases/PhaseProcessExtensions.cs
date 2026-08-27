using DCFApixels.DragonECS;

namespace Client.Simulation.Core.Phases
{
    /// <summary>The four calls a driver makes. One line each, in the framework's own idiom.</summary>
    /// <remarks>
    /// Modelled on DragonECS' <c>UnityProcessExtensions</c>, which exposes <c>LateRun()</c> the
    /// same way. It exists so a driver reads as the contract states it —
    /// <c>Input(); Sim();</c> then <c>Present(); Cleanup();</c> — instead of four
    /// <c>GetRunnerInstance</c> calls, and so a test fixture can drive a headless tick in three
    /// words. Every driver in the project is one file: this is the vocabulary they share.
    /// </remarks>
    public static class PhaseProcessExtensions
    {
        public static void Input(this EcsPipeline pipeline) =>
            pipeline.GetRunnerInstance<EcsInputRunner>().Input();

        public static void Sim(this EcsPipeline pipeline) =>
            pipeline.GetRunnerInstance<EcsSimRunner>().Sim();

        public static void Present(this EcsPipeline pipeline) =>
            pipeline.GetRunnerInstance<EcsPresentRunner>().Present();

        public static void Cleanup(this EcsPipeline pipeline) =>
            pipeline.GetRunnerInstance<EcsCleanupRunner>().Cleanup();
    }
}
