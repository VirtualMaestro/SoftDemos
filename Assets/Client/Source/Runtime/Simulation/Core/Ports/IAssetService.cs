namespace Client.Simulation.Core.Ports
{
    /// <summary>Loads an asset by address. Request and poll.</summary>
    /// <remarks>
    /// The port never returns an engine object, so it has no result at all: a Done request means
    /// the asset is in the adapter's table under the request id, and the adapter's own
    /// <c>TryGetAsset</c> — a signature this interface is not allowed to carry — is what turns it
    /// back into a <c>UnityEngine.Object</c>. A system can therefore choose an asset without ever
    /// holding a Unity reference.
    /// </remarks>
    public interface IAssetService : IAsyncRequestService<AssetLoadRequest>
    {
    }
}
