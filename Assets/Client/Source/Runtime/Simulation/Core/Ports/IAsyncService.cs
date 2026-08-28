namespace Client.Simulation.Core.Ports
{
    /// <summary>What every asynchronous port can do, whatever it loads.</summary>
    /// <remarks>
    /// The simulation does not await and does not take callbacks. It asks for work, keeps the
    /// request id, and polls that id each tick until the status is terminal. Then it releases.
    /// <para>The id is the whole identity of the work: it names the request while it runs and the
    /// result once it is done, and it stops meaning anything the moment it is released. There is
    /// no second id. An adapter that has to hand back something the boundary forbids — a sprite, a
    /// texture — keeps it in a table keyed by this same id and exposes the lookup on its own
    /// concrete type, below the port.</para>
    /// <para>A port INHERITS these methods and never respells them; the analyzer's DEU0123 reports
    /// a port that declares its own <c>Poll</c> or <c>Release</c>.</para>
    /// </remarks>
    public interface IAsyncService
    {
        /// <summary>Status of <paramref name="requestId"/>. An unknown or released id reads as
        /// Pending — a port never throws across the boundary.</summary>
        AsyncOpStatus Poll(int requestId);

        /// <summary>Drops the request and everything the adapter held for it. Call it after a
        /// terminal status, or the request table grows without bound.</summary>
        void Release(int requestId);
    }

    /// <summary>An asynchronous port whose work is described by <typeparamref name="TRequest"/>.</summary>
    /// <remarks>
    /// The request is a named readonly struct declared beside the port, never a parameter list:
    /// a parameter list cannot be read by a rule, cannot gain a field without changing every call
    /// site, and loses its element names the moment somebody reaches for a tuple.
    /// <para>A port with two kinds of work inherits this level twice — see
    /// <see cref="ISceneService"/>, which loads and unloads.</para>
    /// </remarks>
    public interface IAsyncService<in TRequest> : IAsyncService
    {
        /// <summary>Starts the work and returns the id that names it from here on.</summary>
        int Request(TRequest request);
    }

    /// <summary>An asynchronous port that hands a result back to the simulation.</summary>
    /// <remarks>
    /// Only for a result the boundary allows to cross — plain simulation data. A port that loads an
    /// engine object stops at <see cref="IAsyncService{TRequest}"/> and lets its adapter resolve
    /// the object from the request id on the engine side of the line.
    /// </remarks>
    public interface IAsyncService<in TRequest, out TResult> : IAsyncService<TRequest>
    {
        /// <summary>The result. Valid only while the request is Done, else the type's default.</summary>
        TResult Resolve(int requestId);
    }
}
