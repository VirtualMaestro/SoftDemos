namespace Game.Simulation.Ports
{
    /// <summary>
    /// Asset loading by address, as a handle-and-poll port.
    ///
    /// The port never returns an engine object. A completed request resolves to an opaque handle
    /// id that only the adapter can turn back into a real asset — that is what keeps
    /// <c>Game.Simulation</c> free of <c>UnityEngine</c> while still letting a system decide
    /// *which* asset a view should show.
    /// </summary>
    public interface IAssetService
    {
        /// <summary>Starts loading the asset registered under <paramref name="address"/>.</summary>
        int BeginLoad(string address);

        /// <summary>
        /// Current status of <paramref name="requestId"/>. An unknown or already released id
        /// reads as <see cref="AsyncOpStatus.Pending"/> — polling never throws.
        /// </summary>
        AsyncOpStatus Poll(int requestId);

        /// <summary>
        /// Opaque handle to the loaded asset, valid only while the request is
        /// <see cref="AsyncOpStatus.Done"/>. Returns <c>0</c> in every other case.
        /// </summary>
        int ResolveHandle(int requestId);

        /// <summary>
        /// Releases the request and the asset behind it. Not calling this leaks the asset for the
        /// lifetime of the session.
        /// </summary>
        void Release(int requestId);
    }
}
