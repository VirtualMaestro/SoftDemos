using Client.Simulation.Core.Ports;

namespace Client.Adapters.Shared.Stage
{
    /// <summary>The one transition function every stage input system steps its state with.</summary>
    /// <remarks>
    /// There used to be four copies of this machine, one per stage system, each written as a
    /// <c>switch</c> over a private <c>_state</c> field. The state is world data now
    /// (adr-data-placement-is-decided-on-three-axes rule 6), and what is left of the machine is a
    /// pure function over it — no base class, no feature parameter, no side effect.
    /// <para>A caller computes the next state, then applies the side effects of the EDGE it took:
    /// request on <c>Idle -> Loading</c>, resolve and hand over on <c>Loading -> Ready</c>, release
    /// and delete on <c>Closing -> Idle</c>. Which edge it took is the pair
    /// <c>(current, next)</c>, which is why this returns a state and does nothing else.</para>
    /// </remarks>
    public static class StageTransitions
    {
        /// <summary>The state a stage moves to this frame.</summary>
        /// <remarks>
        /// The edges, in the order they are tested:
        /// <list type="table">
        /// <item><term><c>Loading</c>/<c>Ready</c> + <paramref name="unloading"/> or no screen</term>
        /// <description><c>Closing</c> — the exit wins over everything else, because the screen is a
        /// Unity object the scene unload can destroy before this phase runs again</description></item>
        /// <item><term><c>Idle</c> + screen present and changed</term><description><c>Loading</c></description></item>
        /// <item><term><c>Loading</c> + <see cref="AsyncOpStatus.Failed"/></term><description><c>Idle</c></description></item>
        /// <item><term><c>Loading</c> + <see cref="AsyncOpStatus.Done"/></term><description><c>Ready</c></description></item>
        /// <item><term><c>Closing</c></term><description><c>Idle</c>, always — a teardown is one frame</description></item>
        /// </list>
        /// </remarks>
        /// <param name="current">The stage's state as its component carries it.</param>
        /// <param name="screenPresent">
        /// This stage's screen resolves right now AND the navigation state still selects this stage.
        /// The shell, which lives with the world, passes <c>true</c>.
        /// </param>
        /// <param name="screenChanged">
        /// The screen present is not the one this stage opened on — its instance id differs from the
        /// one the component recorded. Only <c>Idle</c> reads it.
        /// </param>
        /// <param name="unloading">
        /// The scene is going away. Separate from <paramref name="screenPresent"/> on purpose: the
        /// screen object can still resolve on the frame the unload is announced. The shell passes
        /// <c>false</c> and never reaches <c>Closing</c>.
        /// </param>
        /// <param name="load">
        /// The worst status of the content requests in flight — <see cref="AsyncOpStatus.Pending"/>
        /// when any is still running. Only <c>Loading</c> reads it.
        /// </param>
        public static StageState Next(StageState current, bool screenPresent, bool screenChanged,
            bool unloading, AsyncOpStatus load)
        {
            switch (current)
            {
                case StageState.Idle:
                    return screenPresent && screenChanged ? StageState.Loading : StageState.Idle;

                case StageState.Loading:
                    if (unloading || !screenPresent)
                        return StageState.Closing;

                    return load switch
                    {
                        AsyncOpStatus.Failed => StageState.Idle,
                        AsyncOpStatus.Done => StageState.Ready,
                        _ => StageState.Loading,
                    };

                case StageState.Ready:
                    return unloading || !screenPresent ? StageState.Closing : StageState.Ready;

                default:
                    return StageState.Idle;
            }
        }
    }
}
