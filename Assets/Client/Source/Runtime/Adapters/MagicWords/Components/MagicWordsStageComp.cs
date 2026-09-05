using Client.Adapters.Shared.Stage;
using DCFApixels.DragonECS;

namespace Client.Adapters.MagicWords.Components
{
    /// <summary>
    /// The dialogue stage's lifecycle and the ids it holds open. Adapter-owned:
    /// <c>MagicWordsInpSystem</c> is its only writer, and it is alive exactly while the stage is.
    /// </summary>
    /// <remarks>
    /// A pool entity rather than a world singleton, because this stage opens and closes: existence
    /// IS the lifecycle, deleting the entity IS the reset, and every write is one a DEU rule can
    /// see. The teardown deletes it LAST, after releasing the ids it carries
    /// (adr-data-placement-is-decided-on-three-axes rule 7) — which is what the five-field
    /// <c>_state == Idle &amp;&amp; _screenInstanceId == 0 &amp;&amp; ...</c> guard used to write by
    /// hand.
    /// <para>Q1 of `state-inside-systems` is measured here: this feature carries both shapes side by
    /// side. A lifecycle lives on an entity; a table the Present half reads per call —
    /// <see cref="DialogueLogArtComp"/> — lives on a singleton.</para>
    /// </remarks>
    internal struct MagicWordsStageComp : IEcsComponent
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
        public int EmojiRequestId;

        /// <summary>The sprite cut from the loaded background, owned by the asset service.</summary>
        public int BackgroundId;
    }
}
