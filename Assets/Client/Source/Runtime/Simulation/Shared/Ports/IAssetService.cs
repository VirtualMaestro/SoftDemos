namespace Client.Simulation.Shared.Ports
{
    /// <summary>Loads an asset by address. Handle and poll.</summary>
    /// <remarks>
    /// The port never returns an engine object. A finished request gives an opaque handle id that
    /// only the adapter can resolve. A system can choose the asset without a Unity reference.
    /// </remarks>
    public interface IAssetService
    {
        /// <summary>Starts to load the asset at <paramref name="address"/>.</summary>
        int BeginLoad(string address);

        /// <summary>Status of <paramref name="requestId"/>. An unknown id reads as Pending.</summary>
        AsyncOpStatus Poll(int requestId);

        /// <summary>Handle to the asset. Valid only while the request is Done, else <c>0</c>.</summary>
        int ResolveHandle(int requestId);

        /// <summary>Releases the request and the asset. Without this the asset stays in memory.</summary>
        void Release(int requestId);
    }
}
