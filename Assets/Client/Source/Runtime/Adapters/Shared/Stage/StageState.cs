namespace Client.Adapters.Shared.Stage
{
    /// <summary>The lifecycle every stage input system steps through, one step per frame.</summary>
    /// <remarks>
    /// Each system uses the subset it needs: <c>ShellStageInpSystem</c> never enters
    /// <see cref="Closing"/>, because the shell lives for the whole session.
    /// <para>There used to be a <c>Starting</c> step between <see cref="Loading"/> and
    /// <see cref="Ready"/>, entered only by the flame demo, which wrote <c>StartFlameCommand</c>
    /// from <c>LateRun</c> and had to wait for the next frame's <c>Run</c> to consume it. The
    /// phases removed the wait — Input runs before Sim in the same frame — and the step went with
    /// it. A state that exists to absorb a scheduling artefact is the artefact.</para>
    /// </remarks>
    public enum StageState
    {
        /// <summary>No scene acquired. Watching for the demo screen to appear.</summary>
        Idle,

        /// <summary>The screen is acquired and content requests are in flight.</summary>
        Loading,

        /// <summary>Running: draining presses and ports into commands every frame.</summary>
        Ready,

        /// <summary>The scene is going away. The next step is a teardown back to <see cref="Idle"/>.</summary>
        Closing,
    }
}
