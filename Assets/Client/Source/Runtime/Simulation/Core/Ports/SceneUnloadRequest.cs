namespace Client.Simulation.Core.Ports
{
    /// <summary>Asks <see cref="ISceneService"/> to unload a scene.</summary>
    /// <remarks>
    /// A separate type from <see cref="SceneLoadRequest"/> rather than a flag on it: the two are
    /// different work with the same shape, and a bool parameter is what makes a call site
    /// unreadable at the point it matters most.
    /// </remarks>
    public readonly struct SceneUnloadRequest
    {
        /// <summary>The scene's address.</summary>
        public readonly string SceneId;

        public SceneUnloadRequest(string sceneId) => SceneId = sceneId;
    }
}
