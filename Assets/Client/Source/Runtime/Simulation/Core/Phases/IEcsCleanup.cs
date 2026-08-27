using DCFApixels.DragonECS;

namespace Client.Simulation.Core.Phases
{
    /// <summary>The phase that closes the frame by deleting every one-frame component. Either side.</summary>
    /// <remarks>
    /// Last in the frame, and the only phase that deletes a <c>*Command</c> or an <c>*Event</c>.
    /// That is what makes the suffix readable as a lifetime: a one-frame component lives from its
    /// <c>Add</c> until Cleanup, is visible to every phase after its producer, and its death is not
    /// a fact about whether some consumer's guard let it reach a <c>Del</c> call.
    /// <para>One Cleanup system per feature, on whichever side of the boundary owns the type — a
    /// simulation Cleanup cannot see an adapter-declared component, so both roots are allowed. It
    /// deletes and does nothing else.</para>
    /// <para>Cleanup closes the frame rather than opening it so that the previous frame's one-frame
    /// data never crosses the frame boundary, where scene teardown, snapshots and the engine's own
    /// callbacks run.</para>
    /// </remarks>
    public interface IEcsCleanup : IEcsProcess
    {
        void Cleanup();
    }
}
