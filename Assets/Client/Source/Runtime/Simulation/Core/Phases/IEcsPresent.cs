using DCFApixels.DragonECS;

namespace Client.Simulation.Core.Phases
{
    /// <summary>The phase that draws the world. Adapters only.</summary>
    /// <remarks>
    /// A Present system reads the world and writes the screen. It keeps no state but what it last
    /// rendered, and it produces no <c>*Command</c> or <c>*Event</c> — one written here is deleted
    /// unread, which is a silent loss rather than a lag; the analyzer's DEU0131 reports it.
    /// <para>Because <see cref="IEcsCleanup"/> closes the frame, an <c>*Event</c> is still alive
    /// when this phase reads it. That is what removes the version counters a view would otherwise
    /// need in order to notice that something changed: it finds the event instead of diffing state
    /// against a private copy.</para>
    /// <para>A headless driver simply never calls it. The phase order itself is the driver's —
    /// <c>EntryPoint</c> for the client, <c>PipelineTestDriver</c> for a headless tick.</para>
    /// </remarks>
    public interface IEcsPresent : IEcsProcess
    {
        void Present();
    }
}
