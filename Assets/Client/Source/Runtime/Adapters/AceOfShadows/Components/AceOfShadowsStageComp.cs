using Client.Adapters.Shared.Stage;
using DCFApixels.DragonECS;

namespace Client.Adapters.AceOfShadows.Components
{
    /// <summary>
    /// The card stage's lifecycle, the ids it holds open and how far its two cursors have run.
    /// Adapter-owned: <c>AceOfShadowsInpSystem</c> is its only writer.
    /// </summary>
    /// <remarks>
    /// A pool entity rather than a world singleton, because this stage opens and closes: existence
    /// IS the lifecycle, deleting the entity IS the reset, and the teardown deletes it LAST, after
    /// the ids and the view handles it carries have gone back to their owners
    /// (adr-data-placement-is-decided-on-three-axes rule 7). The <see cref="CardArtComp"/> table
    /// sits on a singleton beside it — that pair is the Q1 measurement of `state-inside-systems`.
    /// <para>The two cursors are here rather than on the system by rule 4: a fresh instance
    /// constructed mid-frame would reset the face order and the speed step, so they are owned and
    /// owned data is world data.</para>
    /// </remarks>
    internal struct AceOfShadowsStageComp : IEcsComponent
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

        /// <summary>
        /// The first view handle of this open. <c>ViewRegistryService</c> hands out handles in
        /// order, and this system is its only caller, so the views of one open are the contiguous
        /// range <c>FirstHandle .. FirstHandle + SpawnedCount - 1</c> — which is what the list of
        /// handles used to spell out one element at a time.
        /// </summary>
        public int FirstHandle;

        /// <summary>How many card views this open has spawned. The pool arrives over frames.</summary>
        public int SpawnedCount;

        /// <summary>
        /// How many cards have been bound to a view. The next unbound view is
        /// <c>FirstHandle + BoundCount</c>, and the face it shows is
        /// <c>BoundCount % FaceCount</c> — a property of the BIND order and never of the entity.
        /// </summary>
        public int BoundCount;

        /// <summary>Where the speed button has cycled to.</summary>
        public int SpeedIndex;
    }
}
