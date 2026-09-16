using DCFApixels.DragonECS;

namespace Client.Simulation.Core
{
    /// <summary>The read half of the world's singleton accessor, which the framework does not ship.</summary>
    /// <remarks>
    /// A pool says which of the two a system means — <c>Get(e)</c> takes a writable <c>ref</c> and
    /// says "this system writes this component", <c>Read(e)</c> returns <c>ref readonly</c> and says
    /// it only looks. <c>EcsWorld</c> declares <c>Get&lt;T&gt;()</c> alone, so a world component —
    /// the one piece of state every system in the pipeline can reach — was the place the question
    /// could not be asked at all.
    ///
    /// This is that word, and nothing else: it forwards to <c>Get&lt;T&gt;()</c> and narrows the
    /// return. Hold it the way the word means — <c>ref readonly var x = ref world.Read&lt;T&gt;()</c>
    /// — and keep <c>Get</c> for the body that writes through the <c>ref</c>.
    ///
    /// DELETE THIS FILE if DragonECS ever declares <c>Read&lt;T&gt;()</c> on the world. Call sites
    /// need no edit: <c>world.Read&lt;T&gt;()</c> resolves to the member instead, an instance method
    /// winning over an extension. DEU4150 looks the spelling up at the call site for that reason and
    /// does not care which of the two it finds.
    /// </remarks>
    public static class EcsWorldReadExtensions
    {
        public static ref readonly T Read<T>(this EcsWorld world) => ref world.Get<T>();
    }
}
