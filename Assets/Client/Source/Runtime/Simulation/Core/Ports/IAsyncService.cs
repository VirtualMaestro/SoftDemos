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

    public interface IAsyncRequestService<TRequest> : IAsyncService where TRequest: struct
    {
        int Request(in TRequest request);
    }

    public interface IAsyncRequestService : IAsyncService
    {
        int Request();
    }

    public interface IAsyncResolveService<out TResult> : IAsyncService
    {
        TResult Resolve(int requestId);
    }
}
