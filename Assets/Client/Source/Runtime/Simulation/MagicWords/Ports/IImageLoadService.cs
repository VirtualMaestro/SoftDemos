using Client.Simulation.Core.Ports;

namespace Client.Simulation.MagicWords.Ports
{
    /// <summary>Loads a speaker's avatar. Request and poll.</summary>
    /// <remarks>
    /// No result, for the same reason as <see cref="IAssetService"/>: a sprite may not cross the
    /// boundary. A Done request means the image sits in the adapter's table under the request id,
    /// and the adapter's own <c>TryGetSprite</c> resolves it on the engine side.
    /// </remarks>
    public interface IImageLoadService : IAsyncRequestService<ImageLoadRequest>
    {
    }
}
