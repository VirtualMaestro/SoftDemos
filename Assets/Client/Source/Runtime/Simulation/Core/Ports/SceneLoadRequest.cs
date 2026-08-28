namespace Client.Simulation.Core.Ports
{
    /// <summary>Asks <see cref="ISceneService"/> to load a scene additively.</summary>
    public readonly struct SceneLoadRequest
    {
        /// <summary>The scene's address.</summary>
        public readonly string SceneId;

        public SceneLoadRequest(string sceneId) => SceneId = sceneId;
    }
}
