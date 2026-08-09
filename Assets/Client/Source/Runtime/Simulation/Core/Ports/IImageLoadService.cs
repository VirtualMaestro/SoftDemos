namespace Game.Simulation.Ports
{
    /// <summary>
    /// Remote image loading as a handle-and-poll port.
    ///
    /// <c>speakerName</c> accompanies the URL so adapters and diagnostics can preserve
    /// request identity when distinct speakers share or repeat an address.
    /// </summary>
    public interface IImageLoadService
    {
        /// <summary>Starts loading <paramref name="url"/> for <paramref name="speakerName"/>.</summary>
        int BeginLoad(string speakerName, string url);

        /// <summary>
        /// Current status of <paramref name="requestId"/>. An unknown or already released id
        /// reads as <see cref="AsyncOpStatus.Pending"/> — polling never throws.
        /// </summary>
        AsyncOpStatus Poll(int requestId);

        /// <summary>
        /// Opaque handle to the loaded image, valid only while the request is
        /// <see cref="AsyncOpStatus.Done"/>. Returns <c>0</c> in every other case.
        /// </summary>
        int ResolveHandle(int requestId);

        /// <summary>Releases the request and the image behind it.</summary>
        void Release(int requestId);
    }
}
