using Client.Adapters.Shared.Stage;
using DCFApixels.DragonECS;

namespace Client.Adapters.PhoenixFlame.Components
{
    /// <summary>
    /// The flame stage's lifecycle and the ids it holds open. Adapter-owned:
    /// <c>PhoenixFlameInpSystem</c> is its only writer.
    /// </summary>
    /// <remarks>
    /// A pool entity rather than a world singleton, because this stage opens and closes: existence
    /// IS the lifecycle, deleting the entity IS the reset, and the teardown deletes it LAST, after
    /// the ids it carries have gone back to the asset service
    /// (adr-data-placement-is-decided-on-three-axes rule 7).
    /// <para>The six particle sprites are not here. They are derived at the hand-over, given to the
    /// view that draws with them and never read again, so they are locals of that step — by rule 4
    /// a value nothing reads later is not state at all.</para>
    /// </remarks>
    internal struct PhoenixFlameStageComp : IEcsComponent
    {
        /// <summary>Where the stage is. Stepped by <see cref="StageTransitions.Next"/>.</summary>
        public StageState State;

        /// <summary>
        /// The instance id of the screen this stage opened on, so a reopened scene reads as a
        /// different screen without a reference to the old one being kept.
        /// </summary>
        public int ScreenInstanceId;

        public int AtlasRequestId;
        public int BackgroundRequestId;

        /// <summary>The sprite cut from the loaded background, owned by the asset service.</summary>
        public int BackgroundId;
    }
}
