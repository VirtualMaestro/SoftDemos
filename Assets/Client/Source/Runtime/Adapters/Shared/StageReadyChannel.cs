namespace Client.Adapters.Shared
{
    /// <summary>Reports whether the shell and the live demo have painted themselves yet.</summary>
    /// <remarks>
    /// A loaded screen is not a drawn screen, at either level. <c>ScreenId.Demo</c> means only that
    /// the addressable scene landed. The stage system starts its atlas and background requests on
    /// that frame and they finish some frames later. The shell has the same gap on the first
    /// screen the player sees, and it lasts seconds on a real host. Presentation must keep the
    /// old screen up until then. This object owns nothing. Each stage system keeps its own
    /// handles, sprites and views.
    /// </remarks>
    public sealed class StageReadyChannel
    {
        /// <summary>True once the live demo stage system has assigned its background sprite.</summary>
        /// <remarks>
        /// This is not the same as the stage state <c>Ready</c>. Ace of Shadows spawns its cards
        /// over several frames and Phoenix Flame runs a start handshake. The screen is already
        /// covered by then, so waiting longer only adds a stall.
        /// </remarks>
        public bool IsDemoReady { get; private set; }

        /// <summary>True once <c>ShellStageSystem</c> has finished with its three addresses.</summary>
        /// <remarks>
        /// This includes the failure path, which is terminal and leaves the shell unskinned.
        /// A plain menu is still playable. A menu that never appears is not.
        /// </remarks>
        public bool IsShellReady { get; private set; }

        public void MarkDemoReady() => IsDemoReady = true;

        public void MarkShellReady() => IsShellReady = true;

        public void ClearDemo() => IsDemoReady = false;
    }
}
