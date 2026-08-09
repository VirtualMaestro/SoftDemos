namespace Game.Simulation.Ports
{
    /// <summary>
    /// Scene loading as a handle-and-poll port. The adapter owns whatever asynchronous primitive
    /// the underlying loader uses; the simulation only sees an <see cref="int"/> request id and a
    /// status it can read every tick.
    /// </summary>
    public interface ISceneService
    {
        /// <summary>Starts loading <paramref name="sceneId"/> additively. Returns the request id.</summary>
        int BeginLoad(string sceneId);

        /// <summary>Starts unloading <paramref name="sceneId"/>. Returns the request id.</summary>
        int BeginUnload(string sceneId);

        /// <summary>
        /// Current status of <paramref name="requestId"/>. An unknown or already released id
        /// reads as <see cref="AsyncOpStatus.Pending"/> — polling never throws.
        /// </summary>
        AsyncOpStatus Poll(int requestId);

        /// <summary>
        /// Drops the request. A system that read a terminal status must call this, otherwise the
        /// adapter's request table grows for the lifetime of the session.
        /// </summary>
        void Release(int requestId);
    }
}
