namespace Client.Simulation.Core.Ports
{
    /// <summary>Loads and unloads scenes. Request and poll.</summary>
    /// <remarks>
    /// The adapter owns the async primitive. The simulation sees a request id and a status.
    /// <para>Two kinds of work, so the port inherits the request level twice: a loaded scene and an
    /// unloaded one are not one operation with a flag. There is no result either way — the
    /// completion status itself is the whole outcome.</para>
    /// </remarks>
    public interface ISceneService : IAsyncRequestService<SceneLoadRequest>, IAsyncRequestService<SceneUnloadRequest>
    {
    }
}
