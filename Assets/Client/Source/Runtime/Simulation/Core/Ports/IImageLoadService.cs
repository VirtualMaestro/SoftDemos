namespace Client.Simulation.Ports
{
    /// <summary>Loads a remote image. Handle and poll.</summary>
    /// <remarks>
    /// The speaker name goes with the URL, so an adapter can keep the requests apart when two
    /// speakers share the same address.
    /// </remarks>
    public interface IImageLoadService
    {
        /// <summary>Starts to load <paramref name="url"/> for <paramref name="speakerName"/>.</summary>
        int BeginLoad(string speakerName, string url);

        /// <summary>Status of <paramref name="requestId"/>. An unknown id reads as Pending.</summary>
        AsyncOpStatus Poll(int requestId);

        /// <summary>Handle to the image. Valid only while the request is Done, else <c>0</c>.</summary>
        int ResolveHandle(int requestId);

        /// <summary>Releases the request and the image.</summary>
        void Release(int requestId);
    }
}
