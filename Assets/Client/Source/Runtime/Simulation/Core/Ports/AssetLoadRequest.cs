namespace Client.Simulation.Core.Ports
{
    /// <summary>Asks <see cref="IAssetService"/> for the asset at an address.</summary>
    public readonly struct AssetLoadRequest
    {
        /// <summary>The content address, in the project's three-segment form.</summary>
        public readonly string Address;

        public AssetLoadRequest(string address) => Address = address;
    }
}
