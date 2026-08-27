using DCFApixels.DragonECS;

namespace Client.Simulation.Core.Phases
{
    /// <summary>The phase that turns the outside world into commands. Adapters only.</summary>
    /// <remarks>
    /// First in the frame. An Input system reads what happened outside the world — a recorded
    /// button press, a port that finished, a completion queue an engine callback filled — and
    /// writes it in: a <c>*Command</c> for the simulation, or a component the adapter owns.
    /// <para>It is the only phase allowed to write a <c>*Command</c> the simulation reads, and the
    /// reason a press is a property on a view rather than a world write from uGUI's callback: the
    /// frame a command lands in is the frame this phase ran, and nothing else decides it.</para>
    /// <para>The four phases are process interfaces rather than pipeline layers because a layer is
    /// a position in one builder, and a position cannot be read from a class. A system says when
    /// it runs by the interface it implements; the driver says in what order the phases run. See
    /// the analyzer's DEU0133 and DEU0135.</para>
    /// </remarks>
    public interface IEcsInput : IEcsProcess
    {
        void Input();
    }
}
