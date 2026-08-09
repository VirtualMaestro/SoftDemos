namespace Game.Simulation.Ports
{
    /// <summary>Loads and unloads scenes. Handle and poll.</summary>
    /// <remarks>The adapter owns the async primitive. The simulation sees a request id and a status.</remarks>
    public interface ISceneService
    {
        /// <summary>Starts an additive load of <paramref name="sceneId"/>.</summary>
        int BeginLoad(string sceneId);

        /// <summary>Starts an unload of <paramref name="sceneId"/>.</summary>
        int BeginUnload(string sceneId);

        /// <summary>Status of <paramref name="requestId"/>. An unknown id reads as Pending.</summary>
        AsyncOpStatus Poll(int requestId);

        /// <summary>Drops the request. Call it after a terminal status, or the request table grows.</summary>
        void Release(int requestId);
    }
}
