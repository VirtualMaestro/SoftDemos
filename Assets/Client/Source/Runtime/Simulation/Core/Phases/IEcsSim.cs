using DCFApixels.DragonECS;

namespace Client.Simulation.Core.Phases
{
    /// <summary>The phase that advances the game. Simulation only.</summary>
    /// <remarks>
    /// A Sim system reads the commands Input wrote and its own state, and writes its own state and
    /// <c>*Event</c>s. It never touches an engine type — that is what the assembly split buys, and
    /// what lets a server or a test run <c>Input(); Sim(); Cleanup();</c> in a loop with no Present
    /// at all.
    /// <para>A realtime driver calls this phase k times per frame against a fixed timestep, and a
    /// networked one snapshots around it. Neither is visible from inside a Sim system, which is the
    /// point: the driver owns the schedule, the system owns one tick of it.</para>
    /// </remarks>
    public interface IEcsSim : IEcsProcess
    {
        void Sim();
    }
}
