namespace Client.Adapters.Shared.Stage
{
    /// <summary>The lifecycle every stage system steps through in <c>LateRun</c>.</summary>
    /// <remarks>
    /// Each system uses the subset it needs: <see cref="Starting"/> is entered only by
    /// <c>PhoenixFlameStageSystem</c>, which must wait one frame for the simulation to consume
    /// <c>StartFlameCommand</c> before it can snap the Animator to the configured phase.
    /// <c>ShellStageSystem</c> never enters <see cref="Closing"/>; the shell lives for the whole
    /// session.
    /// </remarks>
    public enum StageState
    {
        /// <summary>No scene acquired. Watching for the demo screen to appear.</summary>
        Idle,

        /// <summary>The screen is acquired and content requests are in flight.</summary>
        Loading,

        /// <summary>Content is on screen; waiting for the simulation to start its state.</summary>
        Starting,

        /// <summary>Running: mirroring simulation state onto the view every frame.</summary>
        Ready,

        /// <summary>The scene is going away. The next step is a teardown back to <see cref="Idle"/>.</summary>
        Closing,
    }
}
